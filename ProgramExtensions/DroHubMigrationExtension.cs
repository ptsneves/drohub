using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.Identity.Data;
using DroHub.Data;
using Microsoft.AspNetCore.Identity;

namespace Microsoft.AspNetCore.Hosting
{
    public static partial class IWebHostExtensions
    {
        public static IWebHost migrateDatabase<T>(this IWebHost web_host) where T : DbContext
        {
            using var scope = web_host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<T>();
            var logger = services.GetRequiredService<ILogger<T>>();
            db.Database.Migrate();
            logger.LogWarning("Ran migrate database");
            return web_host;
        }

        public static async Task<IWebHost> migrateDroHubUserClaims<T>(this IWebHost web_host) where T : DroHubContext {
            using (var scope = web_host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var db = services.GetRequiredService<T>();
                var logger = services.GetRequiredService<ILogger<T>>();
                var user_manager = services.GetRequiredService<UserManager<DroHubUser>>();

                foreach(var u in db.Users.ToList()) {
                    await user_manager.RemoveClaimsAsync(u, DroHubUser.UserClaims[u.BaseActingType]);
                    await user_manager.RemoveClaimsAsync(u, SubscriptionAPI.getSubscriptionClaims(u));

                    await user_manager.AddClaimsAsync(u, DroHubUser.UserClaims[u.BaseActingType]);
                    await user_manager.AddClaimsAsync(u, SubscriptionAPI.getSubscriptionClaims(u));
                };
                logger.LogWarning("Ran user claims migration");
            }
            return web_host;
        }
    }
}