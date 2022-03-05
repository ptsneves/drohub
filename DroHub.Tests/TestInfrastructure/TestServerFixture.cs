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
using ConfigurationScanner;
using DroHub.Areas.DHub.Controllers;
using DroHub.Areas.Identity.Data;
using DroHub.Data;
using DroHub.ProgramExtensions;
using Ductus.FluentDocker.Common;
using Ductus.FluentDocker.Extensions;
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
        public IHostService Docker { get; }
        public const string AdminUserEmail = "admin@drohub.xyz";
        public static Uri SiteUri => new Uri("https://localhost");
        public static Uri ThriftUri => new Uri("wss://localhost/ws");
        public static Uri JanusUri => new Uri("https://localhost/janus");

        public static Uri TelemetryHubUri => new Uri("https://localhost:443/telemetryhub");
        public string TargetLiveStreamStoragePath { get; }
        public string AdminPassword { get; private set; }
        private static string DroHubTestsPath => Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory + "../../../");
        public static string DroHubPath => Path.GetFullPath(Path.Join(DroHubTestsPath, "../"));
        public static string FrontEndPathInRepo => "drohub-vue";
        public static string AppPathInRepo => "parrot-backend";
        public static string RPCAPIPathInRepo => "RPCInterfaces";
        public static string TestAssetsPath => Path.Join(DroHubTestsPath, "TestAssets");

        private static string PatchedDockerComposeFileName => Path.Join(DroHubPath, "docker-compose-test.yml");


        public static readonly int ALLOWED_USER_COUNT = 999;
        public static readonly string DEFAULT_ORGANIZATION = "UN";
        public static readonly string DEFAULT_DEVICE_NAME = "A Name";
        public static readonly string DEFAULT_BASE_TYPE = DroHubUser.SUBSCRIBER_POLICY_CLAIM;
        public static readonly string DEFAULT_DEVICE_SERIAL = "Aserial";
        public static readonly string DEFAULT_USER = "auser@drohub.xyz";
        public static readonly string DEFAULT_PASSWORD = "password1234";
        public static readonly int DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES = 999;
        public static readonly int DEFAULT_ALLOWED_USER_COUNT = 3;

        public IConfiguration Configuration { get; private set; }

        public double RPCAPIVersion { get; private set; }

        public DroHubContext DbContext { get; private set; }

        public TestServerFixture() {

            DeployedContainers = new Builder()
                            .UseContainer()
                            .UseCompose()
                            .FromFile(getPatchedDockerComposeFile())
                            .FromFile(Path.Join(DroHubPath, "docker-compose.test-services.yml"))
                            .RemoveOrphans()
                            .ForceRecreate()
                            .Build()
                            .Start();

            var counter = 0;
            while (true) {
                var task = Task.Run(() => {
                    using var handlerHttp = new HttpClientHandler {
                        ServerCertificateCustomValidationCallback =
                            (sender, cert, chain, sslPolicyErrors) => true
                    };
                    return new HttpClient(handlerHttp).GetAsync(SiteUri + "Identity/Account/Login");
                });
                try {
                    task.Wait(TimeSpan.FromMinutes(2));
                    var response = task.Result;
                    if (response.IsSuccessStatusCode)
                        break;
                }
                catch (AggregateException) {
                    // can happen if nginx is still setting up
                }

                if (counter > 60) {
                    throw new TimeoutException("Could not reach asp.net drohub backend");
                }
                Thread.Sleep(1000);
                counter++;
            }

            var hosts = new Hosts().Discover();
            Docker = hosts.First(x => x.IsNative);
            var web_container = DeployedContainers.Containers.First(c => c.Name == "drohub-web");
            using (var logs = Docker.Host.Logs(web_container.Id))
            {
                try {
                    AdminPassword = logs
                        .ReadToEnd()
                        .First(line => line.Contains("GENERATED ROOT PASSWORD"))
                        .Split(null).Last();
                }
                catch (InvalidOperationException) {

                    throw new InvalidProgramException(
                        $"If for some reason the logs were regenerated the GENERATED ROOT PASSWORD string "
                        + "will disappear and this will fail");
                }
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
            var builder = new ConfigurationBuilder().AddDroHub();

            Configuration = builder.Build()
                .ThrowOnConfiguredForbiddenToken();

            RPCAPIVersion = Configuration.GetValue<double>(AndroidApplicationController.APPSETTINGS_API_VERSION_KEY);
        }

        private void initializeDBContext() {
            var options = new DbContextOptionsBuilder<DroHubContext>()
                .UseMySql(Configuration.GetDroHubConnectionString())
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

        public class FileToBeUploaded {
            public string Source { get; set; }
            public bool IsPreview { get; set; }
        }

        public async Task testUpload(int half_duration_multiplier,
            string session_user,
            string session_password,
            string upload_user,
            string upload_password,
            Func<Dictionary<string, dynamic>, int, int, long, int, string, Task<UploadTestReturnEnum>> test,
            int runs = 1,
            string src = "video.webm",
            int chunks = 30,
            int copies = 1,
            bool is_preview = false,
            Func<Task> onConnectionClose = null) {
            var src_list = new FileToBeUploaded {
                Source = src,
                IsPreview = is_preview
            };
            await testUpload(half_duration_multiplier, session_user, session_password, upload_user,
                upload_password, test, onConnectionClose ?? (async () => { await Task.CompletedTask; }),new
                []{src_list},
                runs, chunks, copies);
        }

        public async Task testUpload(int half_duration_multiplier,
            string session_user,
            string session_password,
            string upload_user,
            string upload_password,
            Func<Dictionary<string,dynamic>, int, int, long, int, string, Task<UploadTestReturnEnum>> test,
            Func<Task> onConnectionClose,
            IEnumerable<FileToBeUploaded> src_list,
            int runs = 1,
            int chunks = 30,
            int copies = 1,
            int timestamp_srclist_spread_millis = 0) {

            var _src_list = src_list.ToList();

            var half_duration_seconds = TimeSpan.FromMilliseconds(4000);
            await HttpClientHelper.generateConnectionId(this, 2 * half_duration_seconds, "Aserial0",
                session_user,
                session_password,
                async connection => {
                    for (var src_index = 0; src_index < _src_list.Count(); src_index++) {
                        var src = _src_list[src_index];
                        for (var copy = 0; copy < copies; copy++) {
                            //Otherwise the value is considered local time
                            var date_time_in_range =
                                connection.StartTime
                                + half_duration_multiplier * half_duration_seconds * (copy + 1)
                                + TimeSpan.FromMilliseconds(timestamp_srclist_spread_millis) * src_index;

                            for (var i = 0; i < runs; i++) {
                                await using var stream = new FileStream($"{TestAssetsPath}/{src.Source}",
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
                                                    src.Source,
                                                    $"{TestAssetsPath}/{src.Source}"),
                                                IsPreview = src.IsPreview,
                                                DeviceSerialNumber = "Aserial0",
                                                UnixCreationTimeMS = date_time_in_range.ToUnixTimeMilliseconds(),
                                                AssembledFileSize = stream.Length,
                                                RangeStartBytes = stream.Length / chunks * chunk
                                            });

                                        switch (await test(r, i, chunk, stream.Length / chunks, copy, src.Source)) {
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
                                        await test(new Dictionary<string, dynamic> {
                                                ["error"] = e.Message
                                            },
                                            i, chunk, stream.Length / chunks, copy, src.Source);
                                        return;
                                    }
                                }
                            }
                        }
                    }

                    await onConnectionClose();
                });
        }

        private IEnumerable<IVolumeService> GetVolumes() {
            return DeployedContainers.Containers.SelectMany(c => {
                try {
                    return c.State == ServiceRunningState.Running ? c.GetVolumes() : new List<IVolumeService>();
                }
                catch (FluentDockerException) {
                    return new List<IVolumeService>();
                }
            });
        }

        public void Dispose() {
            var volumes = GetVolumes().ToList();
            Docker.Dispose();
            DeployedContainers.Stop();
            DeployedContainers.Remove(true);

            foreach (var volumeService in volumes) {
                volumeService.Remove();
            }
            File.Delete(PatchedDockerComposeFileName);
        }
    }
}