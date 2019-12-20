using System;
using Xunit;
using DroHub.Tests.TestInfrastructure;
using System.Threading.Tasks;
using System.Linq;
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

        [InlineData("admin", null)]
        [InlineData("admin", "1")]
        [Theory]
        public async void TestLogin(string user, string password)
        {
            if (user == "admin" && password == null)
            {
                password = _fixture.AdminPassword;
                using (var http_client_helper = await HttpClientHelper.createLoggedInUser(_fixture, user, password)) { }
            }
            else
            {
                await Assert.ThrowsAsync<System.InvalidProgramException>(async () =>
                {
                    using (var http_client_helper = await HttpClientHelper.createLoggedInUser(_fixture, user, password)) { }
                });
            }
        }

        [InlineData("True", "MyAnafi", "000000")]
        [InlineData("False", "MyAnafi", null)]
        [InlineData("False", null, null)]
        [InlineData("False", null, "000000")]
        [Theory]
        public async void TestCreateAndDeleteDevice(string is_valid, string device_name, string device_serial)
        {
            using (var helper = await HttpClientHelper.createDevice(_fixture, device_name, device_serial))
            {
                var dom = DroHubFixture.getHtmlDOM(await helper.Response.Content.ReadAsStringAsync());
                Assert.Equal(is_valid, dom.QuerySelectorAll("input[name='IsValid']").First().GetAttribute("value"));
            }
            if (is_valid == "True")
            {
                var devices_list = await HttpClientHelper.getDeviceList(_fixture);
                int device_id = devices_list.First(d => d.serialNumber == device_serial).id;
                using (var helper = await HttpClientHelper.deleteDevice(_fixture, device_id))
                {
                    ;
                }
            }
        }


        [Fact]
        public async void TestConnectionClosedOnNoSerial()
        {
            using (var ws_transport = new TWebSocketClient(_fixture.ThriftUri, System.Net.WebSockets.WebSocketMessageType.Text))
            {
                await Assert.ThrowsAsync<System.Net.WebSockets.WebSocketException>(async () => await ws_transport.OpenAsync());
                await Task.Delay(5000);
            }
        }

        [InlineData("", true)]
        [InlineData("NonexistingSerial", true)]
        [InlineData("000000", false)]
        [Theory]
        public async void TestConnectionClosedOnInvalidSerial(string serial_field, bool expect_throw)
        {
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
        }
    }
}
