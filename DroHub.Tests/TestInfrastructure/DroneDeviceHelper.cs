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
        public static async Task mockDrone(DroHubFixture fixture, DroneRPC drone_rpc, string device_serial, DroneTestDelegate test_delegate)
        {
            var loggerFactory = new LoggerFactory().AddConsole().AddDebug(LogLevel.Trace);
            using (var ws_transport = new TWebSocketClient(fixture.ThriftUri, System.Net.WebSockets.WebSocketMessageType.Binary))
            using (var reverse_tunnel_transport = new TReverseTunnelServer(ws_transport, 1))
            {

                ws_transport.WebSocketOptions.SetRequestHeader("User-Agent", "AirborneProjets");
                ws_transport.WebSocketOptions.SetRequestHeader("Content-Type", "application/x-thrift");
                ws_transport.WebSocketOptions.SetRequestHeader("x-device-expected-serial", device_serial);

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
        public async Task verifyRecordedTelemetry(DroHubFixture fixture)
        {
            foreach (var telemetry_item in TelemetryItems)
            {
                Type telemetry_type = telemetry_item.Key;
                MethodInfo get_device_telemetry = typeof(HttpClientHelper).GetMethod("getDeviceTelemetry").MakeGenericMethod(telemetry_type);
                dynamic awaitable = get_device_telemetry.Invoke(null, new object[] { fixture, _device_serial, 1, 10 });
                await awaitable;
                dynamic result_list = awaitable.GetAwaiter().GetResult();
                ((IEnumerable)result_list).Cast<dynamic>().Single(s => s.Timestamp == telemetry_item.Value.Telemetry.Timestamp);
            }
        }

        public int getSignalRTasksTelemetry()
        {
            var tasks = TelemetryItems.Select(item => ((TelemetryMock.BaseTelemetryItem)item.Value).TaskSource.Task);
            return tasks
                .Where(t => t.Status == TaskStatus.RanToCompletion)
                .Select(t => t.Result)
                .Count();
        }

        public class BaseTelemetryItem
        {
            public TaskCompletionSource<bool> TaskSource { get; }
            public BaseTelemetryItem()
            {
                TaskSource = new TaskCompletionSource<bool>();
            }
        }

        public class TelemetryItem<T> : BaseTelemetryItem
        {
            public T Telemetry;
            public TelemetryItem(T telemetry, HubConnection connection, string type_name) : base()
            {
                Telemetry = telemetry;
                connection.On<string>(type_name, (message) => { this.TaskSource.TrySetResult(true); });
            }
        }

        public void AddTelemetryItem<T>(T value) where T : IDroneTelemetry
        {
            TelemetryItems.Add(typeof(T), new TelemetryItem<IDroneTelemetry>(value, _connection, typeof(T).FullName));
        }

        private HubConnection _connection;
        public async Task startMock(DroHubFixture fixture, string hub_uri, string user, string password, bool create_user = false, bool create_device = false)
        {
            _fixture = fixture;
            if (create_device)
            {
                http_helper = await HttpClientHelper.createDevice(_fixture, "SomeName", _device_serial, user, password, create_user);
                must_delete_device = true;
            }
            else
            {
                http_helper = await HttpClientHelper.createLoggedInUser(_fixture, user, password);
            }

            _connection = new HubConnectionBuilder()
                .WithUrl(new Uri("ws://localhost:5000/telemetryhub"), options => { options.Cookies.Add(http_helper.loginCookie); })
                .Build();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
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

        public async Task stopMock()
        {
            if (must_delete_device)
                (await HttpClientHelper.deleteDevice(_fixture, _device_serial)).Dispose();
        }
        public Dictionary<Type, TelemetryItem<IDroneTelemetry>> TelemetryItems { get; private set; }
        private string _device_serial;
        HttpClientHelper http_helper;
        private bool must_delete_device = false;
        DroHubFixture _fixture;

        public TelemetryMock(string device_serial)
        {
            _device_serial = device_serial;
        }
    }
}
// Thrift.Server.TSimpleAsyncServer.TSimpleAsyncServer(Thrift.Processor.ITAsyncProcessor processor, TServerTransport serverTransport, TProtocolFactory inputProtocolFactory, TProtocolFactory outputProtocolFactory, ILoggerFactory loggerFactory, int clientWaitingDelay = 10)
