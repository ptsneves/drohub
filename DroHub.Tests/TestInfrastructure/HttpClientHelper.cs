using System;
using System.Net.Http;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Threading;
using DroHub.Areas.DHub.Controllers;
using DroHub.Areas.DHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DroHub.Tests.TestInfrastructure
{
    public class HttpClientHelper : IDisposable
    {
        private bool _disposed;
        public HttpClientHandler handlerHttp { get; private set; }
        public HttpClient Client {get; set; }
        public HttpResponseMessage Response { get; private set; }
        public CookieContainer cookieContainer {get; private set; }
        public string verificationToken { get; private set; }
        public Cookie loginCookie { get; private set; }

        public HttpClientHelper() {
            _disposed = false;
            cookieContainer = new CookieContainer();
            handlerHttp = new HttpClientHandler {
                UseCookies = true, UseDefaultCredentials = true, CookieContainer = cookieContainer,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            Client = new HttpClient(handlerHttp);
        }

        private static void setIfNotNull(Dictionary<string, string> d, string key, string value) {
            if (value != null)
                d[key] = value;
        }

        public static async Task sendInvitation(string agent_email, string agent_password,
            string[] emails) {

            var http_helper = await createLoggedInUser(agent_email, agent_password);
            var send_invitation_url = new Uri(DroHubFixture.SiteUri, "DHub/AccountManagement/SendInvitation");
            using var create_page_response = await http_helper.Client.GetAsync(send_invitation_url);
            create_page_response.EnsureSuccessStatusCode();


            var verification_token = DroHubFixture.getVerificationToken(
                await create_page_response.Content.ReadAsStringAsync(),
                "account-send-invitation[anti-forgery-token]",
                "anti-forgery-token");

            var emails_dict = emails.ToDictionary(x => Array.IndexOf(emails, x));
            var data_dic = new Dictionary<string, string> {
                ["__RequestVerificationToken"] = verification_token,
            };
            foreach (var email in emails) {
                var key = $"emails[{Array.IndexOf(emails, email).ToString()}]";
                data_dic[key] = email;
            }

            var urlenc = new FormUrlEncodedContent(data_dic);
            http_helper.Response?.Dispose();
            http_helper.Response = await http_helper.Client.PostAsync(send_invitation_url, urlenc);
            http_helper.Response.EnsureSuccessStatusCode();
            http_helper.Dispose();
        }


        public static async Task changePermissions(string agent_email, string agent_pass,
            string victim_email, string victim_target_permission) {

            if (victim_email == "admin@drohub.xyz")
                return;

            var http_helper = await createLoggedInUser(agent_email, agent_pass);
            var change_permission_url = new Uri(DroHubFixture.SiteUri, "DHub/AccountManagement/ChangeUserPermissions");
            using var create_page_response = await http_helper.Client.GetAsync(change_permission_url);
            create_page_response.EnsureSuccessStatusCode();
            var verification_token = DroHubFixture.getVerificationToken(
                await create_page_response.Content.ReadAsStringAsync(),
                "account-change-permissions-modal[anti-forgery-token]",
                "anti-forgery-token");

            var data_dic = new Dictionary<string, string> {
                ["__RequestVerificationToken"] = verification_token,
                ["user_email"] = victim_email,
                ["permission_type"] = victim_target_permission.Replace("Acting", "").ToLower()
            };
            var urlenc = new FormUrlEncodedContent(data_dic);
            http_helper.Response?.Dispose();
            http_helper.Response = await http_helper.Client.PostAsync(change_permission_url, urlenc);
            http_helper.Response.EnsureSuccessStatusCode();
            http_helper.Dispose();
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
                int allowed_user_count,
                bool remove_on_dispose = true) {

                if (user_email == "admin@drohub.xyz")
                    return new AddUserHelper(test_fixture, user_email, user_password, remove_on_dispose);

                var http_helper = await createLoggedInUser(user_email_of_creator, password_of_creator);
                await http_helper.Response.Content.ReadAsStringAsync();
                var create_user_url = new Uri(DroHubFixture.SiteUri, "Identity/Account/Manage/AdminPanel");
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
                    return new AddUserHelper(test_fixture, user_email, user_password, remove_on_dispose);

                s = errors.Aggregate(s, (current, e) => current + (e.InnerHtml + "\n"));
                throw new InvalidOperationException($"User Add has failed. Errors: {s}");

            }

            public static async ValueTask<AddUserHelper> addUser(DroHubFixture test_fixture,
                string user_email,
                string user_password,
                string organization,
                string user_base_type,
                long allowed_flight_time_minutes,
                int allowed_user_count,
                bool remove_on_dispose = true) {

                return await addUser(test_fixture, "admin@drohub.xyz",
                    test_fixture.AdminPassword, user_email, user_password,
                    organization, user_base_type, allowed_flight_time_minutes, allowed_user_count, remove_on_dispose);
            }

            public static async Task excludeUser(string deleter_email, string deleter_password,
                    string user_email_to_delete) {
                if (user_email_to_delete == "admin@drohub.xyz")
                    return;

                var http_helper = await createLoggedInUser(deleter_email, deleter_password);
                var create_device_url = new Uri(DroHubFixture.SiteUri, "DHub/AccountManagement/ExcludeUser");
                using var create_page_response = await http_helper.Client.GetAsync(create_device_url);
                create_page_response.EnsureSuccessStatusCode();
                var verification_token = DroHubFixture.getVerificationToken(
                    await create_page_response.Content.ReadAsStringAsync(),
                    "account-exclude-user-modal[anti-forgery-token]",
                    "anti-forgery-token");

                var data_dic = new Dictionary<string, string> {
                    ["__RequestVerificationToken"] = verification_token,
                    ["user_email"] = user_email_to_delete
                };
                var urlenc = new FormUrlEncodedContent(data_dic);
                http_helper.Response?.Dispose();
                http_helper.Response = await http_helper.Client.PostAsync(create_device_url, urlenc);
                http_helper.Response.EnsureSuccessStatusCode();
                http_helper.Dispose();
            }

            public static async Task excludeUser(DroHubFixture test_fixture, string user_email) {
                await excludeUser("admin@drohub.xyz", test_fixture.AdminPassword, user_email);
            }

            private readonly DroHubFixture _fixture;
            private readonly string _user_email;
            private readonly string _password;
            private readonly bool _remove_on_dispose;

            private AddUserHelper(DroHubFixture test_fixture, string user_email, string user_password,
                bool remove_on_dispose) {
                _fixture = test_fixture;
                _user_email = user_email;
                _password = user_password;
                _remove_on_dispose = remove_on_dispose;
            }

            public async ValueTask DisposeAsync() {
                if (_remove_on_dispose)
                    await excludeUser(_fixture, _user_email);
            }
        }

        public static async ValueTask<HttpClientHelper> createHttpClient(Uri uri) {
            var http_helper = new HttpClientHelper();
            http_helper.Response = await http_helper.Client.GetAsync(uri);
            http_helper.Response.EnsureSuccessStatusCode();
            return http_helper;
        }

        public static async Task createJanusHandle() {
            var http_helper = new HttpClientHelper();
            http_helper.Response = await http_helper.Client.PostAsJsonAsync(DroHubFixture.JanusUri, new {
                janus = "help"
            });
            http_helper.Response.EnsureSuccessStatusCode();
        }

        public static async ValueTask<HttpClientHelper> createLoggedInUser(string user, string password) {
            var login_uri = new Uri(DroHubFixture.SiteUri, "Identity/Account/Login");
            var http_helper = new HttpClientHelper();
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
            if (http_helper.Response.RequestMessage.RequestUri != new Uri(DroHubFixture.SiteUri, "Identity/Account/Manage"))
            {
                Console.WriteLine(http_helper.verificationToken);
                throw new InvalidProgramException($"Login failed. Instead we are in {http_helper.Response.RequestMessage.RequestUri}" );
            }
            http_helper.loginCookie = http_helper.cookieContainer.GetCookies(login_uri)[".AspNetCore.Identity.Application"];
            return http_helper;
        }

        public static readonly DateTime UnixEpoch =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static async ValueTask<List<Device>> getDeviceList(string user, string
        password) {
            using (var http_helper = await createLoggedInUser(user, password)) {
                var create_device_url = new Uri(DroHubFixture.SiteUri, "DHub/Devices/GetDevicesList");
                http_helper.Response?.Dispose();
                http_helper.Response = await http_helper.Client.GetAsync(create_device_url);
                var stringified = await http_helper.Response.Content.ReadAsStringAsync();
                return Newtonsoft.Json.JsonConvert.DeserializeObject<List<Device>>(stringified);
            }
        }

        public class CreateDeviceHelper : IAsyncDisposable {
            private readonly string _user_email;
            private readonly string _password;
            private readonly string _device_serial;
            private readonly bool _delete_flight_sessions;

            public static async ValueTask<CreateDeviceHelper> createDevice(DroHubFixture test_fixture, string user, string password,
                string device_name, string device_serial, bool delete_flight_sessions = true) {
                if (user == "admin@drohub.xyz")
                    password = test_fixture.AdminPassword;

                var token_result = await getApplicationToken(user, password);
                var m = new AndroidApplicationController.DeviceCreateModel() {
                    Device = new Device() {
                        SerialNumber = device_serial,
                        Name = device_name
                    }
                };
                if (token_result["result"] == "nok")
                    throw new InvalidCredentialException("Could not retrieve token");

                var result = await retrieveFromAndroidApp(user, token_result["result"],
                    "CreateDevice", m);

                var json_obj = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,string>>(result);
                if (json_obj["result"] != "ok")
                    throw new InvalidDataException(json_obj["result"]);

                return new CreateDeviceHelper(test_fixture, user, password, device_serial, delete_flight_sessions);
            }

            private static async Task deleteDevice(int device_id, string user, string
            password, bool delete_flight_sessions = true) {
                var http_helper = await createLoggedInUser(user, password);
                var content = await http_helper.Response.Content.ReadAsStringAsync();
                var create_device_url = new Uri(DroHubFixture.SiteUri, $"DHub/Devices/Delete/{device_id}");
                using var create_page_response = await http_helper.Client.GetAsync(create_device_url);
                create_page_response.EnsureSuccessStatusCode();
                var verification_token = DroHubFixture.getVerificationToken(await create_page_response.Content.ReadAsStringAsync());
                var data_dic = new Dictionary<string, string>();
                data_dic["Id"] = device_id.ToString();
                data_dic["DeleteFlightSession"] = delete_flight_sessions.ToString();
                data_dic["__RequestVerificationToken"] = verification_token;
                var urlenc = new FormUrlEncodedContent(data_dic);
                http_helper.Response?.Dispose();
                http_helper.Response = await http_helper.Client.PostAsync(create_device_url, urlenc);
                http_helper.Response.EnsureSuccessStatusCode();
                var dom = DroHubFixture.getHtmlDOM(await http_helper.Response.Content.ReadAsStringAsync());
                if (dom.QuerySelectorAll("div.validation-summary-errors").Any())
                    throw new InvalidDataException("Delete device failed");

                http_helper?.Dispose();
            }

            public static async Task deleteDevice(string serial_number, string user,
                string password, bool delete_flight_sessions = true) {
                var devices_list = await getDeviceList(user, password);
                int device_id = devices_list.First(d => d.SerialNumber == serial_number).Id;
                await deleteDevice(device_id, user, password, delete_flight_sessions);
            }

            private CreateDeviceHelper(DroHubFixture test_fixture, string user_email, string user_password,
                    string device_serial, bool delete_flight_sessions) {
                _user_email = user_email;
                _password = user_password;
                _device_serial = device_serial;
                _delete_flight_sessions = delete_flight_sessions;
            }

            public async ValueTask DisposeAsync() {
                await deleteDevice(_device_serial, _user_email, _password, _delete_flight_sessions);
            }
        }

        public static TWebSocketClient getTWebSocketClient(string user, string token,
            string device_serial) {
            var ws_transport = new TWebSocketClient(DroHubFixture.ThriftUri, System.Net.WebSockets
            .WebSocketMessageType.Text, false);
            ws_transport.WebSocketOptions.SetRequestHeader("Content-Type", "application/x-thrift");
            ws_transport.WebSocketOptions.SetRequestHeader("x-device-expected-serial", device_serial);
            ws_transport.WebSocketOptions.SetRequestHeader("x-drohub-user", user);
            ws_transport.WebSocketOptions.SetRequestHeader("x-drohub-token", token);
            return ws_transport;
        }

        public static async Task generateConnectionId(DroHubFixture fixture, TimeSpan duration, string serial,
            string user,
            string password,
            Func<long, Task> connection_id_func) {

            if (duration > TimeSpan.FromSeconds(30))
                throw new InvalidProgramException("Cannot generate device connection longer than 30 seconds because there would be a timeout without telemetry.");

            await using var d = await CreateDeviceHelper.createDevice(fixture, user,
                password, "Aname", serial);

            var token = (await getApplicationToken(user,
                password))["result"];

            using var tr = await openWebSocket(user, token, serial);
            Thread.Sleep(duration);
            tr.Close();
            var device_id = await getDeviceId(serial, user, password);
            var ret = await getLastConnection(fixture, device_id);

            await connection_id_func(ret.Id);
        }

        public static async Task<Dictionary<string, dynamic>> uploadMedia(string user, string password,
            AndroidApplicationController.UploadModel upload_model) {
            var token =  (await getApplicationToken(user,
                password))["result"];

            using var form = new MultipartFormDataContent();
            if (upload_model.DeviceSerialNumber != null) {
                var fileContent = new StreamContent(upload_model.File.OpenReadStream());
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
                form.Add(fileContent, "File", upload_model.File?.FileName);
                form.Add(new StringContent(upload_model.IsPreview ? "true" : "false"), "IsPreview");
                form.Add(new StringContent(upload_model.DeviceSerialNumber), "DeviceSerialNumber");
                form.Add(new StringContent(upload_model.UnixCreationTimeMS.ToString()), "UnixCreationTimeMS");
            }

            var res = await retrieveFromAndroidApp(user, token, "UploadMedia", form, false);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,dynamic>>(res);
        }

        public static async Task<TWebSocketClient> openWebSocket(string user, string token, string
        device_serial) {
            var ws_transport = getTWebSocketClient(user, token, device_serial);
            await ws_transport.OpenAsync();
            return ws_transport;
        }

        public static async Task<int> getDeviceId(string serial_number, string user,
            string password) {
            var devices_list = await getDeviceList(user, password);
            return devices_list.Single(d => d.SerialNumber == serial_number).Id;
        }

        public static async Task<long?> getDeviceFlightStartTime(int device_id,
            string user, string password) {
            var http_helper = await createLoggedInUser(user, password);
            var create_device_url = new Uri(DroHubFixture.SiteUri,
                $"DHub/Devices/GetDeviceFlightStartTime/{device_id}");

            http_helper.Response?.Dispose();
            http_helper.Response = await http_helper.Client.GetAsync(create_device_url);
            http_helper.Response.EnsureSuccessStatusCode();
            var s = await http_helper.Response.Content.ReadAsStringAsync();
            return Newtonsoft.Json.JsonConvert.DeserializeObject<long?>(s);
        }

        private static async Task<DeviceConnection> getLastConnection(DroHubFixture fixture, int device_id) {
            var s = await fixture.DbContext.DeviceConnections
                .Where(cd => cd.DeviceId == device_id)
                .OrderByDescending(cd => cd.Id)
                .FirstAsync();

            if (s.EndTime < s.StartTime)
                throw new InvalidDataException("DeviceConnection still ongoing");
            return s;
        }

        public static async ValueTask<List<T>> getDeviceTelemetry<T>(DroHubFixture fixture, string serial_number, string
        user, string
        password) {
            var device_id = await getDeviceId(serial_number, user, password);
            var connection = await getLastConnection(fixture, device_id);
            using (var http_helper = await createLoggedInUser(user, password)) {
                var content = await http_helper.Response.Content.ReadAsStringAsync();
                var create_device_url = new Uri(DroHubFixture.SiteUri, $"DHub/Devices/Get{typeof(T)}s/{connection.Id}");
                http_helper.Response?.Dispose();
                var data_dic = new Dictionary<string, string>();
                var urlenc = new FormUrlEncodedContent(data_dic);
                http_helper.Response = await http_helper.Client.PostAsync(create_device_url, urlenc);
                var r = await http_helper.Response.Content.ReadAsStringAsync();
                return Newtonsoft.Json.JsonConvert.DeserializeObject<List<T>>(r);
            }
        }

        public static async ValueTask<Dictionary<string, string>> getApplicationToken(string user_name,
            string password) {
            var auth_token_uri = new Uri(DroHubFixture.SiteUri, "api/GetToken/GetApplicationToken");
            var content_to_send = new GetTokenController.GetTokenModel() {
                UserName = user_name,
                Password = password,
            };

            var http_helper = new HttpClientHelper();
            http_helper.Response = await http_helper.Client.PostAsJsonAsync(auth_token_uri, content_to_send);
            http_helper.Response.EnsureSuccessStatusCode();
            var res = await http_helper.Response.Content.ReadAsStringAsync();
            if (http_helper.Response.RequestMessage.RequestUri == auth_token_uri) {
                var deserialize_object = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,string>>(res);
                if (deserialize_object["result"] == "nok")
                    throw new InvalidCredentialException("Invalid credential");
                return deserialize_object;
            }

            throw new InvalidProgramException($"Unexpected Redirect... ");
        }

        public static Uri getAndroidActionUrl(string action_name) {
            return new Uri(DroHubFixture.SiteUri, $"api/AndroidApplication/{action_name}");;
        }

        private static async ValueTask<string> retrieveFromAndroidApp(string user,
            string token, string action_name, object query, bool post_json = true) {

            var auth_token_uri = getAndroidActionUrl(action_name);
            var http_helper = new HttpClientHelper();
            http_helper.Client.DefaultRequestHeaders.Add("x-drohub-user", user);
            http_helper.Client.DefaultRequestHeaders.Add("x-drohub-token", token);
            if (query != null) {
                if (post_json)
                    http_helper.Response = await http_helper.Client.PostAsJsonAsync(auth_token_uri, query);
                else {
                    http_helper.Response = await http_helper.Client.PostAsync(auth_token_uri, query as HttpContent);
                }
            }
            else {
                http_helper.Response = await http_helper.Client.GetAsync(auth_token_uri);
            }
            http_helper.Response.EnsureSuccessStatusCode();
            return await http_helper.Response.Content.ReadAsStringAsync();
        }

        public static async ValueTask<Dictionary<string, dynamic>> queryDeviceInfo(string user_name, string token,
            string device_serial_number) {

            var query = new AndroidApplicationController.QueryDeviceModel() {
                DeviceSerialNumber = device_serial_number
            };
            var res = await retrieveFromAndroidApp(user_name, token, "QueryDeviceInfo", query);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,dynamic>>(res);
        }

        public static string ValidateTokenActionName => "ValidateToken";

        public static async ValueTask<Dictionary<string, dynamic>> validateToken(string user_name, string token,
        double? version = null) {
            string res;
            if (version == null)
                res = await retrieveFromAndroidApp(user_name, token, ValidateTokenActionName, new object());
            else {
                res = await retrieveFromAndroidApp(user_name, token, ValidateTokenActionName,
                    new AndroidApplicationController.ValidateTokenModel {
                        Version = version.Value
                    });
            }
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,dynamic>>(res);
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