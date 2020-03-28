using System;
using System.Net.Http;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
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
        public Cookie loginCookie { get; private set; }

        public HttpClientHelper(DroHubFixture test_fixture) {
            _disposed = false;
            _test_fixture = test_fixture;
            cookieContainer = new CookieContainer();
            handlerHttp = new HttpClientHandler { UseCookies = true, UseDefaultCredentials = true, CookieContainer = cookieContainer };
            Client = new HttpClient(handlerHttp);
        }

        private static void setIfNotNull(Dictionary<string, string> d, string key, string value) {
            if (value != null)
                d[key] = value;
        }

        public static async ValueTask<HttpClientHelper> addUser(DroHubFixture test_fixture, string user_email, string user_password,
            string organization, string user_base_type, int allowed_flight_time_minutes, int allowed_user_count) {
            var http_helper = await HttpClientHelper.createLoggedInUser(test_fixture, "admin", test_fixture.AdminPassword);
            await http_helper.Response.Content.ReadAsStringAsync();
            var create_user_url = new Uri(test_fixture.SiteUri, "Identity/Account/Manage/AdminPanel");
            using (var create_page_response = await http_helper.Client.GetAsync(create_user_url))
            {
                create_page_response.EnsureSuccessStatusCode();
                var verification_token = DroHubFixture.getVerificationToken(await create_page_response.Content.ReadAsStringAsync());
                var data_dic = new Dictionary<string, string>();
                setIfNotNull(data_dic, "Email", user_email);
                setIfNotNull(data_dic, "Password", user_password);
                setIfNotNull(data_dic, "ConfirmPassword", user_password);
                setIfNotNull(data_dic, "ActingType", user_base_type);
                setIfNotNull(data_dic, "OrganizationName", organization);
                setIfNotNull(data_dic, "AllowedFlightTime", allowed_flight_time_minutes.ToString());
                setIfNotNull(data_dic, "AllowedUserCount", allowed_user_count.ToString());

                data_dic["__RequestVerificationToken"] = verification_token;
                var urlenc = new FormUrlEncodedContent(data_dic);
                http_helper.Response?.Dispose();
                http_helper.Response = await http_helper.Client.PostAsync(create_user_url, urlenc);
                http_helper.Response.EnsureSuccessStatusCode();
                var dom = DroHubFixture.getHtmlDOM(await http_helper.Response.Content.ReadAsStringAsync());
                if (dom.QuerySelectorAll("div.validation-summary-errors").Any())
                    throw new InvalidOperationException("User Add has failed");
                return http_helper;
            }
        }

        public static async ValueTask<HttpClientHelper> deleteUser(DroHubFixture test_fixture, string user_email, string user_password) {
            var http_helper = await createLoggedInUser(test_fixture, user_email, user_password);
            var create_device_url = new Uri(test_fixture.SiteUri, "Identity/Account/Manage/DeletePersonalData");
            using (var create_page_response = await http_helper.Client.GetAsync(create_device_url))
            {
                create_page_response.EnsureSuccessStatusCode();
                var verification_token = DroHubFixture.getVerificationToken(await create_page_response.Content.ReadAsStringAsync());
                var data_dic = new Dictionary<string, string>();
                setIfNotNull(data_dic, "Password", user_password);
                data_dic["__RequestVerificationToken"] = verification_token;
                var urlenc = new FormUrlEncodedContent(data_dic);
                http_helper.Response?.Dispose();
                http_helper.Response = await http_helper.Client.PostAsync(create_device_url, urlenc);
                http_helper.Response.EnsureSuccessStatusCode();
                return http_helper;
            }
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
            http_helper.loginCookie = http_helper.cookieContainer.GetCookies(login_uri)[".AspNetCore.Identity.Application"];
            return http_helper;
        }

        public static async ValueTask<List<dynamic>> getDeviceList(DroHubFixture test_fixture, string user, string password) {
            using (var http_helper = await HttpClientHelper.createLoggedInUser(test_fixture, user, password)) {
                var content = await http_helper.Response.Content.ReadAsStringAsync();
                var create_device_url = new Uri(test_fixture.SiteUri, "DHub/Devices/GetDevicesList");
                http_helper.Response?.Dispose();
                http_helper.Response = await http_helper.Client.GetAsync(create_device_url);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<List<dynamic>>(await http_helper.Response.Content.ReadAsStringAsync());
            }
        }

        public static async ValueTask<HttpClientHelper> createDevice(DroHubFixture test_fixture, string user, string password,
            string organization, string user_base_type, int allowed_flight_time_minutes, int allowed_user_count,
            string device_name, string device_serial, bool create_user = false) {
            HttpClientHelper http_helper;
            if (create_user)
                (await addUser(test_fixture, user, password, organization, user_base_type,
                    allowed_flight_time_minutes, allowed_user_count)).Dispose();
            http_helper = await HttpClientHelper.createLoggedInUser(test_fixture, user, password);

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
                var dom = DroHubFixture.getHtmlDOM(await http_helper.Response.Content.ReadAsStringAsync());
                if (dom.QuerySelectorAll("input[name='IsValid']").First().GetAttribute("value") != "True")
                    throw new InvalidOperationException("create Device failed");
                return http_helper;
            }
        }

        public static async ValueTask<HttpClientHelper> deleteDevice(DroHubFixture test_fixture, int device_id, string user, string password) {
            var http_helper = await HttpClientHelper.createLoggedInUser(test_fixture, user, password);
            var content = await http_helper.Response.Content.ReadAsStringAsync();
            var create_device_url = new Uri(test_fixture.SiteUri, $"DHub/Devices/Delete/{device_id}");
            using (var create_page_response = await http_helper.Client.GetAsync(create_device_url))
            {
                create_page_response.EnsureSuccessStatusCode();
                var verification_token = DroHubFixture.getVerificationToken(await create_page_response.Content.ReadAsStringAsync());
                var data_dic = new Dictionary<string, string>();
                data_dic["Id"] = device_id.ToString();
                data_dic["__RequestVerificationToken"] = verification_token;
                var urlenc = new FormUrlEncodedContent(data_dic);
                http_helper.Response?.Dispose();
                http_helper.Response = await http_helper.Client.PostAsync(create_device_url, urlenc);
                http_helper.Response.EnsureSuccessStatusCode();
                return http_helper;
            }
        }

        public static async ValueTask<HttpClientHelper> deleteDevice(DroHubFixture test_fixture, string serial_number, string user, string password)
        {
            var devices_list = await HttpClientHelper.getDeviceList(test_fixture, user, password);
            int device_id = devices_list.First(d => d.serialNumber == serial_number).id;
            return await HttpClientHelper.deleteDevice(test_fixture, device_id, user, password);
        }

        public static async ValueTask<List<T>> getDeviceTelemetry<T>(DroHubFixture test_fixture, string serial_number, string user, string password,
                int start_index, int end_index) {
            var devices_list = await HttpClientHelper.getDeviceList(test_fixture, user, password);
            int device_id = devices_list.First(d => d.serialNumber == serial_number).id;

            using (var http_helper = await HttpClientHelper.createLoggedInUser(test_fixture, user, password)) {
                var content = await http_helper.Response.Content.ReadAsStringAsync();
                var create_device_url = new Uri(test_fixture.SiteUri, $"DHub/Devices/Get{typeof(T)}s/{device_id}");
                http_helper.Response?.Dispose();
                var data_dic = new Dictionary<string, string>();
                data_dic["start_index"] = start_index.ToString();
                data_dic["end_index"] = end_index.ToString();
                var urlenc = new FormUrlEncodedContent(data_dic);
                http_helper.Response = await http_helper.Client.PostAsync(create_device_url, urlenc);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<List<List<T>>>(await http_helper.Response.Content.ReadAsStringAsync()).First();
            }
        }

        private static async ValueTask<Dictionary<string, string>> runJsonQuery(DroHubFixture test_fixture, Uri uri,
            Dictionary<string, string> form) {

            var http_helper = await createHttpClient(test_fixture);

            http_helper.Response.Dispose();
            http_helper.Response = await http_helper.Client.PostAsJsonAsync(uri, form);
            http_helper.Response.EnsureSuccessStatusCode();
            var res = await http_helper.Response.Content.ReadAsStringAsync();
            if (http_helper.Response.RequestMessage.RequestUri == uri) {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,string>>(res);
            }

            throw new InvalidProgramException($"Unexpected Redirect... ");
        }

        public static async ValueTask<Dictionary<string, string>> getApplicationToken(DroHubFixture test_fixture, string user_name,
            string password) {
            var auth_token_uri = new Uri(test_fixture.SiteUri, "api/User/GetApplicationToken");
            var content_to_send = new Dictionary<string,string>()
            {
                {"UserName", user_name},
                {"Password", password}
            };
            return await runJsonQuery(test_fixture, auth_token_uri, content_to_send);
        }

        public static async ValueTask<Dictionary<string, string>> authenticateToken(DroHubFixture test_fixture,
            string user_name, string token) {
            var auth_token_uri = new Uri(test_fixture.SiteUri, "api/User/AuthenticateToken");
            var http_helper = await createHttpClient(test_fixture);
            var content_to_send = new Dictionary<string,string>()
            {
                {"UserName", user_name},
                {"Token", token}
            };

            http_helper.Response.Dispose();
            http_helper.Response = await http_helper.Client.PostAsJsonAsync(auth_token_uri, content_to_send);
            http_helper.Response.EnsureSuccessStatusCode();
            var res = await http_helper.Response.Content.ReadAsStringAsync();
            if (http_helper.Response.RequestMessage.RequestUri == auth_token_uri) {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,string>>(res);
            }

            throw new InvalidProgramException($"Token creation failed." );
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