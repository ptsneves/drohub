using System;
using Xunit;
using DroHub.Tests.TestInfrastructure;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Generic;
using System.Reflection;
using System.Net.Http;
using System.Collections;

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
                    using (var http_client_helper = await HttpClientHelper.addUser(_fixture, user, password,
                        organization, user_base_type, allowed_flight_time_minutes, allowed_user_count)) { }
                else
                    await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                        (await HttpClientHelper.addUser(_fixture, user, password,
                            organization, user_base_type, allowed_flight_time_minutes, allowed_user_count)).Dispose());
            }
            try {
                await testLogin(user, password, expect_login_fail);
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
            using (var http_client_helper = await HttpClientHelper.createLoggedInUser(_fixture, "admin", _fixture.AdminPassword))
            {
                var logout_url = new Uri(_fixture.SiteUri, "Identity/Account/Logout");

                using(var response = await http_client_helper.Client.GetAsync(logout_url)) {
                    response.EnsureSuccessStatusCode();
                    Assert.Equal(new Uri(_fixture.SiteUri, "Identity/Account/Login?ReturnUrl=%2FIdentity%2FAccount%2FManage"), response.RequestMessage.RequestUri);
                }
            }
        }

        [InlineData("admin", null, "MyAnafi", "000000", null, null, null, null, true)]
        [InlineData("admin", null, "MyAnafi", null, null, null, null, null, false)]
        [InlineData("admin", null, null, null, null, null, null, null, false)]
        [InlineData("admin", null, null, "000000",  null, null, null, null, false)]
        [Theory]
        public async void TestCreateAndDeleteDevice(string user, string password,
            string device_name, string device_serial, string organization, string user_base_type,
            int allowed_flight_time_minutes, int allowed_user_count, bool expect_created) {

            if (password == null)
                password = _fixture.AdminPassword;

            try
            {
                if (expect_created)
                {
                    using (var helper = await HttpClientHelper.createDevice(_fixture, user, password,
                        organization, user_base_type, allowed_flight_time_minutes, allowed_user_count, device_name,
                        device_serial, false)) { }
                    var devices_list = await HttpClientHelper.getDeviceList(_fixture, user, password);
                    Assert.NotNull(devices_list);
                    devices_list.Single(d => d.serialNumber == device_serial);
                }
                else
                {
                    await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                    {
                        using (var helper = await HttpClientHelper.createDevice(_fixture, user, password,
                            organization, user_base_type, allowed_flight_time_minutes, allowed_user_count, device_name,
                            device_serial, false)) { }
                    });
                    Assert.Null(await HttpClientHelper.getDeviceList(_fixture, user, password));
                }
            }
            finally {
                if (expect_created)
                    (await HttpClientHelper.deleteDevice(_fixture, device_serial, user, password)).Dispose();
                else
                {
                    if (device_serial != null && device_name != null)
                        (await HttpClientHelper.deleteDevice(_fixture, device_serial, user, password)).Dispose();
                }

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

        [InlineData("admin", null, "ASerial", "AName", null, null, null, null, false, true)]
        [InlineData("admin", null, null, "AName", null, null, null, null, true, false)]
        [Theory]
        public async void TestWebSocketConnection(string user, string password,
            string device_serial, string device_name, string organization, string user_base_type,
            int allowed_flight_time_minutes, int allowed_user_count, bool expect_throw, bool create_delete_device)
        {
            if (password == null)
                password = _fixture.AdminPassword;

            if (create_delete_device)
            {
                using (var helper = await HttpClientHelper.createDevice(_fixture, user, password, organization, user_base_type,
                    allowed_flight_time_minutes, allowed_user_count, device_name, device_serial)){ }
            }
            try
            {
                using (var ws_transport = new TWebSocketClient(_fixture.ThriftUri, System.Net.WebSockets.WebSocketMessageType.Text))
                {
                    ws_transport.WebSocketOptions.SetRequestHeader("Content-Type", "application/x-thrift");
                    if (device_serial != null)
                    {
                        ws_transport.WebSocketOptions.SetRequestHeader("x-device-expected-serial", device_serial);
                        ws_transport.WebSocketOptions.SetRequestHeader("x-drohub-user", "admin");
                        ws_transport.WebSocketOptions.SetRequestHeader("x-drohub-password", _fixture.AdminPassword);
                    }
                    if (expect_throw)
                        await Assert.ThrowsAsync<System.Net.WebSockets.WebSocketException>(async () => await ws_transport.OpenAsync());
                    else
                        await ws_transport.OpenAsync();
                }
            }
            finally
            {
                if (create_delete_device)
                {
                    (await HttpClientHelper.deleteDevice(_fixture, device_serial, user, password)).Dispose();
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
                    await DroneDeviceHelper.mockDrone(_fixture, drone_rpcs[i], telemetry_mocks[i].SerialNumber, telemetry_mocks[i].WaitForServer,
                            users[i], password);
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