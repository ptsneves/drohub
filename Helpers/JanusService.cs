using System.Net.Http;
using System;
using Newtonsoft.Json;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace DroHub.Helpers {
    public class JanusServiceOptions
    {
        public JanusServiceOptions() {
            Address = "http://docker";
            Port = 8088;
            AdminKey = "NOTOKEN";
            RecordingPath = "/tmp/";
        }
        public string Address { get; set; }
        public int Port { get; set; }
        public string RecordingPath { get; set; }
        public string AdminKey { get; set; }
    }
    public class JanusService
    {
        public enum VideoCodecType
        {
            H264,
            VP8,
            VP9
        }

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

                [JsonProperty("videocodec")]
                public string VideoCodec { get; set; }

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

        private readonly HttpClient _client;
        private readonly ILogger<JanusService> _logger;
        private readonly JanusServiceOptions _options;
        public JanusService(HttpClient client,
            IOptionsMonitor<JanusServiceOptions> janus_service_options,
            ILogger<JanusService> logger)
        {
            _options = janus_service_options.CurrentValue;
            _client = client;
            client.BaseAddress = new Uri($"{_options.Address}:{_options.Port}/");
            client.Timeout = TimeSpan.FromSeconds(60);
            _logger = logger;
            _logger.LogDebug("Starting Janus Service with Options {_options}", JsonConvert.SerializeObject(_options, Formatting.Indented));
        }

        private async Task<JanusAnswer> getJanusAnswer(string relative_path, Object object_to_serialize) {
            JanusAnswer result;
            try {
                _logger.LogDebug("Request to be created {payload}", JsonConvert.SerializeObject(object_to_serialize,
                    Formatting.Indented));
                var payload = new StringContent(JsonConvert.SerializeObject(object_to_serialize), Encoding.UTF8,
                    "application/json");
                var response = await _client.PostAsync(relative_path, payload);
                response.EnsureSuccessStatusCode();
                var res_string = await response.Content.ReadAsStringAsync();
                result =
                    Newtonsoft.Json.JsonConvert.DeserializeObject<JanusAnswer>(res_string);
                _logger.LogDebug("Janus answer result {result}",
                    JsonConvert.SerializeObject(res_string, Formatting.Indented));
            }
            catch (Exception e) {
                _logger.LogCritical($"Error in Janus Answer {e.GetType()}  \n{e.Message}\n{e.StackTrace}");
                throw new JanusServiceException(e.Message);
            }

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

        public async Task destroyVideoRoom(CreateSession session, Int64 handle, Int64 stream_id) {
            var request = new JanusRequest(session, new JanusRequest.DestroyRequest(_options.AdminKey, stream_id));
            await getJanusAnswer($"/janus/{session.Id}/{handle}", request);
        }

        public async Task startStream(CreateSession session, Int64 handle, Int64 stream_id) {
            var request = new JanusRequest(session, new JanusRequest.StartRequest(_options.AdminKey, stream_id));
            await getJanusAnswer($"/janus/{session.Id}/{handle}", request);
        }
        public async Task<JanusService.VideoRoomEndPoint> createVideoRoom(CreateSession session, Int64 handle, Int64 id,
                string description, string secret, Int64 max_publishers, VideoCodecType video_codec) {
            var request = new JanusRequest(session, new JanusRequest.CreateVideoRoomRequest(_options.AdminKey, id, max_publishers)
            {
                Description = description,
                Record = true,
                RecordingDir = $"{_options.RecordingPath}",
                VideoCodec = video_codec.ToString("g").ToLower(),
                // Secret = secret
            });

            var result = await getJanusAnswer($"/janus/{session.Id}/{handle}", request);
            _logger.LogDebug("Janus answer {result}", JsonConvert.SerializeObject(result.PluginData.StreamingPluginData));
            if (!String.IsNullOrEmpty(result.PluginData.StreamingPluginData.Error))
            {
                if (result.PluginData.StreamingPluginData.Error != $"Room {id} already exists")
                {
                    _logger.LogDebug("Failed to create janus video room");
                    throw new JanusServiceException(JsonConvert.SerializeObject(result.PluginData.StreamingPluginData.Error));
                }
            }

            return new JanusService.VideoRoomEndPoint
            {
                Id = id,
                Secret = null
            };
        }
    }
}