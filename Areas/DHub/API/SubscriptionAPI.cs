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
        public SubscriptionAPI(DroHubContext db_context, IHttpContextAccessor http_context) {
            _db_context = db_context;
            _user = http_context.HttpContext.User;
            if (!_user.Identity.IsAuthenticated)
                throw new AuthenticationException("Cannot use SubscriptionAPI on non authenticated users");
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

        public async Task<TimeSpan> getSubscriptionTimeLeft() {
            var subscription = await getSubscription(getSubscriptionName());
            return subscription.AllowedFlightTime;
        }

        public async Task<TimeSpan> getSubscriptionTimeLeft(OrganizationName organization_name) {
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

        public IQueryable<ICollection<DroHubUser>> getSubscriptionUsers(OrganizationName organization_name) {
            return _db_context.Subscriptions
                .Where(s => s.OrganizationName == organization_name.Value)
                .Include(s => s.Users)
                .Select(s => s.Users);
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

        public async Task<IEnumerable<DeviceConnection>> getSubscribedDeviceConnections(bool include_device) {
            var s1 = querySubscription(getSubscriptionName())
                .Include(s => s.DeviceConnections);

            if (include_device) {
                return await s1
                    .ThenInclude(cd => cd.Device)
                    .SelectMany(s => s.DeviceConnections)
                    .ToArrayAsync();
            }
            else {
                return await s1
                    .SelectMany(s => s.DeviceConnections)
                    .ToArrayAsync();
            }
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
    }
}