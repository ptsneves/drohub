using System;
using System.Collections;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Serilog;

namespace DroHub.ProgramExtensions {
    public static class WebHostBuilderExtension {
        private static IWebHostBuilder WipeEnvironmentalVariables(this IWebHostBuilder builder) {
            var env_variables = Environment.GetEnvironmentVariables();
            foreach (DictionaryEntry e in env_variables) {
                if (!(e.Key is string k)) {
                    continue;
                }

                Environment.SetEnvironmentVariable(k, "");
            }

            return builder;
        }

        public static IWebHostBuilder CreateDroHubWebHostBuilder() {
            return new WebHostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseKestrel()
                .UseUrls("http://*:5000/")
                .UseSerilog()
                .ConfigureAppConfiguration(builder => builder.AddDroHub())
                .WipeEnvironmentalVariables()
                .UseStartup<Startup>();
        }

    }
}