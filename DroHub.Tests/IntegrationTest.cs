using System;
using Xunit;
using DroHub.Tests.TestInfrastructure;
using System.Net.Http;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using AngleSharp;
using AngleSharp.Html.Parser;

namespace DroHub.Tests
{
    public class IntegrationTest : IClassFixture<DroHubFixture>
    {
        DroHubFixture _fixture;
        public IntegrationTest(DroHubFixture fixture) {
            _fixture = fixture;
        }
        private string getVerificationToken(string responseBody)
        {
            var context = BrowsingContext.New(Configuration.Default);
            var parser = context.GetService<IHtmlParser>();
            var document = parser.ParseDocument(responseBody);
            return document.QuerySelectorAll("input[name='__RequestVerificationToken']").FirstOrDefault().GetAttribute("value");
        }

        [Fact]

        public async void TestConnectionClosedOnNoSerial()
        {
            using (var ws_transport = new TWebSocketClient(new Uri("ws://localhost:5000/ws"), System.Net.WebSockets.WebSocketMessageType.Text))
            {
                await Assert.ThrowsAsync<System.Net.WebSockets.WebSocketException>(async () => await ws_transport.OpenAsync());
                await Task.Delay(5000);
            }
        }

        [InlineData("")]
        [InlineData("0000")]
        [Theory]
        public async void TestConnectionClosedOnInvalidSerial(string serial_field) {
            using (var ws_transport = new TWebSocketClient(new Uri("ws://localhost:5000/ws"), System.Net.WebSockets.WebSocketMessageType.Text))
            {
                if (serial_field != null)
                    ws_transport.WebSocketOptions.SetRequestHeader("x-device-expected-serial", serial_field);
                await Assert.ThrowsAsync<System.Net.WebSockets.WebSocketException>(async () => await ws_transport.OpenAsync());
            }
        }

        public async void TestLogin()
        {
            CookieContainer cookieContainer = new CookieContainer();

            string verificationToken;
            var login_uri = new Uri(_fixture.SiteUri, "Identity/Account/Login");

            using (HttpClientHandler handler_http = new HttpClientHandler { UseCookies = true, UseDefaultCredentials = true, CookieContainer = cookieContainer })
            {
                using (HttpClient client = new HttpClient(handler_http))
                {
                    using (HttpResponseMessage response = await client.GetAsync(_fixture.SiteUri))
                    {
                        Assert.Equal(new Uri(_fixture.SiteUri, "Identity/Account/Login?ReturnUrl=%2FIdentity%2FAccount%2FManage"),
                            response.RequestMessage.RequestUri);
                        verificationToken = getVerificationToken(await response.Content.ReadAsStringAsync());
                    }
                    var contentToSend = new FormUrlEncodedContent(new[]
                    {   
                        new KeyValuePair<string, string>("Input.UserName", "admin"),
                        new KeyValuePair<string, string>("Input.Password", _fixture.AdminPassword),
                        new KeyValuePair<string, string>("__RequestVerificationToken", verificationToken),
                    });

                    using (var response = await client.PostAsync(login_uri, contentToSend)) {
                        Assert.Equal(response.RequestMessage.RequestUri, new Uri(_fixture.SiteUri, "Identity/Account/Manage"));

                        var result = await response.Content.ReadAsStringAsync();
                    }

                }
            };
        }
    }
}
