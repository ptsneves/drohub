using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using DroHub.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DroHub.Areas.DHub.API {
    public static class SubscriptionExtensions {
        public static void AddSubscriptionAPI (this IServiceCollection services) {
            services.AddTransient<SubscriptionAPI>();
        }
    }

    public class SubscriptionAPIException : Exception {
        public SubscriptionAPIException(string message) : base(message) {

        }
    }

    public class SubscriptionAPI {
        public readonly struct OrganizationName {
            internal string Value { get; }

            public OrganizationName(string org_name) {
                Value = org_name;
            }
        }

        private readonly DroHubContext _db_context;
        private readonly ClaimsPrincipal _user;
        private Subscription _current_subscription;
        public SubscriptionAPI(DroHubContext db_context, IHttpContextAccessor http_context) {
            _db_context = db_context;
            _user = http_context.HttpContext.User;
            if (!_user.Identity.IsAuthenticated)
                throw new AuthenticationException("Cannot use SubscriptionAPI on non authenticated users");
            _current_subscription = null;
        }

        public IEnumerable<Claim> getCurrentUserClaims() {
            if (!(_user.Identity is ClaimsIdentity identity)) {
                throw new InvalidProgramException("User should be have ClaimsIdentity to get Claims");
            }
            return identity.Claims;
        }

        public static List<Claim> getSubscriptionClaims(DroHubUser user) {
            return new List<Claim>() {
                new Claim(DroHubUser.SUBSCRIPTION_KEY_CLAIM, user.SubscriptionOrganizationName)
            };
        }

        public ClaimsPrincipal getClaimsPrincipal() {
            return _user;
        }

        public async Task<int> getRemainingUserCount() {
            var query_result = await querySubscription(getSubscriptionName())
                .Select(s => new {
                    UsersCount = s.Users.Count(),
                    AllowedUsersCount = s.AllowedUserCount
                })
                .SingleAsync();

            return query_result.AllowedUsersCount - query_result.UsersCount;
        }

        public async Task<bool> isUserCountBelowMaximum() {
            return await getRemainingUserCount() > 0;
        }

        public async Task<int> getAllowedUserCount() {
            var subscription = await getSubscription();
            return subscription.AllowedUserCount;
        }

        private async Task<Subscription> getSubscription() {
            return _current_subscription ??= await getSubscription(getSubscriptionName());
        }

        public async Task<TimeSpan> getSubscriptionTimeLeft() {
            var subscription = await getSubscription();
            return subscription.AllowedFlightTime;
        }

        private async Task<TimeSpan> getSubscriptionTimeLeft(OrganizationName organization_name) {
            var subscription = await getSubscription(organization_name);
            return subscription.AllowedFlightTime;
        }

        public OrganizationName getSubscriptionName() {
            return new OrganizationName(
                getClaimsPrincipal()
                    .Claims.Single(c => c.Type == DroHubUser.SUBSCRIPTION_KEY_CLAIM)?.Value);
        }

        private IQueryable<Subscription> querySubscription(OrganizationName organization_name) {
            return _db_context.Subscriptions
                .Where(s => s.OrganizationName == organization_name.Value);
        }

        private Task<Subscription> getSubscription(OrganizationName organization_name) {
            return querySubscription(organization_name).SingleAsync();
        }

        public IQueryable<DroHubUser> getSubscriptionUsers(OrganizationName organization_name) {
            return _db_context.Subscriptions
                .Where(s => s.OrganizationName == organization_name.Value)
                .Include(s => s.Users)
                .SelectMany(s => s.Users);
        }

        public bool isSameSubscriptionOrganization(OrganizationName organization_name) {
            return getSubscriptionName().Value == organization_name.Value;
        }

        internal IQueryable<Device> querySubscribedDevices() {
            return querySubscription(getSubscriptionName())
                .SelectMany(s => s.Devices);
        }

        public Task<List<Device>> getSubscribedDevices() {
            return _db_context.Subscriptions
                .Where(s => s.OrganizationName == getSubscriptionName().Value)
                .SelectMany(s => s.Devices)
                .ToListAsync();
        }

        public async Task<bool> isTimeLeft(OrganizationName organization_name) {
            return await getSubscriptionTimeLeft(organization_name) > TimeSpan.Zero;
        }

        public Task<TimeSpan> decrementAndGetSubscriptionTimeLeft(TimeSpan consumed_time_span,
            CancellationToken cancellation_token) {
            return decrementAndGetSubscriptionTimeLeft(getSubscriptionName(), consumed_time_span,
                cancellation_token);
        }

        private async Task<TimeSpan> decrementAndGetSubscriptionTimeLeft(OrganizationName organization_name,
            TimeSpan consumed_time_span, CancellationToken cancellation_token) {

            var subscription = await getSubscription(organization_name);
            if (subscription == null)
                throw new InvalidProgramException("Could not find subscription ");

            bool save_failed;
            do {
                save_failed = false;
                subscription.AllowedFlightTime -= consumed_time_span;
                subscription.AllowedFlightTime =
                    subscription.AllowedFlightTime < TimeSpan.Zero ? TimeSpan.Zero : subscription.AllowedFlightTime;

                try {
                    // because we want to try to save the changes always
                    // ReSharper disable once MethodSupportsCancellation
                    await _db_context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException e) {
                    save_failed = true;
                    await e.Entries.Single().ReloadAsync();
                }

                //Only do it in the end so we have a chance to write the value!
                if (save_failed && cancellation_token.IsCancellationRequested)
                    throw new OperationCanceledException();

            } while (save_failed);
            return subscription.AllowedFlightTime;
        }

        public async Task deleteSubscription(OrganizationName organization_name) {

            var sub = await querySubscription(organization_name)
                .Include(s => s.Devices)
                .Include(s => s.DeviceConnections)
                .Include(s => s.Users).SingleAsync();

            if (sub.Devices.Any() || sub.Users.Any() || sub.DeviceConnections.Any())
                throw new SubscriptionAPIException("Cannot remove a subscription with active devices or users");

            _db_context.Subscriptions.Remove(sub);
            await _db_context.SaveChangesAsync();
        }

        public struct UserTypeAttributes {
            public string human_description;
        }

        public Dictionary<string, UserTypeAttributes> getAvailableUserTypes() {
            var r =  new Dictionary<string, UserTypeAttributes>();

            if (getCurrentUserClaims().Any(c => c.Type == DroHubUser.ADMIN_POLICY_CLAIM &&
                                                c.Value == DroHubUser.CLAIM_VALID_VALUE)) {
                r["admin"] = new UserTypeAttributes {
                    human_description =
                        "Manage all the subscriptions registered with the web site, as well as the users and data."
                };
            }

            if (getCurrentUserClaims().Any(c => c.Type == DroHubUser.SUBSCRIBER_POLICY_CLAIM &&
                                                c.Value == DroHubUser.CLAIM_VALID_VALUE)) {
                r["subscriber"] = new UserTypeAttributes {
                    human_description =
                        "Manage own subscription settings within the limits allowed, as well as the users and their permissions. Can also perform the roles of the pilot and guest."
                };
            }

            if (!getCurrentUserClaims().Any(c => c.Type == DroHubUser.OWNER_POLICY_CLAIM &&
                                                 c.Value == DroHubUser.CLAIM_VALID_VALUE))
                return r;

            r["owner"] = new UserTypeAttributes {
                human_description =
                    "Can do everything the subscriber can, except change parameters of the subscription"
            };
            r["pilot"] = new UserTypeAttributes {
                human_description = "User who can use the application, and use the media functions of drohub."
            };
            r["guest"] = new UserTypeAttributes {
                human_description = "Can only see the gallery and live videos. No actions allowed."
            };

            return r;
        }
    }
}