using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Models;
using DroHub.Helpers.Thrift;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using Thrift.Protocol;
using Thrift.Server;

namespace DroHub.Tests.TestInfrastructure
{
    internal static class DroneDeviceHelper
    {
        public delegate Task DroneTestDelegate();
        public static async Task mockDrone(DroHubFixture fixture, DroneRPC drone_rpc, string device_serial, DroneTestDelegate test_delegate,
                string user, string token)
        {
            var loggerFactory = new LoggerFactory();
            using var ws_transport = new TWebSocketClient(DroHubFixture.ThriftUri, WebSocketMessageType.Binary, false);
            using var reverse_tunnel_transport = new TReverseTunnelServer(ws_transport, 1);
            ws_transport.WebSocketOptions.SetRequestHeader("User-Agent", "AirborneProjects");
            ws_transport.WebSocketOptions.SetRequestHeader("Content-Type", "application/x-thrift");
            ws_transport.WebSocketOptions.SetRequestHeader("x-device-expected-serial", device_serial);
            ws_transport.WebSocketOptions.SetRequestHeader("x-drohub-user", user);
            ws_transport.WebSocketOptions.SetRequestHeader("x-drohub-token", token);

            var message_validator_factory = new TAMessageValidatorProtocol.Factory(new TAJsonProtocol.Factory(),
                TAMessageValidatorProtocol.ValidationModeEnum.KEEP_READING,
                TAMessageValidatorProtocol.OperationModeEnum.SEQID_SLAVE);

            var processor = new Drone.AsyncProcessor(drone_rpc);
            var server_engine = new TSimpleAsyncServer(processor, reverse_tunnel_transport,
                message_validator_factory, message_validator_factory, loggerFactory);
            await server_engine.ServeAsync(CancellationToken.None);
            await test_delegate();
        }
    }

    public class TelemetryMock
    {
        public delegate Task StageThriftDroneTestDelegate(DroneRPC drone_rpc, TelemetryMock telemetry_mock,
            string user_name, string token);

        public static async Task stageThriftDrone(DroHubFixture fixture, bool infinite, int minutes, string user_name,
            string password, int allowed_user_count, string organization, string serial_number,
                StageThriftDroneTestDelegate del) {

            var telemetry_mock = new TelemetryMock(serial_number);

            password = (user_name == "admin@drohub.xyz") ? fixture.AdminPassword : password;
            await telemetry_mock.startMock(fixture, user_name, password, organization,
                "ActingPilot", minutes, allowed_user_count,
                DroHubFixture.TelemetryHubUri.ToString());

            var drone_rpc = new DroneRPC(telemetry_mock, infinite);

            try {
                var token = (await HttpClientHelper.getApplicationToken(user_name,
                    password))["result"];
                await del(drone_rpc, telemetry_mock, user_name, token);
            }
            finally {
                drone_rpc.Dispose();
                await telemetry_mock.stopMock();
            }
        }

        public async Task<Dictionary<string, dynamic>> getRecordedTelemetry() {
            var r = new Dictionary<string, dynamic>();
            foreach (var telemetry_item in TelemetryItems)
            {
                var get_device_telemetry = typeof(HttpClientHelper).GetMethod(nameof(HttpClientHelper.getDeviceTelemetry))
                    ?.MakeGenericMethod(telemetry_item.Key);
                if (get_device_telemetry == null)
                    throw new InvalidProgramException("Could not get device telemetry object");

                dynamic awaitable = get_device_telemetry
                    .Invoke(null, new object[] {SerialNumber, UserName, _password});

                IEnumerable result_list = await awaitable;
                try {
                    var d = result_list.Cast<dynamic>()
                        .Single(s => s.Serial == telemetry_item.Value.Telemetry.Serial &&
                                     s.Timestamp == telemetry_item.Value.Telemetry.Timestamp);

                    r.Add(telemetry_item.ToString(), d);
                }
                catch (InvalidOperationException) {
                    throw new InvalidOperationException($"Error in getting a single item. Dump {telemetry_item.Key}:\n" + JsonConvert
                        .SerializeObject(result_list));
                }
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

            protected BaseTelemetryItem()
            {
                TaskSource = new TaskCompletionSource<string>();
            }
        }

        public class TelemetryItem<T> : BaseTelemetryItem, IDisposable
        {
            public readonly T Telemetry;
            private readonly CancellationTokenSource cts;
            private readonly CancellationTokenRegistration cts_callback;
            public TelemetryItem(T telemetry, HubConnection connection, string type_name)
            {
                cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                cts_callback = cts.Token.Register(() => {
                    // this callback will be executed when token is cancelled
                    if (!TaskSource.Task.IsCompleted)
                        TaskSource.TrySetCanceled();
                });
                Telemetry = telemetry;
                connection.On<string>(type_name, message => {
                    TaskSource.SetResult(message);
                });
            }

            public void Dispose() {
                cts_callback.Dispose();
                cts.Dispose();
            }
        }

        private void AddTelemetryItem<T>(Type type, T value) where T : IDroneTelemetry
        {
            TelemetryItems.Add(type, new TelemetryItem<IDroneTelemetry>(value, _connection, type.FullName));
        }

        public static Dictionary<Type, IDroneTelemetry> generateTelemetry(string device_serial) {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return new Dictionary<Type, IDroneTelemetry> {
                [typeof(DronePosition)] = new DronePosition { Longitude = 0.0f, Latitude = 0.1f, Altitude = 10f,
                    Serial = device_serial, Timestamp = timestamp },
                [typeof(DroneReply)] = new DroneReply { Result = true, Serial = device_serial, Timestamp = timestamp },
                [typeof(DroneRadioSignal)] = new DroneRadioSignal { SignalQuality = 2, Rssi = -23.0f, Serial = device_serial,
                Timestamp = timestamp },
                [typeof(DroneFlyingState)] = new DroneFlyingState { State = FlyingState.LANDED, Serial = device_serial,
                    Timestamp = timestamp },
                [typeof(DroneBatteryLevel)] = new DroneBatteryLevel { BatteryLevelPercent = 100, Serial = device_serial,
                    Timestamp = timestamp },
                [typeof(DroneLiveVideoStateResult)] = new DroneLiveVideoStateResult { State = DroneLiveVideoState.LIVE,
                    Serial = device_serial, Timestamp = timestamp },
                [typeof(CameraState)] = new CameraState { Mode = CameraMode.VIDEO, ZoomLevel = 1.2f, MinZoom = 1.0f,
                    MaxZoom = 2.0f, Serial = device_serial, Timestamp = timestamp},
                [typeof(GimbalState)] = new GimbalState {
                    Pitch = 1.2f,
                    Roll = 1.3f,
                    Yaw = 1.4f,
                    MaxPitch = 180f,
                    MaxRoll = 180f,
                    MaxYaw = 90f,
                    MinPitch = 0,
                    MinRoll = -90f,
                    MinYaw = -180f,
                    CalibrationState = GimbalCalibrationState.CALIBRATED,
                    IsPitchStabilized = true,
                    IsRollStastabilized = true,
                    IsYawStabilized = false,
                    Serial = device_serial,
                    Timestamp = timestamp
                }
            };
        }

        private void generateTelemetryItems() {
            TelemetryItems = new Dictionary<Type, TelemetryItem<IDroneTelemetry>>();
            foreach (var (key, value) in generateTelemetry(SerialNumber)) {
                AddTelemetryItem(key, value);
            }
        }

        public async Task WaitForServer() {
            var tasks = TelemetryItems.Select(item => item.Value.TaskSource.Task);
            try {
                await Task.WhenAll(tasks);
            }
            catch (TaskCanceledException) {
                var s = "";
                foreach (var (key, value) in TelemetryItems) {
                    var t = value.TaskSource.Task;
                    if (t.IsCanceled)
                        s += $"Exception:  {key} {SerialNumber}   \n";
                }
                throw new InvalidOperationException(s);
            }
        }

        private HubConnection _connection;

        private async Task startMock(DroHubFixture fixture, string user, string password, string organization_name,
            string user_base_type, int allowed_flight_time_minutes, int allowed_user_count, string hub_uri)
        {
            _fixture = fixture;
            UserName = user;
            _password = password;

            _user = await HttpClientHelper.AddUserHelper.addUser(_fixture, user,
                password, organization_name, user_base_type, allowed_flight_time_minutes, allowed_user_count);
            _device = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, user,
                password, SerialNumber, SerialNumber);

            http_helper = await HttpClientHelper.createLoggedInUser(user, password);

            _connection = new HubConnectionBuilder()
                .WithUrl(new Uri(hub_uri), options => {
                    options
                        .Cookies
                        .Add(http_helper.loginCookie);

                    var handler = new HttpClientHandler {
                        ClientCertificateOptions = ClientCertificateOption.Manual,
                        ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true
                    };
                    options.HttpMessageHandlerFactory = _ => handler;
                    options.WebSocketConfiguration = sockets =>
                    {
                        sockets.RemoteCertificateValidationCallback = (sender, certificate, chain, policyErrors) => true;
                    };
                })
                .Build();
            generateTelemetryItems();
            await _connection.StartAsync();
            if (_connection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Cannot connect to signalR");
        }

        private async Task stopMock() {
            if (_device != null)
                await _device.DisposeAsync();
            if (_user != null)
                await _user.DisposeAsync();
            foreach (var item in TelemetryItems.Values) {
                item.Dispose();
            }
        }

        public Dictionary<Type, TelemetryItem<IDroneTelemetry>> TelemetryItems { get; private set; }
        public string SerialNumber { get; }

        public string UserName { get; private set; }

        private HttpClientHelper http_helper;
        private DroHubFixture _fixture;
        private string _password;
        private HttpClientHelper.AddUserHelper _user;
        private HttpClientHelper.CreateDeviceHelper _device;

        private TelemetryMock(string device_serial)
        {
            SerialNumber = device_serial;
        }
    }
}
// Thrift.Server.TSimpleAsyncServer.TSimpleAsyncServer(Thrift.Processor.ITAsyncProcessor processor, TServerTransport serverTransport, TProtocolFactory inputProtocolFactory, TProtocolFactory outputProtocolFactory, ILoggerFactory loggerFactory, int clientWaitingDelay = 10)
