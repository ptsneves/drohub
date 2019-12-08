using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

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