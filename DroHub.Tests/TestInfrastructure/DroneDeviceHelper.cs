using System;
using System.Threading.Tasks;
using Thrift.Transport;
using Thrift.Protocol;
using DroHub.Helpers.Thrift;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Net;
using System.Reflection;
using System.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Generic;
using DroHub.Areas.DHub.Models;
using System.Linq;

namespace DroHub.Tests.TestInfrastructure
{
   class DroneDeviceHelper
    {
        public delegate Task DroneTestDelegate();
        public static async Task mockDrone(DroHubFixture fixture, DroneRPC drone_rpc, string device_serial, DroneTestDelegate test_delegate,
                string user, string token)
        {
            var loggerFactory = new LoggerFactory();
            using (var ws_transport = new TWebSocketClient(fixture.ThriftUri, System.Net.WebSockets.WebSocketMessageType.Binary))
            using (var reverse_tunnel_transport = new TReverseTunnelServer(ws_transport, 1))
            {
                ws_transport.WebSocketOptions.SetRequestHeader("User-Agent", "AirborneProjets");
                ws_transport.WebSocketOptions.SetRequestHeader("Content-Type", "application/x-thrift");
                ws_transport.WebSocketOptions.SetRequestHeader("x-device-expected-serial", device_serial);
                ws_transport.WebSocketOptions.SetRequestHeader("x-drohub-user", user);
                ws_transport.WebSocketOptions.SetRequestHeader("x-drohub-token", token);

                var message_validator_factory = new TAMessageValidatorProtocol.Factory(new TAJsonProtocol.Factory(),
                        TAMessageValidatorProtocol.ValidationModeEnum.KEEP_READING,
                        TAMessageValidatorProtocol.OperationModeEnum.SEQID_SLAVE);

                var processor = new Drone.AsyncProcessor(drone_rpc);
                var server_engine = new Thrift.Server.TSimpleAsyncServer(processor, reverse_tunnel_transport,
                        message_validator_factory, message_validator_factory, loggerFactory);
                await server_engine.ServeAsync(CancellationToken.None);
                await test_delegate();
            }
        }
    }


    public class TelemetryMock
    {
        public async Task<Dictionary<string, dynamic>> getRecordedTelemetry(DroHubFixture fixture) {
            var r = new Dictionary<string, dynamic>();
            foreach (var telemetry_item in TelemetryItems)
            {
                Type telemetry_type = telemetry_item.Key;
                MethodInfo get_device_telemetry = typeof(HttpClientHelper).GetMethod("getDeviceTelemetry")
                    .MakeGenericMethod(telemetry_type);
                dynamic awaitable = get_device_telemetry
                    .Invoke(null, new object[] { fixture, _device_serial, _user_name, _password, 1, 10 });

                await awaitable;
                dynamic result_list = awaitable.GetAwaiter().GetResult();
                var d =((IEnumerable)result_list).Cast<dynamic>()
                    .Single(s => s.Serial == telemetry_item.Value.Telemetry.Serial &&
                                s.Timestamp == telemetry_item.Value.Telemetry.Timestamp);
                r.Add(telemetry_item.ToString(), d);
            }

            return r;
        }

        public IEnumerable<string> getSignalRTasksTelemetry()
        {
            var tasks = TelemetryItems.Select(item => (item.Value).TaskSource.Task);
            return tasks
                .Where(t => t.Status == TaskStatus.RanToCompletion)
                .Select(t => t.Result);
        }

        public class BaseTelemetryItem
        {
            public TaskCompletionSource<string> TaskSource { get; }
            public BaseTelemetryItem()
            {
                TaskSource = new TaskCompletionSource<string>();
            }
        }

        public class TelemetryItem<T> : BaseTelemetryItem
        {
            public T Telemetry;
            public TelemetryItem(T telemetry, HubConnection connection, string type_name) : base()
            {
                Telemetry = telemetry;
                connection.On<string>(type_name, (message) => {
                    TaskSource.TrySetResult(message);
                });
            }
        }

        public void AddTelemetryItem<T>(T value) where T : IDroneTelemetry
        {
            TelemetryItems.Add(typeof(T), new TelemetryItem<IDroneTelemetry>(value, _connection, typeof(T).FullName));
        }

        public async Task WaitForServer() {
            var tasks = TelemetryItems.Select(item => ((TelemetryMock.BaseTelemetryItem)item.Value).TaskSource.Task);
            await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(10000));
        }

        private HubConnection _connection;
        public async Task startMock(DroHubFixture fixture, string user, string password, string organization_name,
            string user_base_type, int allowed_flight_time_minutes, int allowed_user_count, string hub_uri)
        {
            _fixture = fixture;
            _user_name = user;
            _password = password;

            _user = await HttpClientHelper.AddUserHelper.addUser(_fixture, user,
                password, organization_name, user_base_type, allowed_flight_time_minutes, allowed_user_count);
            _device = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, user,
                password, _device_serial, _device_serial);

            http_helper = await HttpClientHelper.createLoggedInUser(_fixture, user, password);

            _connection = new HubConnectionBuilder()
                .WithUrl(new Uri(hub_uri), options => { options
                    .Cookies
                    .Add(http_helper.loginCookie);
                })
                .Build();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            TelemetryItems = new Dictionary<Type, TelemetryItem<IDroneTelemetry>>();

            AddTelemetryItem<DronePosition>(
                    new DronePosition { Longitude = 0.0f, Latitude = 0.1f, Altitude = 10f, Serial = _device_serial, Timestamp = timestamp });

            AddTelemetryItem<DroneReply>(
                    new DroneReply { Result = true, Serial = _device_serial, Timestamp = timestamp });

            AddTelemetryItem<DroneRadioSignal>(
                    new DroneRadioSignal { SignalQuality = 2, Rssi = -23.0f, Serial = _device_serial, Timestamp = timestamp });

            AddTelemetryItem<DroneFlyingState>(
                    new DroneFlyingState { State = FlyingState.LANDED, Serial = _device_serial, Timestamp = timestamp });

            AddTelemetryItem<DroneBatteryLevel>(
                    new DroneBatteryLevel { BatteryLevelPercent = 100, Serial = _device_serial, Timestamp = timestamp });

            AddTelemetryItem<DroneLiveVideoStateResult>(
                    new DroneLiveVideoStateResult { State = DroneLiveVideoState.LIVE, Serial = _device_serial, Timestamp = timestamp });
            await _connection.StartAsync();
        }

        public async Task stopMock() {
            if (_device != null)
                await _device.DisposeAsync();
            if (_user != null)
                await _user.DisposeAsync();
        }

        public Dictionary<Type, TelemetryItem<IDroneTelemetry>> TelemetryItems { get; private set; }
        private string _device_serial;
        public string SerialNumber => _device_serial;
        HttpClientHelper http_helper;
        private DroHubFixture _fixture;
        private string _user_name;
        private string _password;
        private HttpClientHelper.AddUserHelper _user;
        private HttpClientHelper.CreateDeviceHelper _device;

        public TelemetryMock(string device_serial)
        {
            _device_serial = device_serial;
        }
    }
}
// Thrift.Server.TSimpleAsyncServer.TSimpleAsyncServer(Thrift.Processor.ITAsyncProcessor processor, TServerTransport serverTransport, TProtocolFactory inputProtocolFactory, TProtocolFactory outputProtocolFactory, ILoggerFactory loggerFactory, int clientWaitingDelay = 10)
