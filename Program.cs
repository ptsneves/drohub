using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System;
using Serilog;
using Microsoft.Extensions.Configuration;

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