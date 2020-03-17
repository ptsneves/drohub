using DroHub.Areas.Identity.Data;
using DroHub.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(DroHub.Areas.Identity.IdentityHostingStartup))]
namespace DroHub.Areas.Identity
{
    public class IdentityHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) =>
            {
                services.AddDefaultIdentity<DroHubUser>()
                    .AddEntityFrameworkStores<DroHubContext>();

                AuthorizationOptionsExtension.ConfigureAuthorizationOptions(services);
                // Passwords validation settings.
                // https://docs.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-2.1#password
                services.Configure<IdentityOptions>(options =>
                {
                    options.Password.RequireDigit = false;           // Default true
                    options.Password.RequireLowercase = false;       // Default true
                    options.Password.RequireNonAlphanumeric = false; // Default true
                    options.Password.RequireUppercase = false;       // Default true
                    options.Password.RequiredLength = 6;             // Default 6
                    options.Password.RequiredUniqueChars = 2;        // Default 1
                });
            });
        }
    }
}