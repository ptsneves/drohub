using System;
using Xunit;
using DroHub.Tests.TestInfrastructure;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using DroHub.Areas.Identity.Data;
using DroHub.Helpers.Thrift;

// ReSharper disable StringLiteralTypo

namespace DroHub.Tests
{
    public class IntegrationTest : IClassFixture<DroHubFixture>
    {
        DroHubFixture _fixture;

        private const int ALLOWED_USER_COUNT = 999;
        private const string DEFAULT_ORGANIZATION = "UN";
        private const string DEFAULT_DEVICE_NAME = "A Name";
        private const string DEFAULT_BASE_TYPE = DroHubUser.SUBSCRIBER_POLICY_CLAIM;
        private const string DEFAULT_DEVICE_SERIAL = "Aserial";
        private const string DEFAULT_USER = "auser@drohub.xyz";
        private const string DEFAULT_PASSWORD = "password1234";
        private const int DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES = 3;
        private const int DEFAULT_ALLOWED_USER_COUNT = 3;

        public IntegrationTest(DroHubFixture fixture) {
            _fixture = fixture;
        }

        [Fact]
        public async void TestLoginIsNotHomePageAndAllowsAnonymous() {
            using (var http_helper = await HttpClientHelper.createHttpClient(_fixture, _fixture.SiteUri))
                Assert.NotEqual(new Uri(_fixture.SiteUri, "Identity/Account/Login?ReturnUrl=%2FIdentity%2FAccount%2FManage"),
                    http_helper.Response.RequestMessage.RequestUri);
        }

        private async Task testLogin(string user, string password, bool expect_login_fail) {
            if (expect_login_fail)
                await Assert.ThrowsAsync<System.InvalidProgramException>(async () => (await HttpClientHelper.createLoggedInUser(_fixture, user, password)).Dispose());
            else
                using (var http_client_helper = await HttpClientHelper.createLoggedInUser(_fixture, user, password)) { }
        }

        [InlineData(null, false)]
        [InlineData("1", true)]
        [Theory]
        public async void TestAdminAccount(string password, bool expect_login_fail) {
            await testLogin("admin", password ?? _fixture.AdminPassword, expect_login_fail);
        }

        [Fact]
        public async void TestLogout() {
            using (var http_client_helper = await HttpClientHelper.createLoggedInUser(_fixture, "admin", _fixture.AdminPassword))
            {
                var logout_url = new Uri(_fixture.SiteUri, "Identity/Account/Logout");

                using(var response = await http_client_helper.Client.GetAsync(logout_url)) {
                    response.EnsureSuccessStatusCode();
                    Assert.Equal(new Uri(_fixture.SiteUri, "/"), response.RequestMessage.RequestUri);
                }
            }
        }

        // [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, true)]
        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM, true)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM, true)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM, true)]
        [InlineData(DroHubUser.GUEST_POLICY_CLAIM, false)]
        [Theory]
        public async void TestAuthenticationToken(string user_base_type, bool expect_get_token_success) {
            await using var add_user = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, user_base_type,
                DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

            var res = await HttpClientHelper.getApplicationToken(_fixture, DEFAULT_USER, DEFAULT_PASSWORD);
            Assert.NotEmpty(res["result"]);
            if (expect_get_token_success) {
                Assert.NotEqual("nok", res["result"]);
                var token = res["result"];
                res = await HttpClientHelper.authenticateToken(_fixture, DEFAULT_USER, token);
                Assert.Equal("ok", res["result"]);
            }
            else {
                Assert.Equal("nok", res["result"]);
            }
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
        public async void TestQueryDeviceInfoOnNonExistingUser() {
            var device_info = await HttpClientHelper.queryDeviceInfo(_fixture, "asd",
                "sadsdd",
                DEFAULT_DEVICE_SERIAL);
            Assert.Null(device_info["result"]);
        }

        [InlineData("admin", null, "MyAnafi", "000000", true, true)]
        [InlineData("admin", null, "MyAnafi", "000000", true)]
        [InlineData("admin", null, "MyAnafi", null, false)]
        [InlineData("admin", null, null, null, false)]
        [InlineData("admin", null, null, "000000", false)]
        [InlineData("user@drohub.xyz", DroHubUser.SUBSCRIBER_POLICY_CLAIM, "MyAnafi", "000000", true)]
        [InlineData("user@drohub.xyz", DroHubUser.OWNER_POLICY_CLAIM, "MyAnafi", "000000", true)]
        [InlineData("user@drohub.xyz", DroHubUser.PILOT_POLICY_CLAIM, "MyAnafi", "000000", true)]
        [InlineData("user@drohub.xyz", DroHubUser.GUEST_POLICY_CLAIM, "MyAnafi", "000000", false)]
        [InlineData("user@drohub.xyz", DroHubUser.SUBSCRIBER_POLICY_CLAIM, "MyAnafi", "000000", true, true)]
        [InlineData("user@drohub.xyz", DroHubUser.OWNER_POLICY_CLAIM, "MyAnafi", "000000", true, true)]
        [InlineData("user@drohub.xyz", DroHubUser.PILOT_POLICY_CLAIM, "MyAnafi", "000000", true, true)]
        [InlineData("user@drohub.xyz", DroHubUser.GUEST_POLICY_CLAIM, "MyAnafi", "000000", false, true)]
        [Theory]
        public async void TestCreateAndDeleteDevice(string user, string user_base_type,
            string device_name, string device_serial, bool expect_created, bool use_app_api = false) {

            var create_user = true;

            var password = "default";

            if (user == "admin") {
                password = _fixture.AdminPassword;
            }

            await using var user_add = await HttpClientHelper.AddUserHelper.addUser(_fixture, user, password,
                DEFAULT_ORGANIZATION, user_base_type, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);
            try {
                if (expect_created) {

                    await HttpClientHelper.createDevice(_fixture, user, password, device_name, device_serial,
                        use_app_api);

                    var devices_list = await HttpClientHelper.getDeviceList(_fixture, user, password);
                    Assert.NotNull(devices_list);
                    devices_list.Single(d => d.serialNumber == device_serial);
                    var token = (await HttpClientHelper.getApplicationToken(_fixture, user,
                        password))["result"];
                    var device_info = (await HttpClientHelper.queryDeviceInfo(_fixture, user, token,
                        device_serial));
                    Assert.Equal(device_name, device_info["result"].Name);
                    Assert.Equal(device_serial, device_info["result"].SerialNumber);
                }
                else
                {
                    if (use_app_api) {
                        await Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(async () => {
                            await HttpClientHelper.createDevice(_fixture, user, password, device_name, device_serial,
                                true);
                        });
                    }
                    else {
                        await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                            await HttpClientHelper.createDevice(_fixture, user, password, device_name, device_serial);
                        });
                    }

                    Assert.Null(await HttpClientHelper.getDeviceList(_fixture, user, password));
                }
            }
            finally {
                if (expect_created)
                    await HttpClientHelper.deleteDevice(_fixture, device_serial, user, password);

                var devices_list = await HttpClientHelper.getDeviceList(_fixture,  user, password);
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
            await HttpClientHelper.createDevice(_fixture, "admin", _fixture.AdminPassword,
                DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);


            await using var extra_user = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION,
                DroHubUser.SUBSCRIBER_POLICY_CLAIM, 10, 10);

            var token = (await HttpClientHelper.getApplicationToken(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD))["result"];

            await Assert.ThrowsAsync<WebSocketException>(async () =>
                await HttpClientHelper.openWebSocket(_fixture, DEFAULT_USER, token, DEFAULT_DEVICE_SERIAL));

            await HttpClientHelper.deleteDevice(_fixture, DEFAULT_DEVICE_SERIAL, "admin", _fixture.AdminPassword);
        }

        [Fact]
        public async void TestWebSocketWithDeviceBelongingToSubscriptionSucceeds() {
            await HttpClientHelper.createDevice(_fixture, "admin", _fixture.AdminPassword,
                DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);


            await using var extra_user = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER, DEFAULT_PASSWORD,
                "Administrators", DroHubUser.SUBSCRIBER_POLICY_CLAIM, 10,
                10);

            var token = (await HttpClientHelper.getApplicationToken(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD))["result"];

            await HttpClientHelper.openWebSocket(_fixture, DEFAULT_USER, token, DEFAULT_DEVICE_SERIAL);

            await HttpClientHelper.deleteDevice(_fixture, DEFAULT_DEVICE_SERIAL, DEFAULT_USER, DEFAULT_PASSWORD);
        }

        [Fact]
        public async void TestThriftConnectionDroppedAtTimeout() {
            await using var extra_user = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES,
                ALLOWED_USER_COUNT);

            await HttpClientHelper.createDevice(_fixture, DEFAULT_USER, DEFAULT_PASSWORD,
                DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);

            try {
                var token = (await HttpClientHelper.getApplicationToken(_fixture, DEFAULT_USER, DEFAULT_PASSWORD))["result"];
                using var t_web_socket_client = HttpClientHelper.getTWebSocketClient(_fixture, DEFAULT_USER, token,
                    DEFAULT_DEVICE_SERIAL);

                await t_web_socket_client.OpenAsync();
                Assert.True(t_web_socket_client.IsOpen);
                await Task.Delay(DroneMicroServiceManager.ConnectionTimeout + TimeSpan.FromSeconds(1));

                //For some reason the first one does not throw broken pipe. Probably some stupid internal in WebSocket.
                //All the library is crap.
                await t_web_socket_client.WriteAsync(new byte[1]);

                await Assert.ThrowsAsync<System.IO.IOException>(async () =>
                    await t_web_socket_client.WriteAsync(new byte[1]));
            }
            finally {
                await HttpClientHelper.deleteDevice(_fixture, DEFAULT_DEVICE_SERIAL, DEFAULT_USER, DEFAULT_PASSWORD);
            }
        }

        [InlineData("ThriftSerial1", "mysuser@drohub.com", "pass12345", true, 1, false)]
        [Theory]
        public async void TestThriftDrone(string device_serial, string user, string password,
                bool create_users, int concurrent_devices, bool single_user_multiple_devices)
        {
            var telemetry_mocks = new List<TelemetryMock>();
            var drone_rpcs = new List<DroneRPC>();
            var users = new List<string>();
            for (var i = 0; i < concurrent_devices; i++) {
                telemetry_mocks.Add(new TelemetryMock($"{device_serial}_{i}"));
                users.Add(single_user_multiple_devices ? user : $"{user}{i}");

                await telemetry_mocks[i].startMock(_fixture, users[i], password, "org",
                    "ActingPilot", 3, 3,
                    "ws://localhost:5000/telemetryhub", create_users, true );

                drone_rpcs.Add(new DroneRPC(telemetry_mocks[i]));
            }
            try
            {
                for (var i = 0; i < concurrent_devices; i++) {
                    var token = (await HttpClientHelper.getApplicationToken(_fixture, users[i],
                        password))["result"];

                    await DroneDeviceHelper.mockDrone(_fixture, drone_rpcs[i], telemetry_mocks[i].SerialNumber,
                        telemetry_mocks[i].WaitForServer,
                            users[i], token);
                }

                for (var i = 0; i < concurrent_devices; i++)
                {
                    Assert.Equal(telemetry_mocks[i].TelemetryItems.Count, telemetry_mocks[i].getSignalRTasksTelemetry());
                    await telemetry_mocks[i].verifyRecordedTelemetry(_fixture);
                }
            }
            finally
            {
                for (var i = 0; i < concurrent_devices; i++)
                {
                    drone_rpcs[i].Dispose();
                    await telemetry_mocks[i].stopMock();
                }
            }
        }
    }
}
