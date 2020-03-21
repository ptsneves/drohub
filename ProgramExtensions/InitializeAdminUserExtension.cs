using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using System;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using DroHub.Areas.DHub.Models;
using DroHub.Data;
using Microsoft.AspNetCore.Authorization;
using DroHub.IdentityClaims;

namespace Microsoft.AspNetCore.Hosting
{
    public static partial class IWebHostExtensions
    {
        public async static Task<IWebHost> InitializeAdminUser<T>(this IWebHost webHost) where T : DroHubContext
        {
            using (var scope = webHost.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var db = services.GetRequiredService<T>();
                var logger = services.GetRequiredService<ILogger<T>>();
                var user_manager = services.GetRequiredService<UserManager<DroHubUser>>();
                await InitializeAdminUserHelper.createAdminUser(logger, user_manager, db);
            }
            return webHost;
        }
    }
}

namespace DroHub.IdentityClaims
{
    public static class InitializeAdminUserHelper
    {
        public static string AdminUserName = "admin";
        internal static async Task<string> createAdminUser(ILogger logger, UserManager<DroHubUser> user_manager,
            DroHubContext db_context)
        {
            var user = await user_manager.FindByNameAsync(AdminUserName);
            if (user != null) return user.Id;

            var subscription = new Subscription()
            {
                OrganizationName = "Administrators",
                AllowedFlightTime = TimeSpan.FromMinutes(50338), // MySQL allows maximum 838:59:59.000000 so in minutes 838*60+58 = 50338 minutes,
                AllowedUserCount = Int32.MaxValue
            };

            db_context.Subscriptions.Add(subscription);
            await db_context.SaveChangesAsync();

            var admin_password = generatePassword(10, 0);
            logger.LogWarning("Initialized admin password. Please change it. GENERATED ROOT PASSWORD {admin}\n", admin_password);
            user = new DroHubUser
            {
                EmailConfirmed = true,
                UserName = AdminUserName,
                Subscription = subscription,
                BaseActingType = DroHubUser.ADMIN_POLICY_CLAIM
            };
            await user_manager.CreateAsync(user, admin_password);
            foreach (var claim in DroHubUser.UserClaims[DroHubUser.ADMIN_POLICY_CLAIM]) {
                await user_manager.AddClaimAsync(user, claim);
            };

            return user.Id;
        }

        //Taken https://stackoverflow.com/a/38997554/227990
        private static string generatePassword(int length, int numberOfNonAlphanumericCharacters)
        {
            char[] Punctuations = "!@#$%^&*()_-+=[{]};:>|./?".ToCharArray();

            if (length < 1 || length > 128)
            {
                throw new ArgumentException(nameof(length));
            }

            if (numberOfNonAlphanumericCharacters > length || numberOfNonAlphanumericCharacters < 0)
            {
                throw new ArgumentException(nameof(numberOfNonAlphanumericCharacters));
            }

            using (var rng = RandomNumberGenerator.Create())
            {
                var byteBuffer = new byte[length];

                rng.GetBytes(byteBuffer);

                var count = 0;
                var characterBuffer = new char[length];

                for (var iter = 0; iter < length; iter++)
                {
                    var i = byteBuffer[iter] % 87;

                    if (i < 10)
                    {
                        characterBuffer[iter] = (char)('0' + i);
                    }
                    else if (i < 36)
                    {
                        characterBuffer[iter] = (char)('A' + i - 10);
                    }
                    else if (i < 62)
                    {
                        characterBuffer[iter] = (char)('a' + i - 36);
                    }
                    else
                    {
                        characterBuffer[iter] = Punctuations[i - 62];
                        count++;
                    }
                }

                if (count >= numberOfNonAlphanumericCharacters)
                {
                    return new string(characterBuffer);
                }

                int j;
                var rand = new Random();

                for (j = 0; j < numberOfNonAlphanumericCharacters - count; j++)
                {
                    int k;
                    do
                    {
                        k = rand.Next(0, length);
                    }
                    while (!char.IsLetterOrDigit(characterBuffer[k]));

                    characterBuffer[k] = Punctuations[rand.Next(0, Punctuations.Length)];
                }

                return new string(characterBuffer);
            }
        }
    }
}