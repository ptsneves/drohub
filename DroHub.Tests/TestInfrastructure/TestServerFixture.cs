using System;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Extensions;
using Ductus.FluentDocker.Commands;
using System.Linq;
using System.IO;

namespace DroHub.Tests.TestInfrastructure
{
    public class DroHubFixture : IDisposable
    {
        private readonly ICompositeService _containers;
        private readonly IHostService _docker;
        public Uri SiteUri { get; private set; }
        public Uri ThriftUri {get; private set; }
        public string AdminPassword { get; private set; }

        public DroHubFixture() {
            SiteUri = new Uri("http://localhost:5000/");
            ThriftUri = new Uri("ws://localhost:5000/ws");
            var docker_compose_file = Path.GetFullPath("../../../../docker-compose.yml");

            _containers = new Builder()
                            .UseContainer()
                            .UseCompose()
                            .FromFile(docker_compose_file)
                            .WaitForHttp("web", SiteUri.ToString())
                            .RemoveOrphans()
                            .ForceBuild()
                            .Build().Start();

            var hosts = new Hosts().Discover();
            _docker = hosts.FirstOrDefault(x => x.IsNative);
            var database_container = _containers.Containers.First(c => c.Name == "web");
            using (var logs = _docker.Host.Logs(database_container.Id))
            {
                AdminPassword = logs.ReadToEnd().First(line => line.Contains("GENERATED ROOT PASSWORD")).Split(null).Last();
            }
        }

        public static AngleSharp.Html.Dom.IHtmlDocument getHtmlDOM(string responseBody)
        {
            var context = BrowsingContext.New(Configuration.Default);
            var parser = context.GetService<IHtmlParser>();
            return parser.ParseDocument(responseBody);
        }

        public void Dispose() {
            _containers.Dispose();
        }
    }
}