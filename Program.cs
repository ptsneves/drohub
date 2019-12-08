using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System;
using Serilog;
using Microsoft.Extensions.Configuration;
using DroHub.Data;

namespace DroHub
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var appsettings = "";
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
                appsettings = "appsettings.json";
            else
            {
                appsettings = "appsettings.Development.json";
            }

            var _config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(appsettings, optional: false)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(_config)
                .Enrich.FromLogContext()
                .CreateLogger();
            try
            {
                IWebHost web_host;
                if (_config.GetValue<bool>("AutoMigration"))
                    web_host = CreateWebHostBuilder(args)
                     .Build()
                     .MigrateDatabase<DroHubContext>();

                else
                    web_host = CreateWebHostBuilder(args)
                    .Build();

                (await web_host.InitializeAdminUser<DroHubContext>())
                .Run();
            }
            finally
            {
                Log.CloseAndFlush();
            }
            Serilog.Debugging.SelfLog.Enable(Console.Error);
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
            .UseKestrel()
            .UseUrls("http://*:5000/")
            .UseContentRoot(Directory.GetCurrentDirectory())
            .UseSerilog()
            .UseStartup<Startup>();
    }
}