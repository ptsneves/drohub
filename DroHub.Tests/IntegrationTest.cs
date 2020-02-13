using System;
using Xunit;
using DroHub.Tests.TestInfrastructure;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Generic;
using System.Net.Http;

namespace DroHub.Tests
{
    public class IntegrationTest : IClassFixture<DroHubFixture>
    {
        DroHubFixture _fixture;
        public IntegrationTest(DroHubFixture fixture) {
            _fixture = fixture;
        }

        [Fact]
        public async void TestLoginIsHomePage() {
            using (var http_helper = await HttpClientHelper.createHttpClient(_fixture))
                Assert.Equal(new Uri(_fixture.SiteUri, "Identity/Account/Login?ReturnUrl=%2FIdentity%2FAccount%2FManage"),
                    http_helper.Response.RequestMessage.RequestUri);
        }

        [InlineData("admin", null, false)]
        [InlineData("admin", "1", true, false)]
        [InlineData("guest@drohub.xyz", "1", false, true)]
        [InlineData("guest", "1", true, true, false, true)] //The create does not fail but the user is actually not created thus login and delete fail
        [Theory]
        public async void TestUserCreateAndLogin(string user, string password, bool expect_login_fail, bool create =false,
                bool expect_create_fail = false, bool expect_delete_fail = false)
        {
            if(create) {
                if (!expect_create_fail)
                    using (var http_client_helper = await HttpClientHelper.addUser(_fixture, user, password)) { }
                else
                    await Assert.ThrowsAsync<System.InvalidProgramException>(async () => (await HttpClientHelper.addUser(_fixture, user, password)).Dispose());
            }
            try
            {
                if (password == null)
                    password = _fixture.AdminPassword;

                if (expect_login_fail)
                    await Assert.ThrowsAsync<System.InvalidProgramException>(async () => (await HttpClientHelper.createLoggedInUser(_fixture, user, password)).Dispose());
                else
                    using (var http_client_helper = await HttpClientHelper.createLoggedInUser(_fixture, user, password)) { }
            }
            finally {
                if (create)
                {
                    if (!expect_delete_fail)
                    {
                        (await HttpClientHelper.deleteUser(_fixture, user, password)).Dispose();
                    }
                    else
                        await Assert.ThrowsAsync<System.InvalidProgramException>(async () => (await HttpClientHelper.deleteUser(_fixture, user, password)).Dispose());

                    await Assert.ThrowsAsync<System.InvalidProgramException>(async () => (await HttpClientHelper.createLoggedInUser(_fixture, user, password)).Dispose());
                }
            }
        }

        [Fact]
        public async void TestLogout() {
            using (var http_client_helper = await HttpClientHelper.createLoggedInAdmin(_fixture))
            {
                var logout_url = new Uri(_fixture.SiteUri, "Identity/Account/Logout");

                using(var response = await http_client_helper.Client.GetAsync(logout_url)) {
                    response.EnsureSuccessStatusCode();
                    Assert.Equal(new Uri(_fixture.SiteUri, "Identity/Account/Login?ReturnUrl=%2FIdentity%2FAccount%2FManage"), response.RequestMessage.RequestUri);
                }
            }
        }

        [InlineData("True", "MyAnafi", "000000")]
        [InlineData("False", "MyAnafi", null)]
        [InlineData("False", null, null)]
        [InlineData("False", null, "000000")]
        [Theory]
        public async void TestCreateAndDeleteDevice(string is_valid, string device_name, string device_serial)
        {
            using (var helper = await HttpClientHelper.createDevice(_fixture, device_name, device_serial, "admin", _fixture.AdminPassword)) { }
            if (is_valid == "True")
            {
                (await HttpClientHelper.deleteDevice(_fixture, device_serial)).Dispose();
                var devices_list = await HttpClientHelper.getDeviceList(_fixture);
                Assert.ThrowsAny<ArgumentNullException>(() => devices_list.First(d => d.serialNumber == device_serial));
            }
        }

        [Fact]
        public async void TestConnectionClosedOnNoSerial()
        {
            using (var ws_transport = new TWebSocketClient(_fixture.ThriftUri, System.Net.WebSockets.WebSocketMessageType.Text))
            {
                await Assert.ThrowsAsync<System.Net.WebSockets.WebSocketException>(async () => await ws_transport.OpenAsync());
            }
        }

        [InlineData("ASerial", false, true)]
        [InlineData(null, true, false)]
        [Theory]
        public async void TestConnectionClosedOnInvalidSerial(string serial_field, bool expect_throw, bool create_delete_device)
        {
            if (create_delete_device)
            {
                using (var helper = await HttpClientHelper.createDevice(_fixture, "SomeName", serial_field, "admin", _fixture.AdminPassword)){ }
            }
            using (var ws_transport = new TWebSocketClient(_fixture.ThriftUri, System.Net.WebSockets.WebSocketMessageType.Text))
            {
                ws_transport.WebSocketOptions.SetRequestHeader("Content-Type", "application/x-thrift");
                if (serial_field != null)
                    ws_transport.WebSocketOptions.SetRequestHeader("x-device-expected-serial", serial_field);
                if (expect_throw)
                    await Assert.ThrowsAsync<System.Net.WebSockets.WebSocketException>(async () => await ws_transport.OpenAsync());
                else
                    await ws_transport.OpenAsync();
            }
            if (create_delete_device)
            {
                (await HttpClientHelper.deleteDevice(_fixture, serial_field)).Dispose();
            }
        }

        [InlineData("ThriftSerial")]
        [Theory]
        public async void TestThriftDrone(string device_serial)
        {
            System.Net.Cookie cookie = null;
            using (var helper = await HttpClientHelper.createDevice(_fixture, "SomeName", device_serial, "admin", _fixture.AdminPassword))
            {
                cookie = helper.loginCookie;
            }
            try
            {
                HubConnection connection = new HubConnectionBuilder()
                    .WithUrl(new Uri("ws://localhost:5000/telemetryhub"), options => { options.Cookies.Add(cookie);})
                    .Build();

                var task_sources = new Dictionary<Type, TaskCompletionSource<bool>>();
                var types = new Type[] {
                    typeof(DronePosition),
                    typeof(DroneBatteryLevel),
                    typeof(DroneRadioSignal),
                    typeof(DroneFlyingState),
                    typeof(DroneReply),
                    typeof(DroneLiveVideoStateResult)
                };

                foreach (var type in types)
                {
                    task_sources[type] = new TaskCompletionSource<bool>();
                    connection.On<string>(type.FullName, (message) => { task_sources[type].TrySetResult(true); });
                }

                DroneDeviceHelper.DroneTestDelegate del = async () =>
                {
                    var tasks = task_sources.Values.Select(tcss => tcss.Task);
                    await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(4000)); //Because the ping reply can come later
                    var completed_results = tasks
                        .Where(t => t.Status == TaskStatus.RanToCompletion)
                        .Select(t => t.Result)
                        .ToList();
                    Assert.Equal(task_sources.Count, completed_results.Count);
                };

                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var reply_seed = new DroneReply{ Result = true, Serial = device_serial, Timestamp = timestamp};
                var position_seed = new DronePosition { Longitude = 0.0f, Latitude = 0.1f, Altitude = 10f, Serial = device_serial, Timestamp = timestamp };
                var radio_signal_seed = new DroneRadioSignal { SignalQuality = 2, Rssi = -23.0f, Serial = device_serial, Timestamp = timestamp };
                var flying_state_seed = new DroneFlyingState { State = FlyingState.LANDED, Serial = device_serial, Timestamp = timestamp };
                var battery_level_seed = new DroneBatteryLevel { BatteryLevelPercent = 100, Serial = device_serial, Timestamp = timestamp };
                var drone_video_state_seed = new DroneLiveVideoStateResult {State = DroneLiveVideoState.LIVE, Serial = device_serial, Timestamp = timestamp };

                await connection.StartAsync();
                using (var drone_rpc = new DroneRPC())
                {
                    drone_rpc.PingServiceReply.Add(reply_seed);
                    drone_rpc.PositionReply.Add(position_seed);
                    drone_rpc.RadioSignalReply.Add(radio_signal_seed);
                    drone_rpc.FlyingStateReply.Add(flying_state_seed);
                    drone_rpc.BatteryLevelReply.Add(battery_level_seed);
                    drone_rpc.VideoStateResultReply.Add(drone_video_state_seed);

                    await DroneDeviceHelper.mockDrone(_fixture, drone_rpc, device_serial, del);
                    var reply_result = await HttpClientHelper.getDeviceTelemetry<DroneReply>(_fixture, device_serial, 1, 10);
                    var positions_result = await HttpClientHelper.getDeviceTelemetry<DronePosition>(_fixture, device_serial, 1, 10);
                    var radio_signals_result = await HttpClientHelper.getDeviceTelemetry<DroneRadioSignal>(_fixture, device_serial, 1, 10);
                    var flying_states_result = await HttpClientHelper.getDeviceTelemetry<DroneFlyingState>(_fixture, device_serial, 1, 10);
                    var battery_levels_result = await HttpClientHelper.getDeviceTelemetry<DroneBatteryLevel>(_fixture, device_serial, 1, 10);
                    var drone_video_states_result = await HttpClientHelper.getDeviceTelemetry<DroneLiveVideoStateResult>(_fixture, device_serial, 1, 10);

                    reply_result.Single(s => s.Timestamp == reply_seed.Timestamp);
                    positions_result.Single(s => s.Timestamp == position_seed.Timestamp);
                    radio_signals_result.Single(s => s.Timestamp == radio_signal_seed.Timestamp);
                    flying_states_result.Single(s => s.Timestamp == flying_state_seed.Timestamp);
                    battery_levels_result.Single(s => s.Timestamp == battery_level_seed.Timestamp);
                    drone_video_states_result.Single(s => s.Timestamp == drone_video_state_seed.Timestamp);

                    foreach (var type in types)
                    {
                        // Type telemetry_list_type = typeof(List<>).MakeGenericType(type);
                        // var get_device_telemetry = typeof(HttpClientHelper).GetMethod("getDeviceTelemetry");
                        // var get_telemetry_t_method = get_device_telemetry.MakeGenericMethod(telemetry_list_type);
                        // get_telemetry_t_method.Invoke(null, new object[] {_fixture, device_serial, 1, 10});
                    }
                    // drone_positions.First(p => p.)
                }
            }
            finally
            {
                (await HttpClientHelper.deleteDevice(_fixture, device_serial)).Dispose();
            }
        }
    }
}
