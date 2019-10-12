using System.Net.Http;
using System;
using Newtonsoft.Json;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace DroHub.Helpers {
    public class JanusServiceOptions
    {
        public JanusServiceOptions() {
            Address = "http://docker";
            Port = 8088;
            Token = "NOTOKEN";
            RTPPortStart = 6004;
            RTPPortEnd = 6005;
        }
        public string Address { get; set; }
        public int Port { get; set; }
        public int RTPPortStart { get; set; }
        public int RTPPortEnd { get; set; }
        [JsonIgnore]
        public IEnumerable<int> RTPPortRange {
            get {
                return Enumerable.Range(RTPPortStart, RTPPortEnd);
            }
        }
        public string Token { get; set; }
    }
    public class JanusService
    {
        public class JanusBasicSession
        {
            [JsonProperty("transaction")]
            public Guid TransactionId { get; set; }
            [JsonProperty("janus")]
            public virtual string JanusAction { get; set; }
        }
        public class JanusAnswer : JanusBasicSession
        {
            public class JanusData
            {
                [JsonProperty("id")]
                public Int64 Id { get; set; }
            }
            public class JanusError
            {
                [JsonProperty("code")]
                Int64 Code { get; set; }
                [JsonProperty("reason")]
                string Reason { get; set; }
            }
            public class JanusPluginData {
                public class JanusStreamingPluginData{
                    public class JanusStreamListInfo {
                        [JsonProperty("id")]
                        public Int64 Id { get; set; }
                        [JsonProperty("description")]
                        public string Description { get; set; }
                        [JsonProperty("type")]
                        public string Type { get; set; }
                        private TimeSpan _video_age;
                        public TimeSpan VideoAge{ get; set; }
                        [JsonProperty("video_age_ms")]
                        public Int64 VideoAgeInMs { set { _video_age = TimeSpan.FromMilliseconds(value); } }
                    }
                    [JsonProperty("streaming")]
                    public string Streaming { get; set; }
                    [JsonProperty("list")]
                    public List<JanusStreamListInfo> Streams { get; set; }
                    [JsonProperty("stream")]
                    public JanusStreamListInfo Stream { get; set; }

                    [JsonProperty("info")]
                    public JanusRequest.RTPMountPointInfo RTPMountPointInfo { get; set; }

                }
                [JsonProperty("plugin")]
                public string PluginName { get; set; }
                [JsonProperty("data")]
                public JanusStreamingPluginData StreamingPluginData;
            }

            [JsonProperty("error")]
            public JanusError Error {get; set;}

            [JsonProperty("data")]
            public JanusData Data { get; set; }

            [JsonProperty("plugindata")]
            public JanusPluginData PluginData { get; set; }
        }
        public class CreateSession : JanusBasicSession
        {
           [JsonProperty("id")]
            public Int64 Id { get; set; }
        }
        public class JanusRequest : JanusBasicSession
        {
            public interface IBody {

            }
            public class MessageBody : IBody {

                [JsonProperty("request")]
                public virtual string Request { get; set; }

            }
            public class MessageWithId : MessageBody {
                [JsonProperty("id")]
                public Int64 StreamId { get; set; }
            }

            public class DestroyRequest : MessageWithId {
                public override string Request { get { return "destroy"; } }
            }

            public class InfoRequest : MessageWithId {
                public override string Request { get { return "info"; } }
            }
            public class ListRequest : MessageBody {
                public override string Request { get { return "list"; } }
            }

            [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
            public class RTPMountPointInfo : MessageBody {

                    // [Required(ErrorMessage = "Secret is required.")]
                    [JsonProperty("secret")]
                    public string Secret { get; set; }

                    [JsonProperty("type")]
                    public string Type { get { return "rtp"; } }
                    [JsonProperty("audio")]
                    public bool Audio { get; set; }
                    [JsonProperty("audioport")]
                    public int AudioPort { get; set; }
                    [JsonProperty("audiopt")]
                    public int AudioPt { get; set; }

                    [JsonProperty("audiortpmap")]
                    public string AudioRTPMap { get; set; }

                    [JsonProperty("video")]
                    public bool Video { get; set; }
                    [JsonProperty("videoport")]
                    public int VideoPort { get; set; }
                    [JsonProperty("videopt")]
                    public int VideoPt { get; set; }

                    [JsonProperty("videortpmap")]
                    public string VideoRTPMap { get; set; }
                    [JsonProperty("videofmtp")]
                    public string VideoFMTProfile { get; set; }

                    [Required(ErrorMessage = "Description is required.")]
                    [JsonProperty("description")]
                    public string Description { get; set; }
            }

            public override string JanusAction {get { return "message"; } }
            [JsonProperty("body")]
            public IBody Body { get; set; }
        }
        public class CreateStreamerPluginHandler : JanusBasicSession {
            [JsonProperty("plugin")]
            public string Plugin { get { return "janus.plugin.streaming"; } }
        }

        public HttpClient Client { get; }
        private readonly ILogger<JanusService> _logger;
        private readonly JanusServiceOptions _options;
        public JanusService(HttpClient client,
            IOptionsMonitor<JanusServiceOptions> janus_service_options,
            ILogger<JanusService> logger)
        {
            _options = janus_service_options.CurrentValue;
            client.BaseAddress = new Uri($"{_options.Address}:{_options.Port}/");
            client.Timeout = TimeSpan.FromSeconds(4);
            Client = client;
            _logger = logger;
            _logger.LogDebug("Starting Janus Service with Options {_options}", JsonConvert.SerializeObject(_options, Formatting.Indented));
        }

        public async Task<CreateSession> createSession(Int64 id) {
            var session = new CreateSession
            {
                TransactionId = Guid.NewGuid(),
                JanusAction = "create",
                Id = id
            };
            var payload = new StringContent(JsonConvert.SerializeObject(session), Encoding.UTF8, "application/json");
            _logger.LogDebug("Session to created {payload}", JsonConvert.SerializeObject(session, Formatting.Indented));
            var response = await Client.PostAsync("/janus", payload);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsAsync<JanusAnswer>();
            _logger.LogDebug("Session creation result {result}", JsonConvert.SerializeObject(result, Formatting.Indented));
            return session;
        }

        public async Task<Int64> createHandle(CreateSession session) {
            var handle = new CreateStreamerPluginHandler
            {
                TransactionId = session.TransactionId,
                JanusAction = "attach",
            };
            var payload = new StringContent(JsonConvert.SerializeObject(handle, Formatting.Indented), Encoding.UTF8, "application/json");
            _logger.LogDebug("Handle to created {payload}", JsonConvert.SerializeObject(handle, Formatting.Indented));
            var response = await Client.PostAsync($"/janus/{session.Id}", payload);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsAsync<JanusAnswer>();
            _logger.LogDebug("Handle creation result {result}", await response.Content.ReadAsStringAsync());
            return result.Data.Id;
        }

        public async Task<List<JanusAnswer.JanusPluginData.JanusStreamingPluginData.JanusStreamListInfo>> listMountPoints(
                CreateSession session, Int64 handle) {
            var request = new JanusRequest
            {
                TransactionId = session.TransactionId,
                Body = new JanusRequest.ListRequest {}
            };
            var payload = new StringContent(JsonConvert.SerializeObject(request, Formatting.Indented), Encoding.UTF8, "application/json");
            _logger.LogDebug("List request message {payload}", JsonConvert.SerializeObject(request, Formatting.Indented));
            var response = await Client.PostAsync($"/janus/{session.Id}/{handle}", payload);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsAsync<JanusAnswer>();
            _logger.LogDebug("List request result {result}", await response.Content.ReadAsStringAsync());
            return result.PluginData.StreamingPluginData.Streams;
        }
        public async Task<JanusAnswer> destroyMountPoint(CreateSession session, Int64 handle, Int64 stream_id) {
            var request = new JanusRequest
            {
                TransactionId = session.TransactionId,
                Body = new JanusRequest.DestroyRequest {
                    StreamId = stream_id
                }
            };
            var payload = new StringContent(JsonConvert.SerializeObject(request, Formatting.Indented), Encoding.UTF8, "application/json");
            _logger.LogDebug("List request message {payload}", JsonConvert.SerializeObject(request, Formatting.Indented));
            var response = await Client.PostAsync($"/janus/{session.Id}/{handle}", payload);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsAsync<JanusAnswer>();
            _logger.LogDebug("List request result {result}", await response.Content.ReadAsStringAsync());
            return result;
        }

        public async Task destroyMountPoints(CreateSession session, Int64 handle, string with_description) {
            var list = await listMountPoints(session, handle);
            foreach (var stream in list) {
                if (stream.Description.Contains(with_description)) {
                    destroyMountPoint(session, handle, stream.Id);
                }
            }
        }

        public async Task<JanusAnswer> createRTPVideoMountPoint(CreateSession session, Int64 handle, JanusRequest.RTPMountPointInfo option) {
            if (! _options.RTPPortRange.Contains(option.VideoPort))
                throw new InvalidOperationException(
                    $"Port {option.VideoPort} is not contained between the allowed {_options.RTPPortStart} and {_options.RTPPortEnd} ports."
                );

            option.Request = "create";
            var request = new JanusRequest
            {
                TransactionId = session.TransactionId,
                Body = option
            };
            var payload = new StringContent(JsonConvert.SerializeObject(request, Formatting.Indented), Encoding.UTF8, "application/json");
            _logger.LogDebug("request message to add RTP Mountpoint {payload}", JsonConvert.SerializeObject(request, Formatting.Indented));
            var response = await Client.PostAsync($"/janus/{session.Id}/{handle}", payload);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsAsync<JanusAnswer>();
            _logger.LogDebug("request result for RTP mountpoint adding {result}", await response.Content.ReadAsStringAsync());
            return result;
        }

        public async Task<JanusAnswer> getStreamInfo(CreateSession session, Int64 handle, Int64 stream_id)
        {
            var request = new JanusRequest
            {
                TransactionId = session.TransactionId,
                Body = new JanusRequest.InfoRequest
                {
                    StreamId = stream_id
                }
            };
            var payload = new StringContent(JsonConvert.SerializeObject(request, Formatting.Indented), Encoding.UTF8, "application/json");
            _logger.LogDebug("Info request message {payload}", JsonConvert.SerializeObject(request, Formatting.Indented));
            var response = await Client.PostAsync($"/janus/{session.Id}/{handle}", payload);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsAsync<JanusAnswer>();
            _logger.LogDebug("Info request result {result}", await response.Content.ReadAsStringAsync());
            return result;
        }

        public async Task<IEnumerable<int>> getAvailableMountPointPorts() {
            var session = await createSession();
            var handle = await createHandle(session);
            var list = await listMountPoints(session, handle);
            var not_available = new List<int>();
            foreach (var stream in list) {
                var info = await getStreamInfo(session, handle, stream.Id);
                not_available.Add(info.PluginData.StreamingPluginData.RTPMountPointInfo.VideoPort);
            }
            return _options.RTPPortRange.Where(i => ! not_available.Contains(i));
        }
    }
}