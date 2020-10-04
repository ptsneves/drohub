using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using DroHub.Data;
using DroHub.IdentityClaims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Hosting
{
    public static partial class IWebHostExtensions
    {
        public static async Task<IWebHost> InitializeAdminUser<T>(this IWebHost webHost) where T : DroHubContext
        {
            using (var scope = webHost.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var db = services.GetRequiredService<T>();
                var logger = services.GetRequiredService<ILogger<T>>();
                var sign_in_manager = services.GetRequiredService<SignInManager<DroHubUser>>();
                await InitializeAdminUserHelper.createAdminUser(logger, sign_in_manager, db);
            }
            return webHost;
        }
    }
}

namespace DroHub.IdentityClaims
{
    public static class InitializeAdminUserHelper
    {
        private const string _ADMIN_USER_NAME = "admin@drohub.xyz";
        private const string _ADMIN_ORGANIZATION = "Administrators";

        internal static async Task<string> createAdminUser(ILogger logger, SignInManager<DroHubUser> signin_manager,
            DroHubContext db_context) {
            var user_manager = signin_manager.UserManager;
            var new_user = false;
            var new_subscription = false;

            var user = await user_manager.FindByNameAsync(_ADMIN_USER_NAME);
            if (user == null) {
                user = new DroHubUser();
                new_user = true;
            }

            var subscription = await db_context.Subscriptions.SingleOrDefaultAsync(
                s => s.OrganizationName == _ADMIN_ORGANIZATION);

            if (subscription == null) {
                subscription = new Subscription();
                new_subscription = true;
            }

            subscription.OrganizationName = _ADMIN_ORGANIZATION;
            subscription.BillingPlanName = "Administrator";

            subscription.AllowedFlightTime = TimeSpan.FromMinutes(50338); // MySQL allows maximum 838:59:59.000000 so in minutes 838*60+58 = 50338 minutes,
            subscription.AllowedUserCount = Int32.MaxValue;

            if (new_subscription)
                db_context.Subscriptions.Add(subscription);
            else {
                db_context.Subscriptions.Update(subscription);
            }
            await db_context.SaveChangesAsync();

            user.EmailConfirmed = true;
            user.UserName = _ADMIN_USER_NAME;
            user.Email = _ADMIN_USER_NAME;
            user.Subscription = subscription;
            user.BaseActingType = DroHubUser.ADMIN_POLICY_CLAIM;

            if (new_user) {
                var admin_password = generatePassword(10, 0);
                var create_result = await user_manager.CreateAsync(user, admin_password);
                if (!create_result.Succeeded)
                    throw new InvalidProgramException("Unable to create an administrator account. Aborting");

                logger.LogWarning("Initialized admin password. Please change it. GENERATED ROOT PASSWORD {admin}\n",
                    admin_password);
            }
            else {
                db_context.Users.Update(user);
                await db_context.SaveChangesAsync();
            }

            var refresh_result = await DroHubUser.refreshClaims(signin_manager, user);
            if (refresh_result == IdentityResult.Failed())
                throw new InvalidProgramException("Could not remove admin claims");

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