using System.Threading.Tasks;
using System.Threading;
using System.Net.WebSockets;
using System.Text;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using Thrift.Transport;
using Thrift.Transport.Client;
using Thrift.Protocol;
using Thrift;
namespace DroHub.Helpers.Thrift
{
    public class ThriftMessageHandler
    {
        private readonly ILogger<ThriftMessageHandler> _logger;
        private string _serial_number;
        private CancellationTokenSource _cancellation_token_src;
        private readonly List<Task> _task_list;
        private readonly List<EchoStream> _input_streams;
        private bool _is_disposed;
        private string _socket_id;
        private WebSocket _socket;
        private ConnectionManager _connection_manager;
        public string SerialNumber { get { return _serial_number; } }

        public class TWebSocketStream : TStreamTransport
        {
            private readonly WebSocket socket;
            private readonly ILogger _logger;
            private readonly CancellationToken _tkn;
            public TWebSocketStream(Stream input_stream, WebSocket socket_, ILogger logger, CancellationToken tkn) : base(input_stream, new EchoStream(1024, tkn))
            {
                socket = socket_;
                _logger = logger;
                _tkn = tkn;
            }
            public override async Task FlushAsync(CancellationToken tkn)
            {
                if (socket.State == WebSocketState.Open)
                {
                    if (tkn.IsCancellationRequested)
                    {
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Good bye", CancellationToken.None);
                        return;
                    }

                    if (OutputStream.CanRead)
                    {
                        int maximum_to_flush = (int)Math.Min(Int32.MaxValue, Math.Max(OutputStream.Length, 1));
                        var buffer = new byte[maximum_to_flush];
                        var res = OutputStream.Read(buffer, 0, maximum_to_flush);
                        OutputStream = new EchoStream(1024, _tkn);
                        if (res > 0)
                        {
                            ASCIIEncoding ascii = new ASCIIEncoding();
                            var a = ascii.GetString(buffer, 0, maximum_to_flush);
                            _logger.LogDebug($"Send async res {res} !{a}");
                            await socket.SendAsync(buffer, WebSocketMessageType.Binary, true, tkn);
                        }
                        else
                        {
                            _logger.LogError("???");
                            return;
                        }
                    }
                }
            }
        }

        public class ThriftClient<C> : IDisposable where C : TBaseClient
        {
            public C Client { get; }
            private bool _is_disposed;
            private readonly ThriftMessageHandler _thrift_handler_instance;
            CancellationTokenSource _cn_src;
            private readonly ILogger _logger;
            private readonly string _serial_number;
            private readonly EchoStream _input_stream;
            public ThriftClient(ThriftMessageHandler th, CancellationToken tkn, ILogger logger)
            {
                _cn_src = CancellationTokenSource.CreateLinkedTokenSource(tkn);
                _is_disposed = false;
                _thrift_handler_instance = th;
                _logger = logger;

                _input_stream = new EchoStream(1024, tkn);
                lock (th._input_streams)
                {
                    th._input_streams.Add(_input_stream);
                }
                TTransport transport = new TWebSocketStream(_input_stream, th._socket, logger, tkn);
                TProtocol protocol = new TAJsonProtocol(transport);
                protocol = new TAMessageValidatorProtocol(protocol, TAMessageValidatorProtocol.ValidationModeEnum.KEEP_READING,
                    TAMessageValidatorProtocol.OperationModeEnum.SEQID_MASTER);
                Client = (C)Activator.CreateInstance(typeof(C), protocol);
                _serial_number = th.SerialNumber;
            }
            public void Dispose()
            {
                Dispose(true);
            }
            protected virtual void Dispose(bool disposing)
            {
                if (!_is_disposed && disposing)
                {
                    lock (_thrift_handler_instance._input_streams)
                    {
                        _cn_src.Cancel();
                        _thrift_handler_instance._input_streams.Remove(_input_stream);
                    }
                }
                _is_disposed = true;
            }
        }
        public ThriftClient<C> getClient<C>(ILogger logger) where C : TBaseClient
        {
            return new ThriftClient<C>(this, _cancellation_token_src.Token, logger);
        }

        public ThriftMessageHandler(ConnectionManager connection_manager, ILogger<ThriftMessageHandler> logger)
        {
            _is_disposed = false;
            _logger = logger;
            _task_list = new List<Task>();
            _input_streams = new List<EchoStream>();
            _connection_manager = connection_manager;
        }

        public async Task runHandler(HttpContext context, IThriftTasks tasks) {

            if (!context.WebSockets.IsWebSocketRequest)
            {
                _logger.LogDebug("Got a non websocket request.");
                context.Response.StatusCode = 400;
                return;
            }

            // if (context.Request.ContentType != "application/x-thrift")
            // {
            //     _logger.LogInformation("Got a non thrift message");
            //     context.Response.StatusCode = 400;
            //     return;
            // }

            if (context.Request.Headers["x-device-expected-serial"] == StringValues.Empty)
            {
                _logger.LogInformation("Peer did not provide a serial");
                context.Response.StatusCode = 400;
                return;
            }

            try
            {
                _serial_number = context.Request.Headers["x-device-expected-serial"];
                _cancellation_token_src = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
                _socket = await context.WebSockets.AcceptWebSocketAsync();
                _task_list.Add(Task.Run(async () => await ReceiveFromWebSocket(_socket)));
                _socket_id = _connection_manager.AddSocket(this);
                var new_tasks = await tasks.getTasks(this, _cancellation_token_src.Token);
                if (!new_tasks.Any())
                {
                    _logger.LogInformation("No tasks were given for this socket. Closing.");

                    return;
                }

                await Task.WhenAll(_task_list.ToArray());
                _logger.LogDebug("Finished all tasks.");
            }
            finally
            {
                try
                {
                    _connection_manager.RemoveSocket(_socket_id);
                    if (!_cancellation_token_src.IsCancellationRequested)
                        _cancellation_token_src.Cancel();

                    // don't leave the socket in any potentially connected state
                    if (_socket.State != WebSocketState.Closed)
                        _socket.Abort();

                }
                catch (Exception e)
                {
                    _logger.LogWarning(e.Message);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_is_disposed && disposing)
            {
               
            }
            _is_disposed = true;
        }

        private void populateInputStreams(byte[] buffer, int offset, int count)
        {
            lock (_input_streams)
            {
                UTF8Encoding utf8 = new UTF8Encoding();
                _logger.LogDebug("Received {ascii}", utf8.GetString(buffer, 0, count));
                foreach (var input_stream in _input_streams)
                {
                    if (input_stream.CanWrite)
                    {
                        input_stream.Write(buffer, 0, count);
                    }
                }
            }
        }

        private async Task ReceiveFromWebSocket(WebSocket socket)
        {
            WebSocketReceiveResult result = null;
            var buffer = new byte[1024 * 4];
            try
            {
                while (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted
                    && !_cancellation_token_src.Token.IsCancellationRequested)
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellation_token_src.Token);
                    if (!_cancellation_token_src.Token.IsCancellationRequested)
                    {
                        if (socket.State == WebSocketState.CloseReceived && result.MessageType == WebSocketMessageType.Close)
                        {
                            _logger.LogDebug("Acknowledging Close frame received from client");
                            _cancellation_token_src.Cancel();
                            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge Close frame", CancellationToken.None);
                        }

                        if (socket.State == WebSocketState.Open)
                        {
                            if (result.MessageType == WebSocketMessageType.Binary)
                            {
                                populateInputStreams(buffer, 0, result.Count);
                            }
                        }
                    }
                }
                await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            }
            finally
            {
                if (!_cancellation_token_src.IsCancellationRequested)
                    _cancellation_token_src.Cancel();
                _logger.LogDebug($"Finished processing received loop in state {socket.State}");
           }
        }
    }
}