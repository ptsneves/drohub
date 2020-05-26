using System;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Extensions;
using Ductus.FluentDocker.Commands;
using System.Linq;
using System.IO;
using AngleSharp;
using AngleSharp.Html.Parser;

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
            Console.WriteLine("Ready to start");
        }

        public static AngleSharp.Html.Dom.IHtmlDocument getHtmlDOM(string responseBody)
        {
            var context = BrowsingContext.New(Configuration.Default);
            var parser = context.GetService<IHtmlParser>();
            return parser.ParseDocument(responseBody);
        }

        public static string getVerificationToken(string responseBody)
        {
            var document = getHtmlDOM(responseBody);
            return document.QuerySelectorAll("input[name='__RequestVerificationToken']").First().GetAttribute("value");
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