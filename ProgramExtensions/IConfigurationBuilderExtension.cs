using System.IO;
using DotNetEnv;
using Microsoft.Extensions.Configuration;

namespace DroHub.ProgramExtensions {
    public static class IConfigurationBuilderExtension {
        public static IConfigurationBuilder AddDroHub(this IConfigurationBuilder builder) {
            return builder
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables("DHUB_")
                .LoadFromEnvFile(".env", "DHUB_", LoadOptions.DEFAULT);
        }

        public const string DATABASE_PROVIDER_KEY = "DatabaseProvider";
    }
}