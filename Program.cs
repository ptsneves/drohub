using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System;
using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DroHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Hosting
{
    public static class IWebHostExtensions
    {
        public static IWebHost MigrateDatabase<T>(this IWebHost webHost) where T : DbContext
        {
            using (var scope = webHost.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var db = services.GetRequiredService<T>();
                var logger = services.GetRequiredService<ILogger<T>>();
                try
                {
                    db.Database.Migrate();
                    logger.LogWarning("Ran migrate database");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while migrating the database.");
                }
            }
            return webHost;
        }
    }
}
namespace DroHub
{
    public class Program
    {
        public static void Main(string[] args)
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
                if (_config.GetValue<bool>("AutoMigration"))
                    CreateWebHostBuilder(args).Build().MigrateDatabase<DroHubContext>().Run();
                else
                    CreateWebHostBuilder(args).Build().Run();
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