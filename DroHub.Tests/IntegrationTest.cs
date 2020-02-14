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

        [InlineData(true, "MyAnafi", "000000")]
        [InlineData(false, "MyAnafi", null)]
        [InlineData(false, null, null)]
        [InlineData(false, null, "000000")]
        [Theory]
        public async void TestCreateAndDeleteDevice(bool expect_created, string device_name, string device_serial)
        {
            try
            {
                if (expect_created)
                {
                    using (var helper = await HttpClientHelper.createDevice(_fixture, device_name, device_serial, "admin", _fixture.AdminPassword)) { }
                    var devices_list = await HttpClientHelper.getDeviceList(_fixture);
                    Assert.NotNull(devices_list);
                    devices_list.Single(d => d.serialNumber == device_serial);
                }
                else
                {
                    await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                    {
                        using (var helper = await HttpClientHelper.createDevice(_fixture, device_name, device_serial, "admin", _fixture.AdminPassword)) { }
                    });
                    Assert.Null(await HttpClientHelper.getDeviceList(_fixture));
                }
            }
            finally {
                if (expect_created)
                    (await HttpClientHelper.deleteDevice(_fixture, device_serial)).Dispose();
                else
                {
                    if (device_serial != null && device_name != null)
                        (await HttpClientHelper.deleteDevice(_fixture, device_serial)).Dispose();
                }

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

        [InlineData("ThriftSerial1", "mysuser", "pass", true)]
        [Theory]
        public async void TestThriftDrone(string device_serial, string user, string password, bool create_user)
        {
            TelemetryMock telemetry_mock = new TelemetryMock(device_serial);

            await telemetry_mock.startMock(_fixture, "ws://localhost:5000/telemetryhub", user, password, create_user, true);
            DroneDeviceHelper.DroneTestDelegate del = async () =>
            {
                var tasks = telemetry_mock.TelemetryItems.Select(item => ((TelemetryMock.BaseTelemetryItem)item.Value).TaskSource.Task);
                await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(4000));
            };

            using (var drone_rpc = new DroneRPC(telemetry_mock))
            {
                await DroneDeviceHelper.mockDrone(_fixture, drone_rpc, device_serial, del);
                Assert.Equal(telemetry_mock.TelemetryItems.Count, telemetry_mock.getSignalRTasksTelemetry());
                await telemetry_mock.verifyRecordedTelemetry(_fixture);
            }
            await telemetry_mock.stopMock();
        }
    }
}
