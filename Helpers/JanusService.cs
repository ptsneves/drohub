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
            [JsonProperty("janus")]
            public string JanusAnswerStatus { get; set; }
        }
        public class CreateSession : JanusBasicSession
        {
            [JsonProperty("id")]
            public Int64 Id { get; set; }
            public override string JanusAction {
                get {return "create";}
            }
            public CreateSession() {
                Random random = new Random();
                TransactionId = Guid.NewGuid();
                JanusAction = "create";
                Id = random.NextLong(0, Int64.MaxValue);
            }
        }
        public class JanusRequest : JanusBasicSession
        {
            public JanusRequest(CreateSession session, IBody body_message) {
                TransactionId = session.TransactionId;
                Body = body_message;
            }

            public interface IBody { }
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
            public override string JanusAction {get {return "attach"; } }

            public CreateStreamerPluginHandler(CreateSession session) {
                TransactionId = session.TransactionId;
            }
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

        private async Task<JanusAnswer> getJanusAnswer(string relative_path, Object object_to_serialize) {
            _logger.LogDebug("Session to be created {payload}", JsonConvert.SerializeObject(object_to_serialize,
                Formatting.Indented));
            var payload = new StringContent(JsonConvert.SerializeObject(object_to_serialize), Encoding.UTF8, "application/json");
            var response = await Client.PostAsync(relative_path, payload);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsAsync<JanusAnswer>();
            _logger.LogDebug("Janus answer result {result}", JsonConvert.SerializeObject(result, Formatting.Indented));
            if (result.JanusAnswerStatus != "success")
                throw new ApplicationException("Janus service failed an action");
            return result;
        }
        public async Task<CreateSession> createSession() {
            var session = new CreateSession();
            await getJanusAnswer("/janus", session);
            return session;
        }

        public async Task<Int64> createStreamerPluginHandle(CreateSession session) {
            var handle = new CreateStreamerPluginHandler(session);
            return (await getJanusAnswer($"/janus/{session.Id}", handle)).Data.Id;
        }

        public async Task<List<JanusAnswer.JanusPluginData.JanusStreamingPluginData.JanusStreamListInfo>> listMountPoints(
                CreateSession session, Int64 handle) {
            var request = new JanusRequest(session, new JanusRequest.ListRequest());
            return (await getJanusAnswer($"/janus/{session.Id}/{handle}", request)).PluginData.StreamingPluginData.Streams;
        }
        public async Task<JanusAnswer> destroyMountPoint(CreateSession session, Int64 handle, Int64 stream_id) {
            var request = new JanusRequest(session, new JanusRequest.DestroyRequest{
                    StreamId = stream_id
                }
            );
            return await getJanusAnswer($"/janus/{session.Id}/{handle}", request);
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
            var request = new JanusRequest(session, option);
            return await getJanusAnswer($"/janus/{session.Id}/{handle}", request);
        }

        public async Task<JanusAnswer> getStreamInfo(CreateSession session, Int64 handle, Int64 stream_id)
        {
            var request = new JanusRequest(session, new JanusRequest.InfoRequest{
                    StreamId = stream_id
                }
            );
            return await getJanusAnswer($"/janus/{session.Id}/{handle}", request);
        }

        public async Task<IEnumerable<int>> getAvailableMountPointPorts() {
            var session = await createSession();
            var handle = await createStreamerPluginHandle(session);
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