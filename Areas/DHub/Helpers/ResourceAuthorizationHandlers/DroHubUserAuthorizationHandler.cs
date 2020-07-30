using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers {
    public static class DroHubUserAuthorizationExtensions {
        public static void AddDroHubUserResourceAuthorization (this IServiceCollection services) {
            services.AddTransient<IAuthorizationHandler, DroHubUserAuthorizationHandler>();
        }
    }

    public class DroHubUserAuthorizationException : InvalidOperationException {
        public DroHubUserAuthorizationException(string error) : base(error) {}
    }

    public class DroHubUserAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, DroHubUser> {
        private readonly IServiceProvider _service_provider;
        private SubscriptionAPI _subscription_api;
        private List<Claim> _current_claims;

        public DroHubUserAuthorizationHandler( IServiceProvider serviceProvider) {
            _service_provider = serviceProvider;
        }

        private bool isSameSubscription(DroHubUser res) {
            return _subscription_api.isSameSubscriptionOrganization(
                new SubscriptionAPI.OrganizationName(res.SubscriptionOrganizationName));
        }

        private bool amIAdmin() {
            return _current_claims.Any(c => c.Type == DroHubUser.ADMIN_POLICY_CLAIM);
        }

        private bool amIOwner() {
            return _current_claims.Any(c => c.Type == DroHubUser.OWNER_POLICY_CLAIM
                                   && c.Value == DroHubUser.CLAIM_VALID_VALUE);
        }

        private async Task<bool> isCreateAuthorized(DroHubUser res) {
            if (amIAdmin())
                return true;

            return isSameSubscription(res)
                   && amIOwner()
                   && _current_claims.Any(
                                c => c.Type == res.BaseActingType &&
                                     c.Value == DroHubUser.CLAIM_VALID_VALUE)
                   && await _subscription_api.isUserCountBelowMaximum();
        }

        private bool isUpdateAuthorized(DroHubUser res) {
            if (amIAdmin())
                return true;

            return isSameSubscription(res)
                   && amIOwner()
                   && _current_claims.Any(c => c.Type == res.BaseActingType &&
                                      c.Value == DroHubUser.CLAIM_VALID_VALUE);
        }

        private bool isDeleteAuthorized(DroHubUser res) {
            if (amIAdmin())
                return true;

            return isSameSubscription(res)
                   && amIOwner()
                   && _current_claims.Any(c => c.Type == res.BaseActingType &&
                                               c.Value == DroHubUser.CLAIM_VALID_VALUE);
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext ctx,
            OperationAuthorizationRequirement requirement, DroHubUser res) {
            _subscription_api = _service_provider.GetRequiredService<SubscriptionAPI>();
            _current_claims = ctx.User.Claims.ToList();


            if (requirement.Name == ResourceOperations.Create.Name && await isCreateAuthorized(res))
                ctx.Succeed(requirement);
            else if (requirement.Name == ResourceOperations.Read.Name)
                ctx.Succeed(requirement);
            else if (requirement.Name == ResourceOperations.Update.Name && isUpdateAuthorized(res))
                ctx.Succeed(requirement);
            else if (requirement.Name == ResourceOperations.Delete.Name && isDeleteAuthorized(res)) //No difference
                ctx.Succeed(requirement);
            else
                ctx.Fail();
        }
    }


}