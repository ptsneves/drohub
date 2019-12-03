using System.Threading.Tasks;
using System.Threading;
using System.Net.WebSockets;
using System.Text;
using System.Web;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
        private readonly TaskCompletionSource<bool> _task_completion_src;
        private string _serial_number;
        private CancellationTokenSource _cancellation_token_src;
        private readonly List<Task> _task_list;
        private class IOEchoStream
        {
            public EchoStream Input { get; }
            public EchoStream Output { get; }
            public IOEchoStream(CancellationToken tkn)
            {
                Input = new EchoStream(1024, tkn);
                Output = new EchoStream(1024, tkn);
            }
        }
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
            public TWebSocketStream(Stream input_stream, Stream output_stream, WebSocket socket_, ILogger logger) : base(input_stream, output_stream)
            {
                socket = socket_;
                _logger = logger;
            }
            public override async Task FlushAsync(CancellationToken tkn)
            {
                var output_stream = OutputStream;
                if (socket.State == WebSocketState.Open)
                {
                    if (tkn.IsCancellationRequested)
                    {
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Good bye", CancellationToken.None);
                        return;
                    }

                    int maximum_to_flush = (int)Math.Min(Int32.MaxValue, Math.Max(output_stream.Length, 1));
                    var buffer = new byte[maximum_to_flush];
                    if (output_stream.CanRead)
                    {
                        var res = output_stream.Read(buffer, 0, maximum_to_flush);
                        if (res > 0)
                        {
                            ASCIIEncoding ascii = new ASCIIEncoding();

                            _logger.LogDebug($"Send async res {res} !" + ascii.GetString(buffer, 0, maximum_to_flush));
                            await socket.SendAsync(buffer, WebSocketMessageType.Binary, true, tkn);
                            _logger.LogDebug("Sent");
                        }
                        else
                        {
                            _logger.LogDebug("???");
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
            private readonly IOEchoStream _stream;
            CancellationTokenSource _cn_src;
            private readonly ILogger _logger;
            private readonly string _serial_number;
            public string SerialNumber { get { return _serial_number; } }
            public ThriftClient(ThriftMessageHandler th, CancellationToken tkn, ILogger logger)
            {
                _cn_src = CancellationTokenSource.CreateLinkedTokenSource(tkn);
                _is_disposed = false;
                _thrift_handler_instance = th;
                _logger = logger;

                _stream = new IOEchoStream(tkn);
                lock (th._input_streams)
                {
                    th._input_streams.Add(_stream.Input);
                }
                TTransport transport = new TWebSocketStream(_stream.Input, _stream.Output, th._socket, logger);
                transport = new TFramedTransport(transport);
                TProtocol protocol = new TJsonProtocol(transport);
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
                        _thrift_handler_instance._input_streams.Remove(_stream.Input);


                        _logger.LogDebug("Disposed");
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
            _task_completion_src = new TaskCompletionSource<bool>();
            _logger = logger;
            _task_list = new List<Task>();
            _input_streams = new List<EchoStream>();
            _connection_manager = connection_manager;
        }

        public async Task runHandler(HttpContext context, IThriftTasks tasks) {
            try
            {
                _serial_number = context.Request.Headers["x-device-expected-serial"];
                _cancellation_token_src = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    _logger.LogDebug("Got a non websocket request.");
                    context.Response.StatusCode = 400;
                    return;
                }

                if (context.Request.ContentType != "application/x-thrift")
                {
                    _logger.LogInformation("Got a non thrift message");
                    context.Response.StatusCode = 400;
                    return;
                }

                if (context.Request.Headers["x-device-expected-serial"] == StringValues.Empty)
                {
                    _logger.LogInformation("Peer did not provide a serial");
                    context.Response.StatusCode = 400;
                    return;
                }
                _socket = await context.WebSockets.AcceptWebSocketAsync();
                _task_list.Add(Task.Run(async () => await ReceiveFromWebSocket(_socket)));
                _task_list.AddRange(tasks.getTasks(this, _cancellation_token_src.Token));
                _socket_id = _connection_manager.AddSocket(this);
                await _task_completion_src.Task;
                _cancellation_token_src.Cancel();
            }
            finally
            {
                try
                {
                    _connection_manager.RemoveSocket(_socket_id);
                    _logger.LogDebug($"Stopping all tasks for disconnect ${_cancellation_token_src.IsCancellationRequested}");
                    await Task.WhenAll(_task_list.ToArray());
                    _logger.LogDebug("Finished Stopping all tasks for disconnect");
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
                // _logger.LogDebug("{ascii}", utf8.GetString(buffer, 0, count));
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
            try
            {
                var buffer = new byte[1024 * 4];
                WebSocketReceiveResult result = null;
                while (socket.State == WebSocketState.Open)
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellation_token_src.Token);

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        populateInputStreams(buffer, 0, result.Count);
                    }
                }
                if (result != null)
                    await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                if (_cancellation_token_src.Token.IsCancellationRequested)
                {
                    _logger.LogDebug("Receive was canceled");
                    _task_completion_src.SetCanceled();
                }
                else
                {
                    _logger.LogDebug("Receive was successful");
                    _task_completion_src.SetResult(true);
                }
                _logger.LogDebug("Finished Receive");
            }
            catch (Exception e)
            {
                _task_completion_src.TrySetException(e);
            }
        }
    }
}