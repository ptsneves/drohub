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
            AdminKey = "NOTOKEN";
            RTPPortStart = 6004;
            RTPPortEnd = 6005;
            RecordingPath = "/tmp/";
        }
        public string Address { get; set; }
        public int Port { get; set; }
        public int RTPPortStart { get; set; }
        public int RTPPortEnd { get; set; }
        public string RecordingPath { get; set; }
        [JsonIgnore]
        public IEnumerable<int> RTPPortRange {
            get {
                return Enumerable.Range(RTPPortStart, RTPPortEnd);
            }
        }
        public string AdminKey { get; set; }
    }
    public class JanusService
    {
        public class JanusServiceException : Exception
        {
            public JanusServiceException(string message): base(message) { }
        }
        public class VideoRoomEndPoint
        {
            public Int64 Id { get; set; }
            public String Secret { get; set; }
        }

        public class JanusBasicSession
        {
            [JsonProperty("transaction")]
            public Guid TransactionId { get; set; }
            [JsonProperty("janus")]
            public virtual string JanusAction { get; set; }
        }
        internal class JanusAnswer : JanusBasicSession
        {
            internal class JanusData
            {
                [JsonProperty("id")]
                public Int64 Id { get; set; }
            }
            internal class JanusError
            {
                [JsonProperty("code")]
                Int64 Code { get; set; }
                [JsonProperty("reason")]
                string Reason { get; set; }
            }
            internal class JanusPluginData {
                internal class JanusStreamingPluginData{

                    [JsonProperty("videoroom")]
                    public string PluginName { get; set; }

                    [JsonProperty("error_code")]
                    public string ErrorCode { get; set; }

                    [JsonProperty("error")]
                    public string Error { get; set; }

                }
                [JsonProperty("plugin")]
                public string PluginName { get; set; }

                [JsonProperty("data")]
#pragma warning disable CS0649
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
        internal class JanusRequest : JanusBasicSession
        {
            internal JanusRequest(CreateSession session, IBody body_message) {
                TransactionId = session.TransactionId;
                Body = body_message;
            }

            internal interface IBody { }
            internal class MessageBody : IBody {

                [JsonProperty("request")]
                public virtual string Request { get; set; }
                [JsonProperty("admin_key")]
                public string AdminKey { get;}
                public MessageBody(string admin_key) { AdminKey = admin_key; }
            }


            internal class MessageWithId : MessageBody {
                public MessageWithId(string admin_key, Int64 stream_id) : base(admin_key) { StreamId = stream_id; }
                [JsonProperty("room")]
                public Int64 StreamId { get; set; }
            }

            [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
            internal class CreateVideoRoomRequest : MessageWithId
            {
                public CreateVideoRoomRequest(string admin_key, Int64 stream_id, Int64 max_publishers) : base(admin_key, stream_id) {
                    Publishers = max_publishers;
                }
                public override string Request { get { return "create"; } }

                [JsonProperty("permanent")]
                public bool Permanent { get; set; }

                [JsonProperty("description")]
                public string Description { get; set; }

                [JsonProperty("secret")]
                public string Secret { get; set; }

                [JsonProperty("pin")]
                public string Pin { get; set; }

                [JsonProperty("is_private")]
                public bool IsPrivate { get; set; }

                [JsonProperty("require_pvtid")]
                public bool RequirePvtId { get; set; }

                [JsonProperty("publishers")]
                public Int64 Publishers { get; set; }

                [JsonProperty("bitrate")]

                //...
                public Int64 Bitrate { get; set; }

                [JsonProperty("record")]
                public bool Record { get; set; }

                [JsonProperty("rec_dir")]
                public string RecordingDir { get; set; }

                [JsonProperty("notify_joining")]
                public bool NotifyJoining { get; set; }
            }


            internal class DestroyRequest : MessageWithId {
                public DestroyRequest(string admin_key, Int64 stream_id) : base(admin_key, stream_id) { }
                public override string Request { get { return "destroy"; } }
            }

            internal class InfoRequest : MessageWithId {
                public InfoRequest(string admin_key, Int64 stream_id) : base(admin_key, stream_id) { }
                public override string Request { get { return "info"; } }
            }
            internal class WatchRequest : MessageWithId {
                public WatchRequest(string admin_key, Int64 stream_id) : base(admin_key, stream_id) { }
                public override string Request { get { return "watch"; } }
            }
            internal class StartRequest : MessageWithId {
                public StartRequest(string admin_key, Int64 stream_id) : base(admin_key, stream_id) { }
                public override string Request { get { return "start"; } }
            }
            internal class StopRequest : MessageWithId {
                public StopRequest(string admin_key, Int64 stream_id) : base(admin_key, stream_id) { }
                public override string Request { get { return "stop"; } }
            }

            internal class ListRequest : MessageBody {
                public override string Request { get { return "list"; } }
                public ListRequest(string admin_key) : base(admin_key) { }
            }


            internal class StartRecordingRequest : MessageWithId
            {
                public StartRecordingRequest(string admin_key, Int64 stream_id) : base(admin_key, stream_id)
                {
                }

                public override string Request { get { return "recording"; } }

                [JsonProperty("action")]
                public string Action {get { return "start"; } }

                [JsonProperty("audio", NullValueHandling = NullValueHandling.Ignore)]
                public string AudioPath { get { return null; } }

                [JsonProperty("video", NullValueHandling = NullValueHandling.Ignore)]
                public string VideoPath { get; set; }

                [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
                public string DataPath { get { return null; } }
            }

            internal class StopRecordingRequest : MessageWithId
            {
                public StopRecordingRequest(string admin_key, Int64 stream_id) : base(admin_key, stream_id)
                {

                }

                public override string Request { get { return "recording"; } }

                [JsonProperty("action")]
                public string Action { get { return "stop"; } }

                [JsonProperty("audio")]
                public bool StopAudio { get { return true; } }

                [JsonProperty("video")]
                public bool StopVideo { get { return true; } }

                [JsonProperty("data")]
                public bool StopData { get { return true; } }
            }

            public override string JanusAction {get { return "message"; } }
            [JsonProperty("body")]
            public IBody Body { get; set; }
        }

        internal class CreateVideoRoomPluginHandler : JanusBasicSession {
            [JsonProperty("plugin")]
            public string Plugin { get { return "janus.plugin.videoroom"; } }
            public override string JanusAction {get {return "attach"; } }

            public CreateVideoRoomPluginHandler(CreateSession session) {
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
            _logger.LogDebug("Request to be created {payload}", JsonConvert.SerializeObject(object_to_serialize,
                Formatting.Indented));
            var payload = new StringContent(JsonConvert.SerializeObject(object_to_serialize), Encoding.UTF8, "application/json");
            var response = await Client.PostAsync(relative_path, payload);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsAsync<JanusAnswer>();
            _logger.LogDebug("Janus answer result {result}", JsonConvert.SerializeObject(
                    await response.Content.ReadAsStringAsync(), Formatting.Indented));

            if (result.JanusAnswerStatus != "success" && result.JanusAnswerStatus != "ack")
                throw new JanusServiceException("Janus service failed an action");
            return result;
        }
        public async Task<CreateSession> createSession() {
            var session = new CreateSession();
            await getJanusAnswer("/janus", session);
            return session;
        }

        public async Task<Int64> createStreamerPluginHandle(CreateSession session) {
            var handle = new CreateVideoRoomPluginHandler(session);
            return (await getJanusAnswer($"/janus/{session.Id}", handle)).Data.Id;
        }

        public async Task startRecording(CreateSession session, Int64 handle, Int64 stream_id, string video_file_name) {
            var request = new JanusRequest(session, new JanusRequest.StartRecordingRequest(_options.AdminKey, stream_id)
            {
                VideoPath = $"{_options.RecordingPath}/{video_file_name}"
            });
            _logger.LogInformation($"Started recording video to {$"{_options.RecordingPath}/{video_file_name}"}");
            await getJanusAnswer($"/janus/{session.Id}/{handle}", request);
        }

        public async Task destroyVideoRoom(CreateSession session, Int64 handle, Int64 stream_id) {
            var request = new JanusRequest(session, new JanusRequest.DestroyRequest(_options.AdminKey, stream_id));
            await getJanusAnswer($"/janus/{session.Id}/{handle}", request);
        }

        public async Task startStream(CreateSession session, Int64 handle, Int64 stream_id) {
            var request = new JanusRequest(session, new JanusRequest.StartRequest(_options.AdminKey, stream_id));
            await getJanusAnswer($"/janus/{session.Id}/{handle}", request);
        }

        public async Task stopRecording(CreateSession session, Int64 handle, Int64 stream_id)
        {
            var request = new JanusRequest(session, new JanusRequest.StopRecordingRequest(_options.AdminKey, stream_id));
            await getJanusAnswer($"/janus/{session.Id}/{handle}", request);
        }

        public async Task<JanusService.VideoRoomEndPoint> createVideoRoom(CreateSession session, Int64 handle, Int64 id,
                string description, string secret, Int64 max_publishers) {
            var rand = new Random();
            int video_port = rand.Next(_options.RTPPortStart, _options.RTPPortEnd);
            var request = new JanusRequest(session, new JanusRequest.CreateVideoRoomRequest(_options.AdminKey, id, max_publishers)
            {
                Description = description,
                // Secret = secret
            });
            var result = (await getJanusAnswer($"/janus/{session.Id}/{handle}", request));
            _logger.LogDebug("Janus answer {result}", JsonConvert.SerializeObject(result.PluginData.StreamingPluginData));
            if (!String.IsNullOrEmpty(result.PluginData.StreamingPluginData.Error))
            {
                _logger.LogDebug("Failed to create janus video room");
                throw new JanusServiceException(JsonConvert.SerializeObject(result.PluginData.StreamingPluginData.Error));
            }

            return new JanusService.VideoRoomEndPoint
            {
                Id = id,
                Secret = null
            };
        }
    }
}