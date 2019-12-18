using System;
using Xunit;
using DroHub.Tests.TestInfrastructure;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Thrift.Transport;
using Thrift.Protocol;
using DroHub.Helpers.Thrift;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Collections;
using System.Net.Http;
using System.Net;
using System.Reflection;

namespace DroHub.Tests
{
    public class IntegrationTest : IClassFixture<DroHubFixture>
    {
        DroHubFixture _fixture;
        public IntegrationTest(DroHubFixture fixture) {
            _fixture = fixture;
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
        public async void TestConnectionClosedOnInvalidSerial(string serial_field) {
            using (var ws_transport = new TWebSocketClient(_fixture.ThriftUri, System.Net.WebSockets.WebSocketMessageType.Text))
            {
                if (serial_field != null)
                    ws_transport.WebSocketOptions.SetRequestHeader("x-device-expected-serial", serial_field);
                await Assert.ThrowsAsync<System.Net.WebSockets.WebSocketException>(async () => await ws_transport.OpenAsync());
            }
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
    }
}
