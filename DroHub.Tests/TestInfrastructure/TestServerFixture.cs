using System;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Extensions;
using Ductus.FluentDocker.Commands;
using System.Linq;
using System.IO;
using AngleSharp;
using AngleSharp.Html.Parser;
using DroHub.Helpers;
using mailslurp.Api;
using mailslurp.Model;

namespace DroHub.Tests.TestInfrastructure
{
    public class DroHubFixture : IDisposable
    {
        public ICompositeService Containers { get; }
        private IHostService Docker { get; }
        public static Uri SiteUri => new Uri("http://localhost:5000/");
        public static Uri ThriftUri => new Uri("ws://localhost:5000/ws");
        public string TargetLiveStreamStoragePath { get; }
        public string AdminPassword { get; private set; }
        private static string DroHubTestsPath => Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory + "../../../");
        private static string DroHubPath => Path.GetFullPath(Path.Join(DroHubTestsPath, "../"));
        public static string TestAssetsPath => Path.Join(DroHubTestsPath, "TestAssets");

        public DroHubFixture() {
            var docker_compose_file = Path.Join(DroHubPath, "docker-compose.yml");

            Containers = new Builder()
                            .UseContainer()
                            .UseCompose()
                            .FromFile(docker_compose_file)
                            .WaitForHttp("web", SiteUri.ToString())
                            .RemoveOrphans()
                            // .ForceBuild()
                            .Build()
                            .Start();

            var hosts = new Hosts().Discover();
            Docker = hosts.First(x => x.IsNative);
            var web_container = Containers.Containers.First(c => c.Name == "web");
            using (var logs = Docker.Host.Logs(web_container.Id))
            {
                AdminPassword = logs
                    .ReadToEnd()
                    .First(line => line.Contains("GENERATED ROOT PASSWORD"))
                    .Split(null).Last();
            }

            var live_video_mount = web_container
                .GetConfiguration().Mounts
                .Single(m => m.Source.Contains("live-video-storage"));
            TargetLiveStreamStoragePath = live_video_mount.Destination + Path.DirectorySeparatorChar;
            Console.WriteLine("Ready to start");
        }

        public static AngleSharp.Html.Dom.IHtmlDocument getHtmlDOM(string responseBody)
        {
            var context = BrowsingContext.New(Configuration.Default);
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
            try {
                // _containers.Dispose();
            } catch (Ductus.FluentDocker.Common.FluentDockerException) {
                //Do nothing. This exception seems a bug in FluentDocker
            }
        }
    }
}