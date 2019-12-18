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
        [InlineData("False", "MyAnafi", "000000")]
        [InlineData("False", "MyAnafi", null)]
        [InlineData("False", null, null)]
        [InlineData("False", null, "000000")]
        [Theory]
        public async void TestCreateDevice(string is_valid, string device_name, string device_serial) {
            using (var http_client_helper = await HttpClientHelper.createLoggedInAdmin(_fixture)) {
                var content = await http_client_helper.Response.Content.ReadAsStringAsync();
                var create_device_url = new Uri(_fixture.SiteUri, "DHub/Devices/Create");
                using (var create_page_response = await http_client_helper.Client.GetAsync(create_device_url)) {
                    create_page_response.EnsureSuccessStatusCode();
                    var verification_token = DroHubFixture.getVerificationToken(await create_page_response.Content.ReadAsStringAsync());
                    var data_dic = new Dictionary<string, string>();
                    if (device_name != null)
                        data_dic["Name"] = device_name;
                    if (device_serial != null)
                        data_dic["SerialNumber"] = device_serial;
                    data_dic["__RequestVerificationToken"] = verification_token;
                    var urlenc = new FormUrlEncodedContent(data_dic);
                    using (var post_device_create_response = await http_client_helper.Client.PostAsync(create_device_url, urlenc)) {
                        var dom = DroHubFixture.getHtmlDOM(await post_device_create_response.Content.ReadAsStringAsync());
                        Assert.Equal(is_valid, dom.QuerySelectorAll("input[name='IsValid']").First().GetAttribute("value"));
                    }
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

        [InlineData("")]
        [InlineData("0000")]
        [Theory]
        public async void TestConnectionClosedOnInvalidSerial(string serial_field)
        {
            using (var ws_transport = new TWebSocketClient(_fixture.ThriftUri, System.Net.WebSockets.WebSocketMessageType.Text))
            {
                if (serial_field != null)
                    ws_transport.WebSocketOptions.SetRequestHeader("x-device-expected-serial", serial_field);
                await Assert.ThrowsAsync<System.Net.WebSockets.WebSocketException>(async () => await ws_transport.OpenAsync());
            }
        }
    }
}
