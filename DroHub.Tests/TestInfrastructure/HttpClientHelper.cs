using System;
using System.Net.Http;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Parser;
using System.Collections.Generic;

namespace DroHub.Tests.TestInfrastructure
{
    public class HttpClientHelper : IDisposable
    {
        private bool _disposed;
        private DroHubFixture _test_fixture;
        public HttpClientHandler handlerHttp { get; private set; }
        public HttpClient Client {get; set; }
        public HttpResponseMessage Response { get; private set; }
        public CookieContainer cookieContainer {get; private set; }
        public string verificationToken { get; private set; }

        public HttpClientHelper(DroHubFixture test_fixture) {
            _disposed = false;
            _test_fixture = test_fixture;
            cookieContainer = new CookieContainer();
            handlerHttp = new HttpClientHandler { UseCookies = true, UseDefaultCredentials = true, CookieContainer = cookieContainer };
            Client = new HttpClient(handlerHttp);
        }

        public static async  ValueTask<HttpClientHelper> createLoggedInAdmin(DroHubFixture test_fixture) {
            return await createLoggedInUser(test_fixture, "admin", test_fixture.AdminPassword);
        }

        public static async ValueTask<HttpClientHelper> createHttpClient(DroHubFixture test_fixture) {
            var http_helper = new HttpClientHelper(test_fixture);
            http_helper.Response = await http_helper.Client.GetAsync(test_fixture.SiteUri);
            http_helper.Response.EnsureSuccessStatusCode();
            http_helper.verificationToken = DroHubFixture.getVerificationToken(await http_helper.Response.Content.ReadAsStringAsync());
            return http_helper;
        }

        public static async ValueTask<HttpClientHelper> createLoggedInUser(DroHubFixture test_fixture, string user, string password) {
            var login_uri = new Uri(test_fixture.SiteUri, "Identity/Account/Login");
            var http_helper = await createHttpClient(test_fixture);
            var contentToSend = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Input.UserName", user),
                new KeyValuePair<string, string>("Input.Password", password),
                new KeyValuePair<string, string>("__RequestVerificationToken", http_helper.verificationToken),
            });
            http_helper.Response.Dispose();
            http_helper.Response = await http_helper.Client.PostAsync(login_uri, contentToSend);
            http_helper.Response.EnsureSuccessStatusCode();
            if (http_helper.Response.RequestMessage.RequestUri != new Uri(test_fixture.SiteUri, "Identity/Account/Manage"))
            {
                Console.WriteLine(http_helper.verificationToken);
                throw new InvalidProgramException($"Login failed. Instead we are in {http_helper.Response.RequestMessage.RequestUri.ToString()}" );
            }
            return http_helper;
        }

        public static async ValueTask<HttpClientHelper> createDevice(DroHubFixture test_fixture, string device_name, string device_serial) {
            var http_helper = await HttpClientHelper.createLoggedInAdmin(test_fixture);
            var content = await http_helper.Response.Content.ReadAsStringAsync();
            var create_device_url = new Uri(test_fixture.SiteUri, "DHub/Devices/Create");
            using (var create_page_response = await http_helper.Client.GetAsync(create_device_url))
            {
                create_page_response.EnsureSuccessStatusCode();
                var verification_token = DroHubFixture.getVerificationToken(await create_page_response.Content.ReadAsStringAsync());
                var data_dic = new Dictionary<string, string>();
                if (device_name != null)
                    data_dic["Name"] = device_name;
                if (device_serial != null)
                    data_dic["SerialNumber"] = device_serial;
                data_dic["__RequestVerificationToken"] = verification_token;
                var urlenc = new FormUrlEncodedContent(data_dic);
                http_helper.Response?.Dispose();
                http_helper.Response = await http_helper.Client.PostAsync(create_device_url, urlenc);
                http_helper.Response.EnsureSuccessStatusCode();
                return http_helper;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    handlerHttp?.Dispose();
                    Client?.Dispose();
                    Response?.Dispose();
                }
                _disposed = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
    }
}