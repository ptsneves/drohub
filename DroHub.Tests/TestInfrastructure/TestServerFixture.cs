using System;
using System.Collections.Generic;
using System.Globalization;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Commands;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Parser;
using DroHub.Areas.DHub.Controllers;
using DroHub.Data;
using Ductus.FluentDocker.Services.Extensions;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using YamlDotNet.RepresentationModel;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace DroHub.Tests.TestInfrastructure
{
    [UsedImplicitly]
    public class TestServerFixture : IDisposable
    {
        public ICompositeService DeployedContainers { get; }
        private IHostService Docker { get; }
        public const string AdminUserEmail = "admin@drohub.xyz";
        public static Uri SiteUri => new Uri("https://master/");
        public static Uri ThriftUri => new Uri("wss://master/ws");
        public static Uri JanusUri => new Uri("https://master/janus");

        public static Uri TelemetryHubUri => new Uri("https://master:443/telemetryhub");
        public string TargetLiveStreamStoragePath { get; }
        public string AdminPassword { get; private set; }
        private static string DroHubTestsPath => Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory + "../../../");
        public static string DroHubPath => Path.GetFullPath(Path.Join(DroHubTestsPath, "../"));
        public static string FrontEndPathInRepo => "drohub-vue";
        public static string AppPathInRepo => "parrot-backend";
        public static string RPCAPIPathInRepo => "RPCInterfaces";
        public static string TestAssetsPath => Path.Join(DroHubTestsPath, "TestAssets");

        private static string PatchedDockerComposeFileName => Path.Join(DroHubPath, "docker-compose-test.yml");

        public IConfiguration Configuration { get; private set; }

        public double RPCAPIVersion { get; private set; }

        public DroHubContext DbContext { get; private set; }

        public TestServerFixture() {

            DeployedContainers = new Builder()
                            .UseContainer()
                            .UseCompose()
                            .FromFile(getPatchedDockerComposeFile())
                            .RemoveOrphans()
                            .Wait("nginx", (service, i) => {

                                while (true) {
                                    using var handlerHttp = new HttpClientHandler {
                                        ServerCertificateCustomValidationCallback =
                                            (sender, cert, chain, sslPolicyErrors) => true
                                    };
                                    using var client = new HttpClient(handlerHttp);
                                    var response = client.GetAsync(SiteUri).Result;

                                    if (response.IsSuccessStatusCode)
                                        return 0;
                                    Thread.Sleep(1000);
                                }
                            } )
                            // .ForceBuild()
                            .Build()
                            .Start();

            var hosts = new Hosts().Discover();
            Docker = hosts.First(x => x.IsNative);
            var web_container = DeployedContainers.Containers.First(c => c.Name == "web");
            using (var logs = Docker.Host.Logs(web_container.Id))
            {
                AdminPassword = logs
                    .ReadToEnd()
                    .First(line => line.Contains("GENERATED ROOT PASSWORD"))
                    .Split(null).Last();
            }

            initializeConfiguration(web_container);
            initializeDBContext();

            var live_video_mount = web_container
                .GetConfiguration().Mounts
                .Single(m => m.Source.Contains("live-video-storage"));
            TargetLiveStreamStoragePath = live_video_mount.Destination + Path.DirectorySeparatorChar;
            Console.WriteLine("Ready to start");
        }

        private string getPatchedDockerComposeFile() {
            var docker_compose_file = Path.Join(DroHubPath, "docker-compose.yml");
            var stream = new StringReader(File.OpenText(docker_compose_file).ReadToEnd());
            var yaml = new YamlStream();
            yaml.Load(stream);
            var root_node = (YamlMappingNode)yaml.Documents[0].RootNode;

            ((YamlMappingNode) root_node["services"]).Children.Remove("letsencrypt");

            var patched_file = PatchedDockerComposeFileName;
            using var o = new StreamWriter(patched_file);
            yaml.Save(o, false);

            return patched_file;
        }

        private void initializeConfiguration(IContainerService web_container) {
            var app_settings_result = web_container.Execute("cat /app/appsettings.json");
            string json_string;
            if (!app_settings_result.Success) {
                if (File.Exists(DroHubPath + "appsettings.Development.json")) {
                    json_string = File.ReadAllText(DroHubPath + "appsettings.Development.json");
                }
                else
                    throw new InvalidProgramException("Cannot read appsettings file for tests. Exiting");
            }
            else {
                var appsettings_data = app_settings_result.Data;
                json_string = appsettings_data.Aggregate("", (current, item) => current + item);
            }
            var json_stream = new MemoryStream();
            json_stream.Write(Encoding.UTF8.GetBytes(json_string));
            json_stream.Seek(0, SeekOrigin.Begin);
            var builder = new ConfigurationBuilder()
                .AddJsonStream(json_stream);

            Configuration = builder.Build();
            RPCAPIVersion = Configuration.GetValue<double>(AndroidApplicationController.APPSETTINGS_API_VERSION_KEY);
        }

        private void initializeDBContext() {
            var db_provider = Configuration.GetValue<string>(Program.DATABASE_PROVIDER_KEY);
            var db_connection_string = Configuration
                .GetConnectionString(db_provider);

            var options = new DbContextOptionsBuilder<DroHubContext>()
                .UseMySql(db_connection_string)
                .Options;
            DbContext = new DroHubContext(options);
        }

        public static AngleSharp.Html.Dom.IHtmlDocument getHtmlDOM(string responseBody)
        {
            var context = BrowsingContext.New(AngleSharp.Configuration.Default);
            var parser = context.GetService<IHtmlParser>();
            return parser.ParseDocument(responseBody);
        }

        public static string toCamelCase(string str)
        {
            var pattern = new Regex(@"[A-Z]{2,}(?=[A-Z][a-z]+[0-9]*|\b)|[A-Z]?[a-z]+[0-9]*|[A-Z]|[0-9]+");
            return new string(
                new CultureInfo("en-US", false)
                    .TextInfo
                    .ToTitleCase(
                        string.Join(" ", pattern.Matches(str)).ToLower()
                    )
                    .Replace(@" ", "")
                    .Select((x, i) => i == 0 ? char.ToLower(x) : x)
                    .ToArray()
            );
        }

        public static string getVerificationToken(string responseBody,
            string selector = "input[name='__RequestVerificationToken']",
            string attribute = "value")
        {
            var document = getHtmlDOM(responseBody);
            return document
                .QuerySelectorAll(selector)
                .First()
                .GetAttribute(attribute);
        }

        public static string computeFileSHA256(string filePath) {
            using var file_stream = File.OpenRead(filePath);
            return Convert.ToBase64String(SHA256.Create().ComputeHash(file_stream));
        }

        public static string getGradleArgument(string property_name, string value) {
            return $"-Pandroid.testInstrumentationRunnerArguments.{property_name}={value}";
        }

        public enum UploadTestReturnEnum {
            CONTINUE,
            SKIP_RUN,
            STOP_UPLOAD
        }

        public async Task testUpload(int half_duration_multiplier,
            string session_user,
            string session_password,
            string upload_user,
            string upload_password,
            Func<Dictionary<string, dynamic>, int, int, long, int, UploadTestReturnEnum> test,
            int runs = 1,
            string src = "video.webm",
            int chunks = 30,
            int copies = 1) {
            await testUpload(half_duration_multiplier, session_user, session_password, upload_user,
                upload_password, test, () => { }, runs, src, chunks, copies);
        }

        public async Task testUpload(int half_duration_multiplier,
            string session_user,
            string session_password,
            string upload_user,
            string upload_password,
            Func<Dictionary<string,dynamic>, int, int, long, int, UploadTestReturnEnum> test,
            Action onConnectionClose,
            int runs = 1,
            string src = "video.webm",
            int chunks = 30,
            int copies = 1) {

            var half_duration_seconds = TimeSpan.FromMilliseconds(4000);
            await HttpClientHelper.generateConnectionId(this, 2 * half_duration_seconds, "Aserial0",
                session_user,
                session_password,
                async connection => {

                    for (var copy = 0; copy < copies; copy++) {
                        //Otherwise the value is considered local time
                        var date_time_in_range =
                            connection.StartTime + half_duration_multiplier * half_duration_seconds * (copy+1);

                        for (var i = 0; i < runs; i++) {
                            await using var stream = new FileStream($"{TestServerFixture.TestAssetsPath}/{src}",
                                FileMode.Open);
                            for (var chunk = 0; chunk < chunks; chunk++) {
                                try {
                                    var amount_send = stream.Length / chunks;
                                    if (chunk == chunks - 1)
                                        amount_send = stream.Length - stream.Length / chunks * chunk;
                                    var r = await HttpClientHelper.uploadMedia(upload_user,
                                        upload_password,
                                        new AndroidApplicationController.UploadModel {
                                            File = new FormFile(stream, stream.Length / chunks * chunk, amount_send,
                                                src,
                                                $"{TestAssetsPath}/{src}"),
                                            IsPreview = false, //needs to be because preview files have different paths
                                            DeviceSerialNumber = "Aserial0",
                                            UnixCreationTimeMS = date_time_in_range.ToUnixTimeMilliseconds(),
                                            AssembledFileSize = stream.Length,
                                            RangeStartBytes = stream.Length / chunks * chunk
                                        });

                                    switch (test(r, i, chunk, stream.Length / chunks, copy)) {
                                        case UploadTestReturnEnum.CONTINUE:
                                            continue;
                                        case UploadTestReturnEnum.SKIP_RUN:
                                            chunk = chunks; //short circuit
                                            break;
                                        case UploadTestReturnEnum.STOP_UPLOAD:
                                            return;
                                        default:
                                            throw new ArgumentOutOfRangeException();
                                    }
                                }
                                catch (HttpRequestException e) {
                                    test(new Dictionary<string, dynamic> {
                                            ["error"] = e.Message
                                        },
                                        i, chunk, stream.Length / chunks, copy);
                                    return;
                                }
                            }
                        }
                    }

                    onConnectionClose();
                });
        }


        public void Dispose() {
            Docker.Dispose();
            Containers.Dispose();
            File.Delete(PatchedDockerComposeFileName);
        }
    }
}