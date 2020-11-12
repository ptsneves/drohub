using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Thrift;
using Thrift.Protocol;
using Thrift.Transport;
using Thrift.Transport.Client;

namespace DroHub.Helpers.Thrift
{
    public class ThriftMessageHandler : IAsyncDisposable
    {
        private readonly ILogger<ThriftMessageHandler> _logger;
        private readonly List<Task> _task_list;
        private readonly List<Channel<byte[]>> _input_consumers;
        private readonly DeviceAPI _device_api;
        private readonly DeviceConnectionAPI _connection_api;

        private CancellationTokenSource _cancellation_token_src;
        private bool _is_disposed;
        private WebSocket _socket;

        public DeviceAPI.DeviceSerial SerialNumber { get ; private set; }

        private class TWebSocketStream : TStreamTransport
        {
            private readonly WebSocket _socket;
            private readonly ILogger _logger;
            public TWebSocketStream(Stream input_stream, WebSocket socket, ILogger logger) :
                base(input_stream, new MemoryStream(512))
            {
                _socket = socket;
                _logger = logger;
            }
            public override async Task FlushAsync(CancellationToken tkn)
            {
                if (_socket.State == WebSocketState.Open) {
                    if (tkn.IsCancellationRequested) {
                        await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Good bye", CancellationToken.None);
                        return;
                    }
                    var buffer = ((MemoryStream) OutputStream).ToArray();
                    _logger.LogDebug("Send async res {ascii}",  new UTF8Encoding().GetString(buffer));
                    await _socket.SendAsync(buffer, WebSocketMessageType.Binary, true, tkn);
                    OutputStream = new MemoryStream(512);
                    _logger.LogDebug("Sent");
                }
            }
        }

        public class ThriftClient<C> : IDisposable where C : TBaseClient {
            private C _client;
            private readonly ChannelInputStream _channel_input_stream;
            private readonly List<Channel<byte[]>> _input_consumers;
            private readonly Channel<byte[]> _input_channel;

            public C Client {
                get {
                    if (_channel_input_stream != null)
                        return _client;
                    throw new ObjectDisposedException("This thrift client has been disposed.");
                }
            }

            internal ThriftClient(WebSocket socket,
                List<Channel<byte[]>> input_consumers, ILogger logger) {

                _input_consumers = input_consumers;
                _input_channel = Channel.CreateUnbounded<byte[]>();
                lock(_input_consumers){
                    _input_consumers.Add(_input_channel);
                }
                _channel_input_stream = new ChannelInputStream(_input_channel);
                TTransport transport = new TWebSocketStream(_channel_input_stream, socket, logger);
                TProtocol protocol = new TAJsonProtocol(transport);
                protocol = new TAMessageValidatorProtocol(protocol, TAMessageValidatorProtocol.ValidationModeEnum.KEEP_READING,
                    TAMessageValidatorProtocol.OperationModeEnum.SEQID_MASTER);
                _client = (C)Activator.CreateInstance(typeof(C), protocol);
            }

            public void Dispose() {
                if (_client == null)
                    return;

                _client = null;
                lock (_input_consumers) {
                    _input_consumers.Remove(_input_channel);
                }
                _channel_input_stream?.Dispose();
            }
        }

        public ThriftClient<C> getClient<C>(ILogger logger) where C : TBaseClient {
            // We lock inside
            // ReSharper disable once InconsistentlySynchronizedField
            return new ThriftClient<C>(_socket, _input_consumers, logger);
        }

        public ThriftMessageHandler(DeviceAPI device_api,
            ILogger<ThriftMessageHandler> logger,
            DeviceConnectionAPI connection_api)
        {
            _is_disposed = false;
            _logger = logger;
            _connection_api = connection_api;
            _task_list = new List<Task>();
            _device_api = device_api;
            _input_consumers = new List<Channel<byte[]>>();
        }

        public async Task runHandler(HttpContext context, IThriftTasks tasks) {
            if (!context.WebSockets.IsWebSocketRequest) {
                _logger.LogDebug("Got a non websocket request.");
                context.Response.StatusCode = 400;
                return;
            }

            var device = await _connection_api.getDeviceFromCurrentConnectionClaim();
            SerialNumber = new DeviceAPI.DeviceSerial(device.SerialNumber);

            var is_duplicate_connection = true;
            var remove_duplicate_connection_attempt = 0;
            while (is_duplicate_connection) {
                var last_connection = DeviceConnectionAPI.getRPCSessionOrDefault(device);

                if (remove_duplicate_connection_attempt > 3) {
                    _logger.LogError("Could not remove connection after 3 attempts. Aborting.");
                    context.Response.StatusCode = 400;
                    return;
                }

                if (last_connection != null && !last_connection._is_disposed) {
                    _logger.LogError($"Removing last connection {_device_api.getDeviceSerialNumberFromConnectionClaim()}");
                    last_connection._cancellation_token_src.Cancel();
                    remove_duplicate_connection_attempt++;
                    Thread.Sleep(500);
                }
                else {
                    is_duplicate_connection = false;
                }
            }


            _cancellation_token_src = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            try {
                _socket = await context.WebSockets.AcceptWebSocketAsync();

                await _connection_api.addRPCSessionHandler(this);
                _task_list.Add(ReceiveFromWebSocket(_socket));
                _task_list.Add(tasks.doTask(_cancellation_token_src));
                await Task.WhenAll(_task_list);
                _logger.LogDebug("Finished all tasks.");
            }
            finally {
                // don't leave the socket in any potentially connected state
                // if (_socket.State != WebSocketState.Aborted) {
                //     await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure,
                //         "Good bye",
                //         CancellationToken.None);
                // }
                if (!_cancellation_token_src.IsCancellationRequested) {
                    _logger.LogInformation($"Finished all tasks and cancelling {_device_api.getDeviceSerialNumberFromConnectionClaim()}");
                    _cancellation_token_src.Cancel();
                }
            }
        }

        public async ValueTask DisposeAsync() {
            if (_is_disposed)
                return;
            if (_cancellation_token_src != null)
                _logger.LogInformation($"Disposing {SerialNumber.Value} isCancelled = {_cancellation_token_src.Token.IsCancellationRequested}");

            _is_disposed = true;
            try {
                await _connection_api.removeRPCSessionHandler(this);
            }
            catch (Exception e) {
                _logger.LogWarning(e.Message);
            }

            _cancellation_token_src?.Cancel();
            _socket?.Abort();
            _socket?.Dispose();

            _cancellation_token_src?.Dispose();
        }

        private void populateInputStreams(byte[] buffer) {
            _logger.LogDebug("Received {ascii}", new UTF8Encoding().GetString(buffer));
            lock (_input_consumers) {
                _input_consumers.ForEach(c => c.Writer.WriteAsync(buffer));
            }
        }

        private async Task ReceiveFromWebSocket(WebSocket socket)
        {
            var buffer = new byte[1024 * 4];
            try {
                while (socket.State == WebSocketState.Open &&
                       !_cancellation_token_src.Token.IsCancellationRequested) {
                    var result = await _socket.ReceiveAsync(buffer, _cancellation_token_src.Token);

                    if (result.MessageType == WebSocketMessageType.Binary) {

                        populateInputStreams(buffer.Take(result.Count).ToArray());
                    }
                    else {
                        _logger.LogWarning($"Cancelling receive! {_device_api.getDeviceSerialNumberFromConnectionClaim()}");
                        _cancellation_token_src.Cancel();
                    }
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