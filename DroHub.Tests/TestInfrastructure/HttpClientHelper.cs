using System;
using System.Net.Http;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Security.Authentication;
using DroHub.Areas.DHub.Controllers;
using DroHub.Areas.DHub.Models;
using DroHub.Data.Migrations;

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

        public class AddUserHelper : IAsyncDisposable {
            public static async ValueTask<AddUserHelper> addUser(DroHubFixture test_fixture,
                string user_email_of_creator,
                string password_of_creator,
                string user_email,
                string user_password,
                string organization,
                string user_base_type,
                long allowed_flight_time_minutes,
                int allowed_user_count) {

                if (user_email == "admin")
                    return new AddUserHelper(test_fixture, user_email, user_password);

                var http_helper = await createLoggedInUser(test_fixture, user_email_of_creator, password_of_creator);
                await http_helper.Response.Content.ReadAsStringAsync();
                var create_user_url = new Uri(test_fixture.SiteUri, "Identity/Account/Manage/AdminPanel");
                using var create_page_response = await http_helper.Client.GetAsync(create_user_url);
                create_page_response.EnsureSuccessStatusCode();
                var verification_token =
                    DroHubFixture.getVerificationToken(await create_page_response.Content.ReadAsStringAsync());
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
                http_helper?.Dispose();
                var errors = dom.QuerySelectorAll("div.validation-summary-errors");
                var s = "";
                if (!errors.Any())
                    return new AddUserHelper(test_fixture, user_email, user_password);

                s = errors.Aggregate(s, (current, e) => current + (e.InnerHtml + "\n"));
                throw new InvalidOperationException($"User Add has failed. Errors: {s}");

            }

            public static async ValueTask<AddUserHelper> addUser(DroHubFixture test_fixture,
                string user_email,
                string user_password,
                string organization,
                string user_base_type,
                long allowed_flight_time_minutes,
                int allowed_user_count) {

                return await addUser(test_fixture, "admin", test_fixture.AdminPassword, user_email, user_password,
                    organization, user_base_type, allowed_flight_time_minutes, allowed_user_count);
            }

            private static async Task deleteUser(DroHubFixture test_fixture, string user_email, string user_password) {
                if (user_email == "admin")
                    return;

                var http_helper = await createLoggedInUser(test_fixture, user_email, user_password);
                var create_device_url = new Uri(test_fixture.SiteUri, "Identity/Account/Manage/DeletePersonalData");
                using var create_page_response = await http_helper.Client.GetAsync(create_device_url);
                create_page_response.EnsureSuccessStatusCode();
                var verification_token = DroHubFixture.getVerificationToken(await create_page_response.Content.ReadAsStringAsync());
                var data_dic = new Dictionary<string, string>();
                setIfNotNull(data_dic, "Password", user_password);
                data_dic["__RequestVerificationToken"] = verification_token;
                var urlenc = new FormUrlEncodedContent(data_dic);
                http_helper.Response?.Dispose();
                http_helper.Response = await http_helper.Client.PostAsync(create_device_url, urlenc);
                http_helper.Response.EnsureSuccessStatusCode();
                http_helper.Dispose();
            }

            private readonly DroHubFixture _fixture;
            private readonly string _user_email;
            private readonly string _password;

            private AddUserHelper(DroHubFixture test_fixture, string user_email, string user_password) {
                _fixture = test_fixture;
                _user_email = user_email;
                _password = user_password;
            }

            public async ValueTask DisposeAsync() {
                await deleteUser(_fixture, _user_email, _password);
            }
        }

        public static async ValueTask<HttpClientHelper> createHttpClient(DroHubFixture test_fixture, Uri uri) {
            var http_helper = new HttpClientHelper(test_fixture);
            http_helper.Response = await http_helper.Client.GetAsync(uri);
            http_helper.Response.EnsureSuccessStatusCode();
            return http_helper;
        }

        public static async ValueTask<HttpClientHelper> createLoggedInUser(DroHubFixture test_fixture, string user, string password) {
            var login_uri = new Uri(test_fixture.SiteUri, "Identity/Account/Login");
            var http_helper = new HttpClientHelper(test_fixture);
            http_helper.Response = await http_helper.Client.GetAsync(login_uri);
            http_helper.Response.EnsureSuccessStatusCode();
            http_helper.verificationToken = DroHubFixture.getVerificationToken(await http_helper.Response.Content.ReadAsStringAsync());
            var contentToSend = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Input.Email", user),
                new KeyValuePair<string, string>("Input.Password", password),
                new KeyValuePair<string, string>("__RequestVerificationToken", http_helper.verificationToken),
            });
            http_helper.Response.Dispose();
            http_helper.Response = await http_helper.Client.PostAsync(login_uri, contentToSend);
            http_helper.Response.EnsureSuccessStatusCode();
            if (http_helper.Response.RequestMessage.RequestUri != new Uri(test_fixture.SiteUri, "Identity/Account/Manage"))
            {
                Console.WriteLine(http_helper.verificationToken);
                throw new InvalidProgramException($"Login failed. Instead we are in {http_helper.Response.RequestMessage.RequestUri}" );
            }
            http_helper.loginCookie = http_helper.cookieContainer.GetCookies(login_uri)[".AspNetCore.Identity.Application"];
            return http_helper;
        }

        public static readonly DateTime UnixEpoch =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static async ValueTask<List<dynamic>> getDeviceList(DroHubFixture test_fixture, string user, string password) {
            using (var http_helper = await createLoggedInUser(test_fixture, user, password)) {
                var create_device_url = new Uri(test_fixture.SiteUri, "DHub/Devices/GetDevicesList");
                http_helper.Response?.Dispose();
                http_helper.Response = await http_helper.Client.GetAsync(create_device_url);
                var stringified = await http_helper.Response.Content.ReadAsStringAsync();
                return Newtonsoft.Json.JsonConvert.DeserializeObject<List<dynamic>>(stringified);
            }
        }

        public class CreateDeviceHelper : IAsyncDisposable {
            private readonly DroHubFixture _fixture;
            private readonly string _user_email;
            private readonly string _password;
            private readonly string _device_serial;

            public static async ValueTask<CreateDeviceHelper> createDevice(DroHubFixture test_fixture, string user, string password,
                string device_name, string device_serial, bool use_app_api = false) {
                if (user == "admin")
                    password = test_fixture.AdminPassword;

                if (use_app_api) {
                    var token_result = await getApplicationToken(test_fixture, user, password);
                    var m = new AndroidApplicationController.DeviceCreateModel() {
                        Device = new Device() {
                            SerialNumber = device_serial,
                            Name = device_name
                        }
                    };
                    if (token_result["result"] == "nok")
                        throw new InvalidCredentialException("Could not retrieve token");

                    var result = await retrieveFromAndroidApp(test_fixture, user, token_result["result"],
                        "CreateDevice", m);

                    var json_obj = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,string>>(result);
                    if (json_obj["result"] != "ok")
                        throw new InvalidDataException(json_obj["result"]);
                }
                else {
                    using var http_helper = await createLoggedInUser(test_fixture, user, password);
                    var create_device_url = new Uri(test_fixture.SiteUri, "DHub/Devices/Create");
                    using var create_page_response = await http_helper.Client.GetAsync(create_device_url);
                    create_page_response.EnsureSuccessStatusCode();
                    var verification_token =
                        DroHubFixture.getVerificationToken(await create_page_response.Content.ReadAsStringAsync());
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
                }

                return new CreateDeviceHelper(test_fixture, user, password, device_serial);
            }

            private static async Task deleteDevice(DroHubFixture test_fixture, int device_id, string user, string
            password) {
                var http_helper = await HttpClientHelper.createLoggedInUser(test_fixture, user, password);
                var content = await http_helper.Response.Content.ReadAsStringAsync();
                var create_device_url = new Uri(test_fixture.SiteUri, $"DHub/Devices/Delete/{device_id}");
                using var create_page_response = await http_helper.Client.GetAsync(create_device_url);
                create_page_response.EnsureSuccessStatusCode();
                var verification_token = DroHubFixture.getVerificationToken(await create_page_response.Content.ReadAsStringAsync());
                var data_dic = new Dictionary<string, string>();
                data_dic["Id"] = device_id.ToString();
                data_dic["__RequestVerificationToken"] = verification_token;
                var urlenc = new FormUrlEncodedContent(data_dic);
                http_helper.Response?.Dispose();
                http_helper.Response = await http_helper.Client.PostAsync(create_device_url, urlenc);
                http_helper.Response.EnsureSuccessStatusCode();
                http_helper?.Dispose();
            }

            private static async Task deleteDevice(DroHubFixture test_fixture, string serial_number, string user,
                string password)
            {
                var devices_list = await getDeviceList(test_fixture, user, password);
                int device_id = devices_list.First(d => d.serialNumber == serial_number).id;
                await deleteDevice(test_fixture, device_id, user, password);
            }

            private CreateDeviceHelper(DroHubFixture test_fixture, string user_email, string user_password,
                    string device_serial) {
                _fixture = test_fixture;
                _user_email = user_email;
                _password = user_password;
                _device_serial = device_serial;
            }

            public async ValueTask DisposeAsync() {
                await deleteDevice(_fixture, _device_serial, _user_email, _password);
            }
        }

        public static TWebSocketClient getTWebSocketClient(DroHubFixture fixture, string user, string token,
            string device_serial) {
            var ws_transport = new TWebSocketClient(fixture.ThriftUri, System.Net.WebSockets.WebSocketMessageType.Text);
            ws_transport.WebSocketOptions.SetRequestHeader("Content-Type", "application/x-thrift");
            ws_transport.WebSocketOptions.SetRequestHeader("x-device-expected-serial", device_serial);
            ws_transport.WebSocketOptions.SetRequestHeader("x-drohub-user", user);
            ws_transport.WebSocketOptions.SetRequestHeader("x-drohub-token", token);
            return ws_transport;
        }

        public static async Task<TWebSocketClient> openWebSocket(DroHubFixture fixture, string user, string token, string
        device_serial) {
            var ws_transport = getTWebSocketClient(fixture, user, token, device_serial);
            await ws_transport.OpenAsync();
            return ws_transport;
        }

        public static async Task<int> getDeviceId(DroHubFixture test_fixture, string serial_number, string user,
            string password) {
            var devices_list = await getDeviceList(test_fixture, user, password);
            return devices_list.First(d => d.serialNumber == serial_number).id;
        }

        public static async Task<long?> getDeviceFlightStartTime(DroHubFixture test_fixture, int device_id,
            string user, string password) {
            var http_helper = await createLoggedInUser(test_fixture, user, password);
            var create_device_url = new Uri(test_fixture.SiteUri,
                $"DHub/Devices/GetDeviceFlightStartTime/{device_id}");

            http_helper.Response?.Dispose();
            http_helper.Response = await http_helper.Client.GetAsync(create_device_url);
            http_helper.Response.EnsureSuccessStatusCode();
            var s = await http_helper.Response.Content.ReadAsStringAsync();
            return Newtonsoft.Json.JsonConvert.DeserializeObject<long?>(s);
        }

        public static async ValueTask<List<T>> getDeviceTelemetry<T>(DroHubFixture test_fixture, string serial_number, string user, string password,
                int start_index, int end_index) {
            var device_id = await getDeviceId(test_fixture, serial_number, user, password);

            using (var http_helper = await HttpClientHelper.createLoggedInUser(test_fixture, user, password)) {
                var content = await http_helper.Response.Content.ReadAsStringAsync();
                var create_device_url = new Uri(test_fixture.SiteUri, $"DHub/Devices/Get{typeof(T)}s/{device_id}");
                http_helper.Response?.Dispose();
                var data_dic = new Dictionary<string, string>();
                data_dic["start_index"] = start_index.ToString();
                data_dic["end_index"] = end_index.ToString();
                var urlenc = new FormUrlEncodedContent(data_dic);
                http_helper.Response = await http_helper.Client.PostAsync(create_device_url, urlenc);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<List<T>>(await http_helper.Response.Content.ReadAsStringAsync());
            }
        }

        public static async ValueTask<Dictionary<string, string>> getApplicationToken(DroHubFixture test_fixture, string user_name,
            string password) {
            var auth_token_uri = new Uri(test_fixture.SiteUri, "api/GetToken/GetApplicationToken");
            var content_to_send = new GetTokenController.GetTokenModel() {
                UserName = user_name,
                Password = password,
            };

            var http_helper = new HttpClientHelper(test_fixture);
            http_helper.Response = await http_helper.Client.PostAsJsonAsync(auth_token_uri, content_to_send);
            http_helper.Response.EnsureSuccessStatusCode();
            var res = await http_helper.Response.Content.ReadAsStringAsync();
            if (http_helper.Response.RequestMessage.RequestUri == auth_token_uri) {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,string>>(res);
            }

            throw new InvalidProgramException($"Unexpected Redirect... ");
        }

        private static async ValueTask<string> retrieveFromAndroidApp(DroHubFixture test_fixture, string user,
            string token, string action_name, object query) {

            var auth_token_uri = new Uri(test_fixture.SiteUri, $"api/AndroidApplication/{action_name}");
            var http_helper = new HttpClientHelper(test_fixture);
            http_helper.Client.DefaultRequestHeaders.Add("x-drohub-user", user);
            http_helper.Client.DefaultRequestHeaders.Add("x-drohub-token", token);
            http_helper.Response = await http_helper.Client.PostAsJsonAsync(auth_token_uri, query);
            http_helper.Response.EnsureSuccessStatusCode();
            return await http_helper.Response.Content.ReadAsStringAsync();
        }

        public static async ValueTask<Dictionary<string, Device>> queryDeviceInfo(DroHubFixture test_fixture,
            string user_name, string token, string device_serial_number) {
            var query = new AndroidApplicationController.QueryDeviceModel() {
                DeviceSerialNumber = device_serial_number
            };
            var res = await retrieveFromAndroidApp(test_fixture, user_name, token, "QueryDeviceInfo", query);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,Device>>(res);
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