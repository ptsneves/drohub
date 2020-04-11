using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace DroHub.Areas.Identity {
    public static class DroHubUserHelper {
        private const string _APP_TOKEN_PURPOSE = "DroHubApp";
        private static readonly string TokenProvider = TokenOptions.DefaultProvider;

        public class GenerateTokenException : InvalidOperationException {
            public static GenerateTokenException SigninFailed => new GenerateTokenException("User of password are wrong");
            public static GenerateTokenException PilotCheckFailed => new GenerateTokenException("Only pilots can get tokens");

            private GenerateTokenException(string value) : base(value) { }
        }

        public static async Task<string> generateToken(this SignInManager<DroHubUser> signin_manager, string user_name,
            string password) {

            var result = await signin_manager.PasswordSignInAsync(user_name, password,
                false, true);

            if (!result.Succeeded)
                throw GenerateTokenException.SigninFailed;

            var user = await signin_manager.UserManager.FindByNameAsync(user_name);
            var claims = await signin_manager.UserManager.GetClaimsAsync(user);
            if (!claims.Any(c =>
                c.Type == DroHubUser.PILOT_POLICY_CLAIM && c.Value == DroHubUser.CLAIM_VALID_VALUE)) {
                throw GenerateTokenException.PilotCheckFailed;
            }

            return await signin_manager
                .UserManager
                .GenerateUserTokenAsync(user, TokenProvider, _APP_TOKEN_PURPOSE);
        }

        public static async Task<bool> isTokenValid(this SignInManager<DroHubUser> sign_in_manager, string user_name,
            string token) {
            var user = await sign_in_manager.UserManager.FindByNameAsync(user_name);
            if (user == null)
                return false;

            var claims = await sign_in_manager.UserManager.GetClaimsAsync(user);
            if (!claims.Any(c =>
                c.Type == DroHubUser.PILOT_POLICY_CLAIM && c.Value == DroHubUser.CLAIM_VALID_VALUE)) {
                return false;
            }
            var verified = await sign_in_manager.UserManager.VerifyUserTokenAsync(user, TokenProvider, _APP_TOKEN_PURPOSE,
                token);
            if (verified)
                await sign_in_manager.SignInAsync(user, false);

            return verified;
        }

        public static IIncludableQueryable<DroHubUser, Subscription> getCurrentUserWithSubscription(this UserManager<DroHubUser> user_manager, ClaimsPrincipal user){
            return user_manager.Users
                .Where(u => u.Id == user_manager.GetUserId(user))
                .Include(u => u.Subscription);
        }

        public static IQueryable<Subscription> getCurrentUserSubscription(this IIncludableQueryable<DroHubUser, Subscription> users) {
            return users
                .ThenInclude(s => s.Devices)
                .Select(u => u.Subscription);
        }

        public static IQueryable<Device> getSubscriptionDevices(this IQueryable<Subscription> subscriptions) {
            return subscriptions.SelectMany(s => s.Devices);
        }
    }
}