using System;
using Xunit;
using DroHub.Tests.TestInfrastructure;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
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

        [InlineData("guest@drohub.xyz", "1234567", "UN", "ActingGuest", 10, 3, false, true, false, false)]
        [InlineData("guest", "1" , "UN", "ActingGuest", 10, 3, true, true, true, true)]
        [Theory]
        public async void TestUserCreateAndLogin(string user, string password, string organization, string user_base_type,
            int allowed_flight_time_minutes, int allowed_user_count, bool expect_login_fail, bool create =false,
                bool expect_create_fail = false, bool expect_delete_fail = false)
        {
            if(create) {
                if (!expect_create_fail)
                    await HttpClientHelper.addUser(_fixture, user, password, organization, user_base_type, allowed_flight_time_minutes, allowed_user_count);
                else
                    await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                        await HttpClientHelper.addUser(_fixture, user,  password, organization,
                            user_base_type, allowed_flight_time_minutes, allowed_user_count)
                    );
            }
            try {
                await testLogin(user, password, expect_login_fail);
            }
            finally {
                if (create)
                {
                    if (!expect_delete_fail)
                    {
                        await HttpClientHelper.deleteUser(_fixture, user, password);
                    }
                    else
                        await Assert.ThrowsAsync<System.InvalidProgramException>(async () => await HttpClientHelper.deleteUser(_fixture, user, password));

                    await Assert.ThrowsAsync<System.InvalidProgramException>(async () => (await HttpClientHelper.createLoggedInUser(_fixture, user, password)).Dispose());
                }
            }
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
            try {
                await HttpClientHelper.addUser(_fixture, DEFAULT_USER, DEFAULT_PASSWORD,
                    DEFAULT_ORGANIZATION, user_base_type, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

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
            finally {
                await HttpClientHelper.deleteUser(_fixture, DEFAULT_USER, DEFAULT_PASSWORD);
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

            try
            {
                await HttpClientHelper.addUser(_fixture, user, password,
                    DEFAULT_ORGANIZATION, user_base_type, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

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

                await HttpClientHelper.deleteUser(_fixture, user, password);
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

        [InlineData("admin", DroHubUser.ADMIN_POLICY_CLAIM, 999, false, true, true, true)]
        // We cannot run a test to validate empty headers because stupid SetRequestHeader always sets an empty space
        [InlineData("sub2@drohub.xyz", DroHubUser.SUBSCRIBER_POLICY_CLAIM, 999, true, true, false, false)]
        [InlineData("sub2@drohub.xyz", DroHubUser.SUBSCRIBER_POLICY_CLAIM, 999, true, false)]
        [Theory]
        public async void TestWebSocketConnection(string user, string user_base_type,
            int allowed_flight_time_minutes, bool expect_throw, bool create_delete_device,
            bool create_user_same_as_websocket = false, bool create_user_organization_same_as_websocket_user = false) {

            var password = DEFAULT_PASSWORD;
            if (user == "admin")
                password = _fixture.AdminPassword;

            var create_device_user = create_user_same_as_websocket ? user : "subscriber@drohub.xyz";
            var create_device_pass = create_user_same_as_websocket ? password : "subscriber@drohub.xyz";

            if (create_delete_device) {
                await HttpClientHelper.addUser(_fixture, create_device_user, create_device_pass,
                    DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE, allowed_flight_time_minutes, ALLOWED_USER_COUNT);

                await HttpClientHelper.createDevice(_fixture, create_device_user, create_device_pass,
                    DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);

                if (!create_user_same_as_websocket) {
                    await HttpClientHelper.addUser(_fixture, user, password,
                        DEFAULT_ORGANIZATION+create_user_organization_same_as_websocket_user,
                        user_base_type, allowed_flight_time_minutes, ALLOWED_USER_COUNT);
                }
            }

            var token = (await HttpClientHelper.getApplicationToken(_fixture, user, password))["result"];

            try {
                if (expect_throw)
                    await Assert.ThrowsAsync<System.Net.WebSockets.WebSocketException>(async () =>
                        await HttpClientHelper.openWebSocket(_fixture, user, token, DEFAULT_DEVICE_SERIAL));
                else
                    await HttpClientHelper.openWebSocket(_fixture, user, token, DEFAULT_DEVICE_SERIAL);
            }
            finally
            {
                if (create_delete_device)
                {
                    await HttpClientHelper.deleteDevice(_fixture, DEFAULT_DEVICE_SERIAL, create_device_user, create_device_pass);
                    await HttpClientHelper.deleteUser(_fixture, create_device_user, create_device_pass);
                    if (!create_user_same_as_websocket) {
                        await HttpClientHelper.deleteUser(_fixture, user, password);
                    }
                }
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
