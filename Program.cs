using System;
using Microsoft.AspNetCore.Hosting;
using DroHub.ProgramExtensions;
using Serilog;
using Serilog.Events;

namespace DroHub
{
    public class Program {
        public static int Main() {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateBootstrapLogger();
            try {
                WebHostBuilderExtension.CreateDroHubWebHostBuilder()
                    .Build()
                    .Run();
            }
            catch (Exception ex) {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally {
                Log.CloseAndFlush();
            }

            return 0;
        }
    }
}