using System;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Extensions;
using Ductus.FluentDocker.Commands;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using AngleSharp;
using AngleSharp.Html.Parser;
using DroHub.Data;
using Ductus.FluentDocker.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using YamlDotNet.RepresentationModel;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace DroHub.Tests.TestInfrastructure
{
    public class DroHubFixture : IDisposable
    {
        public ICompositeService DeployedContainers { get; }
        private IHostService Docker { get; }
        public static Uri SiteUri => new Uri("https://localhost/");
        public static Uri ThriftUri => new Uri("wss://localhost/ws");
        public static Uri JanusUri => new Uri("https://localhost/janus");

        public static Uri TelemetryHubUri => new Uri("https://localhost:443/telemetryhub");
        public string TargetLiveStreamStoragePath { get; }
        public string AdminPassword { get; private set; }
        private static string DroHubTestsPath => Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory + "../../../");
        public static string DroHubPath => Path.GetFullPath(Path.Join(DroHubTestsPath, "../"));
        public static string TestAssetsPath => Path.Join(DroHubTestsPath, "TestAssets");

        private static string PatchedDockerComposeFileName => Path.Join(DroHubPath, "docker-compose-test.yml");

        public IConfiguration Configuration { get; private set; }

        public DroHubContext DbContext { get; private set; }

        public DroHubFixture() {

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

        public void Dispose() {
            Docker.Dispose();
            Containers.Dispose();
            File.Delete(PatchedDockerComposeFileName);
        }
    }
}