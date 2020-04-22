using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DroHub.Areas.DHub;
using DroHub.Areas.DHub.Controllers;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Thrift;
using Thrift.Protocol;
using Thrift.Transport;
using Thrift.Transport.Client;

namespace DroHub.Helpers.Thrift
{
    public class ThriftMessageHandler : IDisposable
    {
        private readonly ILogger<ThriftMessageHandler> _logger;
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
        private WebSocket _socket;
        private ConnectionManager _connection_manager;
        private readonly SignInManager<DroHubUser> _signin_manager;
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
                            var a = ascii.GetString(buffer, 0, maximum_to_flush);
                            _logger.LogDebug($"Send async res {res} !{a}");
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
                // transport = new TFramedTransport(transport);
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
                        _thrift_handler_instance._input_streams.Remove(_stream.Input);
                        _cn_src.Dispose();
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

        public ThriftMessageHandler(ConnectionManager connection_manager, SignInManager<DroHubUser> signin_manager, ILogger<ThriftMessageHandler> logger)
        {
            _is_disposed = false;
            _logger = logger;
            _task_list = new List<Task>();
            _input_streams = new List<EchoStream>();
            _connection_manager = connection_manager;
            _signin_manager = signin_manager;
        }

        private async Task<bool> passesHeaderChecks(HttpContext context) {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                _logger.LogDebug("Got a non websocket request.");
                context.Response.StatusCode = 400;
                return false;
            }

            if (context.Request.Headers["x-device-expected-serial"] == StringValues.Empty)
            {
                _logger.LogInformation("Peer did not provide a serial");
                context.Response.StatusCode = 400;
                return false;
            }

            if (context.Request.Headers["x-drohub-user"] == StringValues.Empty ||
                context.Request.Headers["x-drohub-token"] == String.Empty) {

                _logger.LogInformation("User did not provide a user or token");
                context.Response.StatusCode = 401;
                return false;
            }

            // Disable because we want to keep the abort short circuit pattern
            // ReSharper disable once InvertIf
            if ((await DeviceHelper.queryDeviceInfo(_signin_manager,
                    context.Request.Headers["x-drohub-user"],
                    context.Request.Headers["x-drohub-token"],
                    context.Request.Headers["x-device-expected-serial"])) == null) {

                _logger.LogWarning(
                    $"Failed authentication for {context.Request.Headers["x-drohub-user"]} {context.Request.Headers["x-drohub-token"]} and serial {context.Request.Headers["x-device-expected-serial"]}");
                context.Response.StatusCode = 401;
                return false;
            }
            return true;
        }

        public async Task runHandler(HttpContext context, IThriftTasks tasks) {
            if (!await passesHeaderChecks(context))
                return;

            if (!await tasks.doesItPassPreconditions(context.Request.Headers["x-device-expected-serial"])) {
                context.Response.StatusCode = 401;
                return;
            }

            var last_connection = _connection_manager.GetRPCSessionBySerial(_serial_number);
            if (last_connection != null && !last_connection._is_disposed) {
                _cancellation_token_src.Cancel();
                last_connection._connection_manager.RemoveSocket(_serial_number);
            }

            _serial_number = context.Request.Headers["x-device-expected-serial"];
            _cancellation_token_src = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            try {
                _socket = await context.WebSockets.AcceptWebSocketAsync();
                _task_list.Add(Task.Run(async () => await ReceiveFromWebSocket(_socket)));
                _connection_manager.AddSocket(this);
                var new_tasks = await tasks.getTasks(this, _cancellation_token_src);
                if (!new_tasks.Any()) {
                    _logger.LogInformation("No tasks were given for this socket. Closing.");
                    return;
                }

                await Task.WhenAll(_task_list.ToArray());
                _logger.LogDebug("Finished all tasks.");
            }
            finally {
                // don't leave the socket in any potentially connected state
                if (_socket.State != WebSocketState.Aborted) {
                    await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure,
                        "Good bye",
                        CancellationToken.None);
                }
                if (!_cancellation_token_src.IsCancellationRequested)
                    _cancellation_token_src.Cancel();
            }
        }

        public void Dispose()
        {
            if (_is_disposed)
                return;
            _is_disposed = true;
            try {
                _connection_manager.RemoveSocket(_serial_number);
            }
            catch (Exception e) {
                _logger.LogWarning(e.Message);
            }
            _cancellation_token_src?.Cancel();
            _socket?.Abort();
            _socket?.Dispose();

            _cancellation_token_src?.Dispose();
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
            var buffer = new byte[1024 * 4];
            try {
                while (socket.State == WebSocketState.Open &&
                       !_cancellation_token_src.Token.IsCancellationRequested) {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer),
                        _cancellation_token_src.Token);

                    if (result.MessageType == WebSocketMessageType.Binary) {
                        populateInputStreams(buffer, 0, result.Count);
                    }
                    else
                        _cancellation_token_src.Cancel();
                }
            }
            catch (Exception) {
                ;
            }
            finally {
                if (socket.State == WebSocketState.Open)
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure,
                        "Good bye",
                        CancellationToken.None);
                _cancellation_token_src.Cancel(); //Regardless of whether it was canceled or not.
                _logger.LogDebug($"Finished processing received loop in state {socket.State}");
            }
        }
    }
}