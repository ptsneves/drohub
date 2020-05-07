using System;
using Xunit;
using DroHub.Tests.TestInfrastructure;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Threading;
using DroHub.Areas.Identity.Data;
using DroHub.Helpers;

// ReSharper disable StringLiteralTypo

namespace DroHub.Tests
{
    public class IntegrationTest : IClassFixture<DroHubFixture>
    {
        private readonly DroHubFixture _fixture;

        private const int ALLOWED_USER_COUNT = 999;
        private const string DEFAULT_ORGANIZATION = "UN";
        private const string DEFAULT_DEVICE_NAME = "A Name";
        private const string DEFAULT_BASE_TYPE = DroHubUser.SUBSCRIBER_POLICY_CLAIM;
        private const string DEFAULT_DEVICE_SERIAL = "Aserial";
        private const string DEFAULT_USER = "auser@drohub.xyz";
        private const string DEFAULT_PASSWORD = "password1234";
        private const int DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES = 999;
        private const int DEFAULT_ALLOWED_USER_COUNT = 3;

        public IntegrationTest(DroHubFixture fixture) {
            _fixture = fixture;
        }

        [Fact]
        public async void TestLoginIsNotHomePageAndAllowsAnonymous() {
            using var http_helper = await HttpClientHelper.createHttpClient(_fixture, _fixture.SiteUri);
            Assert.NotEqual(new Uri(_fixture.SiteUri, "Identity/Account/Login?ReturnUrl=%2FIdentity%2FAccount%2FManage"),
                http_helper.Response.RequestMessage.RequestUri);
        }

        [InlineData("DHub/DeviceRepository/Dashboard")]
        [InlineData("Identity/Account/Manage")]
        [InlineData("Identity/Account/Manage/AdminPanel")]
        [Theory]
        public async void TestPageRedirectedToLogin(string uri_path) {
            using var http_helper = await HttpClientHelper.createHttpClient(_fixture,
                new Uri(_fixture.SiteUri + uri_path));
            Assert.NotEqual(new Uri(_fixture.SiteUri, uri_path),
                http_helper.Response.RequestMessage.RequestUri);
        }

        private async Task testLogin(string user, string password, bool expect_login_fail) {
            if (expect_login_fail)
                await Assert.ThrowsAsync<InvalidProgramException>(async () => (await HttpClientHelper.createLoggedInUser(_fixture, user, password)).Dispose());
            else
                using (await HttpClientHelper.createLoggedInUser(_fixture, user, password)) { }
        }

        [InlineData(null, false)]
        [InlineData("1", true)]
        [Theory]
        public async void TestAdminAccount(string password, bool expect_login_fail) {
            await testLogin("admin", password ?? _fixture.AdminPassword, expect_login_fail);
        }

        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, false, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, false, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, false, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, false, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, false, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, true, false)]
        [Theory]
        public async void TestCreateUserSimple(string user_base_role, string new_user_role, bool same_org, bool expect_success) {
            await using var agent_user = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, user_base_role,
                DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

            var new_user_org = same_org ? DEFAULT_ORGANIZATION : DEFAULT_ORGANIZATION + "1";
            var t = HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD, DEFAULT_USER+"1", DEFAULT_PASSWORD,
                DEFAULT_ORGANIZATION, new_user_role, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

            if (expect_success) {
                await using var u = await t;
            }
            else {
                await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                    await using var u = await t;
                });
            }
        }

        [Fact]
        public async void TestCreateSubscriptionAllowedMinutesLimits() {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await using var u1 = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                    DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE, (long)TimeSpan.MaxValue.TotalMinutes+1,
                    DEFAULT_ALLOWED_USER_COUNT);
            });

            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await using var u1 = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                    DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE, 0,
                    DEFAULT_ALLOWED_USER_COUNT);
            });

            await using var u = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE, (long)TimeSpan.MaxValue.TotalMinutes,
                DEFAULT_ALLOWED_USER_COUNT);

        }

        [Fact]
        public async void TestCreateSubscriptionWithNoAllowedUsers() {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await using var u = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                    DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 0);
            });
        }

        [Fact]
        public async void TestUpdateSubscriptionOnlyOnUserDelete() {
            {
                await using var u1 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                    DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                    DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 3);
                await using var u2 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                    DEFAULT_USER+"1", DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                    DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 1);
                await using var u3 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                    DEFAULT_USER+"2", DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                    DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 3);
            }
            {
                await using var u1 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                    DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                    DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 1);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                    await using var u2 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                        DEFAULT_USER+"1", DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                        DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 1);
                });
            }
        }

        [Fact]
        public async void TestCreateUserCountLimit() {
            await using var u1 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 2);

            await using var u2 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_USER+"1",
                DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 2);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await using var u3 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                    DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_USER+"2",
                    DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                    DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 2);
            });
        }

        [Fact]
        public async void TestDoubleUserCreationFails() {
            await using var u1 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await using var u2 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                    DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                    DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);
            });
        }

        [Fact]
        public async void TestLogout() {
            using var http_client_helper = await HttpClientHelper.createLoggedInUser(_fixture, "admin", _fixture.AdminPassword);
            var logout_url = new Uri(_fixture.SiteUri, "Identity/Account/Logout");

            using var response = await http_client_helper.Client.GetAsync(logout_url);
            response.EnsureSuccessStatusCode();
            Assert.Equal(new Uri(_fixture.SiteUri, "/"), response.RequestMessage.RequestUri);
        }

        [Fact]
        public async void TestQueryDeviceInfoIsEmpty() {
            var token = (await HttpClientHelper.getApplicationToken(_fixture, "admin",
                _fixture.AdminPassword))["result"];
            var device_info = await HttpClientHelper.queryDeviceInfo(_fixture, "admin", token,
                DEFAULT_DEVICE_SERIAL);
            Assert.Null(device_info["result"]);
        }

        [Fact]
        public async void TestCreateExistingDeviceFails() {
            await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, "admin",
                _fixture.AdminPassword, DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);

            await using var s = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER, DEFAULT_PASSWORD,
            DEFAULT_ORGANIZATION,
                DEFAULT_BASE_TYPE, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

            await Assert.ThrowsAsync<InvalidDataException>(async () => {
                await using var f = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, DEFAULT_USER,
                    DEFAULT_PASSWORD, DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);
            });
        }

        [Fact]
        public async void TestQueryDeviceInfoOnNonExistingUser() {
            await Assert.ThrowsAsync<HttpRequestException>(async () => {
                await HttpClientHelper.queryDeviceInfo(_fixture, "asd",
                    "sadsdd",
                    DEFAULT_DEVICE_SERIAL);
            });
        }

        [InlineData("MyAnafi", null)]
        [InlineData(null, null)]
        [Theory]
        public async void TestIncompleteCreateDeviceModelFails(string device_name, string device_serial) {
                await Assert.ThrowsAsync<HttpRequestException>(async () => {
                    await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, "admin",
                        _fixture.AdminPassword, device_name, device_serial);
                });
        }

        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM, "MyAnafi", "000000", true)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM, "MyAnafi", "000000", true)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM, "MyAnafi", "000000", true)]
        [InlineData(DroHubUser.GUEST_POLICY_CLAIM, "MyAnafi", "000000", false)]
        [Theory]
        public async void TestCreateAndDeleteDevicePermission(string user_base_type,
            string device_name, string device_serial, bool expect_created) {
            await using var user_add = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD,
                DEFAULT_ORGANIZATION, user_base_type, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES,
                DEFAULT_ALLOWED_USER_COUNT);
            {
                if (expect_created) {
                    await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, DEFAULT_USER,
                        DEFAULT_PASSWORD, device_name, device_serial);

                    var devices_list = await HttpClientHelper.getDeviceList(_fixture, DEFAULT_USER, DEFAULT_PASSWORD);
                    Assert.NotNull(devices_list);
                    devices_list.Single(ds => ds.serialNumber == device_serial);
                    var token = (await HttpClientHelper.getApplicationToken(_fixture, DEFAULT_USER,
                        DEFAULT_PASSWORD))["result"];
                    var device_info = (await HttpClientHelper.queryDeviceInfo(_fixture, DEFAULT_USER, token,
                        device_serial));
                    Assert.Equal(device_name, device_info["result"].Name);
                    Assert.Equal(device_serial, device_info["result"].SerialNumber);
                }
                else {
                    await Assert.ThrowsAsync<InvalidCredentialException>(async () => {
                        await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture,
                            DEFAULT_USER,
                            DEFAULT_PASSWORD, device_name, device_serial);
                    });
                }
            }
            Assert.Null(await HttpClientHelper.getDeviceList(_fixture, DEFAULT_USER, DEFAULT_PASSWORD));
        }

        [Fact]
        public async void TestConnectionClosedOnNoSerial() {
            using var ws_transport = new TWebSocketClient(_fixture.ThriftUri, WebSocketMessageType.Text);
            await Assert.ThrowsAsync<WebSocketException>(async () => await ws_transport.OpenAsync());
        }

        [Fact]
        public async void TestWebSocketWithNonExistingDevice() {
            const string user = "admin";
            var password = _fixture.AdminPassword;
            var token = (await HttpClientHelper.getApplicationToken(_fixture, user, password))["result"];
            await Assert.ThrowsAsync<WebSocketException>(async () =>
                await HttpClientHelper.openWebSocket(_fixture, user, token, DEFAULT_DEVICE_SERIAL));
        }

        [Fact]
        public async void TestWebSocketWithDeviceNotBelongingToSubscriptionFails() {
            await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, "admin",
                _fixture.AdminPassword, DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);

            await using var extra_user = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION,
                DroHubUser.SUBSCRIBER_POLICY_CLAIM, 10, 10);

            var token = (await HttpClientHelper.getApplicationToken(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD))["result"];

            await Assert.ThrowsAsync<WebSocketException>(async () =>
                await HttpClientHelper.openWebSocket(_fixture, DEFAULT_USER, token, DEFAULT_DEVICE_SERIAL));
        }

        [Fact]
        public async void TestWebSocketWithDeviceBelongingToSubscriptionSucceeds() {
            await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, "admin",
                _fixture.AdminPassword, DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);

            await using var extra_user = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER, DEFAULT_PASSWORD,
                "Administrators", DroHubUser.SUBSCRIBER_POLICY_CLAIM, 10,
                10);

            var token = (await HttpClientHelper.getApplicationToken(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD))["result"];

            await HttpClientHelper.openWebSocket(_fixture, DEFAULT_USER, token, DEFAULT_DEVICE_SERIAL);
        }

        [Fact]
        public async void TestWebSocketFailedAuthentication() {
            var exception_occured = false;
            try {
                await HttpClientHelper.openWebSocket(_fixture, DEFAULT_USER, DEFAULT_PASSWORD, _fixture
                    .AdminPassword);
            }
            catch (Exception e) {
                Assert.Equal("The server returned status code '401' when status code '101' was expected.", e.Message);
                exception_occured = true;
            }
            Assert.True(exception_occured, "No exception occurred and should have");
        }

        [Fact]
        public async void TestDeviceFlightStartTime() {
            Assert.Null(await HttpClientHelper.getDeviceFlightStartTime(_fixture, 1, "admin",
                _fixture.AdminPassword));
            await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, "admin",
                _fixture.AdminPassword, DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);

            var token = (await HttpClientHelper.getApplicationToken(_fixture, "admin",
                _fixture.AdminPassword))["result"];

            var device_id = await HttpClientHelper.getDeviceId(_fixture, DEFAULT_DEVICE_SERIAL,
                "admin", _fixture.AdminPassword);
            Assert.Null(await HttpClientHelper.getDeviceFlightStartTime(_fixture, device_id, "admin",
                _fixture.AdminPassword));

            var time_start = DateTime.Now.ToUniversalTime();
            using var f = await HttpClientHelper.openWebSocket(_fixture, "admin", token, DEFAULT_DEVICE_SERIAL);
            await Task.Delay(TimeSpan.FromSeconds(5));
            var received = await HttpClientHelper.getDeviceFlightStartTime(_fixture, device_id, "admin",
                _fixture.AdminPassword);

            Assert.True(received.HasValue);
            var time_diff = time_start - HttpClientHelper.UnixEpoch.AddMilliseconds(received.Value);
            Assert.True(time_diff < TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async void TestThriftConnectionDroppedAtTimeout() {
            await using var extra_user = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES,
                ALLOWED_USER_COUNT);

            await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD, DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);

            var token = (await HttpClientHelper.getApplicationToken(_fixture, DEFAULT_USER, DEFAULT_PASSWORD))["result"];
            using var t_web_socket_client = HttpClientHelper.getTWebSocketClient(_fixture, DEFAULT_USER, token,
                DEFAULT_DEVICE_SERIAL);

            await t_web_socket_client.OpenAsync();
            Assert.True(t_web_socket_client.IsOpen);
            await Task.Delay(DroneMicroServiceManager.ConnectionTimeout + TimeSpan.FromSeconds(1));

            //For some reason the first one does not throw broken pipe. Probably some stupid internal in WebSocket.
            //All the library is crap.
            await t_web_socket_client.WriteAsync(new byte[1]);

            await Assert.ThrowsAsync<IOException>(async () =>
                await t_web_socket_client.WriteAsync(new byte[1]));
        }

        private delegate Task StageThriftDroneTestDelegate(DroneRPC drone_rpc, TelemetryMock telemetry_mock,
            string user_name, string token);

        private async Task stageThriftDrone(bool infinite, int minutes, string user_name, string organization, string
        serial_number, StageThriftDroneTestDelegate del) {
            var telemetry_mock = new TelemetryMock(serial_number);

            var password = (user_name == "admin") ? _fixture.AdminPassword : DEFAULT_PASSWORD;
            await telemetry_mock.startMock(_fixture, user_name, password, organization,
                "ActingPilot", minutes, DEFAULT_ALLOWED_USER_COUNT,
                "ws://localhost:5000/telemetryhub");

            var drone_rpc = new DroneRPC(telemetry_mock, infinite);

            try {
                var token = (await HttpClientHelper.getApplicationToken(_fixture, user_name,
                    password))["result"];
                await del(drone_rpc, telemetry_mock, user_name, token);
            }
            finally {
                drone_rpc.Dispose();
                await telemetry_mock.stopMock();
            }
        }

        [Fact]
        public async void TestThriftDroneDataCorrectness() {
            var tasks = new List<Task>();
            for (var i = 0; i < 100; i++) {
                var t = stageThriftDrone(false, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_USER+i, DEFAULT_ORGANIZATION+i, DEFAULT_DEVICE_SERIAL+i,
                    async (drone_rpc, telemetry_mock, user_name, token) => {
                    await DroneDeviceHelper.mockDrone(_fixture, drone_rpc, telemetry_mock.SerialNumber,
                        telemetry_mock.WaitForServer, user_name, token);
                    foreach (var f in telemetry_mock.getSignalRTasksTelemetry()) {
                        var ds = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(f);
                        Assert.Equal((string) ds.Serial, telemetry_mock.SerialNumber);
                    }

                    var r = await telemetry_mock.getRecordedTelemetry(_fixture);

                    foreach (var key_value_pair in r) {
                        Assert.NotNull(key_value_pair.Value);
                    }
                    });
                tasks.Add(t);
            }

            try {
                await Task.WhenAll(tasks);
            }
            catch (Exception e) {
                var s = "";
                foreach (var task in tasks) {
                    if (task.IsFaulted) {
                        s += $"Exception:  {task.Exception}   \n";
                    }
                }
                throw new InvalidDataException(s);
            }
        }

        [Fact]
        public async void TestSubscriptionEnd() {
            const int minutes = 1;
            var tasks = new List<Task>();
            for (var i = 0; i < 50; i++) {
                var t = stageThriftDrone(true, minutes, DEFAULT_USER+i, DEFAULT_ORGANIZATION+i, DEFAULT_DEVICE_SERIAL+i,
                    async (drone_rpc, telemetry_mock, user_name, token) => {
                        var timer_start = DateTime.Now;
                        await DroneDeviceHelper.mockDrone(_fixture, drone_rpc, telemetry_mock.SerialNumber,
                            async () => {
                                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(minutes + 0.5f * minutes));
                                await drone_rpc.MonitorConnection(TimeSpan.FromSeconds(5), cts.Token);
                            }, user_name, token);
                        var elapsed_time = DateTime.Now - timer_start;
                        if (elapsed_time < TimeSpan.FromMinutes(minutes))
                            throw new InvalidDataException($"{elapsed_time} > {TimeSpan.FromMinutes(minutes)} FAILED");
                        if (!(elapsed_time.TotalMinutes <
                              TimeSpan.FromMinutes(minutes).TotalMinutes + DroneMicroServiceManager.SubscriptionCheckInterval.TotalMinutes))
                            throw new InvalidDataException(
                                $"{elapsed_time.TotalMinutes} < {TimeSpan.FromMinutes(minutes).TotalSeconds + DroneMicroServiceManager.SubscriptionCheckInterval.TotalSeconds} FAILED");

                        await Assert.ThrowsAsync<WebSocketException>(async () =>
                            await HttpClientHelper.openWebSocket(_fixture, telemetry_mock.UserName, token,
                                telemetry_mock.SerialNumber));
                    });
                tasks.Add(t);
            }
            await Task.WhenAll(tasks);
        }

        // ReSharper disable once xUnit1004
        [Fact (Skip = "To be ran manually")]
        public async void TestInterface() {
            const int minutes = 999;
            var tasks = new List<Task>();
            for (var i = 0; i < 1; i++) {
                var t = stageThriftDrone(true, minutes, "admin", "administrators", DEFAULT_DEVICE_SERIAL+i,
                    async (drone_rpc, telemetry_mock, user_name, token) => {
                        await DroneDeviceHelper.mockDrone(_fixture, drone_rpc, telemetry_mock.SerialNumber,
                            async () => {
                                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(minutes + 0.5f * minutes));
                                await drone_rpc.MonitorConnection(TimeSpan.FromSeconds(5), cts.Token);
                            }, user_name, token);
                    });
                tasks.Add(t);
            }
            await Task.WhenAll(tasks);
        }
    }
}
