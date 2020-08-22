using System;
using System.Security.Claims;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers {
    public static class MediaObjectAuthorizationExtensions {
        public static void AddMediaObjectResourceAuthorization(this IServiceCollection services) {
            services.AddTransient<IAuthorizationHandler, MediaObjectAuthorizationHandler>();
        }
    }

    public class MediaObjectAuthorizationException : InvalidOperationException {
        public MediaObjectAuthorizationException(string error) : base(error) {}
    }

    public class MediaObjectAuthorizationHandler  : AuthorizationHandler<OperationAuthorizationRequirement, MediaObject> {
        public class MediaObjectResourceOperations : ResourceOperations {
            public static OperationAuthorizationRequirement ManipulateTags =
                new OperationAuthorizationRequirement { Name = nameof(ManipulateTags) };

            private const string DEFAULT_CLAIM_VALUE = "Yes";
            public static readonly Claim ReadClaim = new Claim($"Can{Read.Name}Media", DEFAULT_CLAIM_VALUE);
            public static readonly Claim DeleteClaim = new Claim($"Can{Delete.Name}Media", DEFAULT_CLAIM_VALUE);
        }

        private readonly IServiceProvider _service_provider;
        private SubscriptionAPI _subscription_api;

        public MediaObjectAuthorizationHandler(IServiceProvider service_provider) {
            _service_provider = service_provider;
        }

        private bool isSameSubscription(MediaObject res) {
            return _subscription_api.isSameSubscriptionOrganization(
                new SubscriptionAPI.OrganizationName(res.SubscriptionOrganizationName));
        }


        private bool amIAdmin(AuthorizationHandlerContext ctx) {
            return ctx.User.HasClaim(c => c.Type == DroHubUser.ADMIN_POLICY_CLAIM && c.Value == DroHubUser.CLAIM_VALID_VALUE);
        }

        private bool isClaimAuthorized(AuthorizationHandlerContext ctx, MediaObject res, Claim claim) {
            if (amIAdmin(ctx))
                return true;
            return isSameSubscription(res) && ctx.User.HasClaim(c => c.Type == claim.Type && c.Value == claim.Value);
        }

        private bool isReadAuthorized(AuthorizationHandlerContext ctx, MediaObject res) {
            return isClaimAuthorized(ctx, res, MediaObjectResourceOperations.ReadClaim);
        }

        private bool isDeleteAuthorized(AuthorizationHandlerContext ctx, MediaObject res) {
            return isClaimAuthorized(ctx, res, MediaObjectResourceOperations.DeleteClaim);
        }

        private bool isManipulateTagsAuthorized(AuthorizationHandlerContext ctx, MediaObject res) {
            return isDeleteAuthorized(ctx, res); //For now only somebody who can manipulate videos can manipulate tags
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext ctx,
        OperationAuthorizationRequirement requirement, MediaObject res) {

            _subscription_api = _service_provider.GetRequiredService<SubscriptionAPI>();
            if (requirement.Name == ResourceOperations.Read.Name && isReadAuthorized(ctx, res))
                ctx.Succeed(requirement);
            else if (requirement.Name == ResourceOperations.Delete.Name && isDeleteAuthorized(ctx, res)) //No difference
                ctx.Succeed(requirement);
            else if (requirement.Name == MediaObjectResourceOperations.ManipulateTags.Name &&
                     isManipulateTagsAuthorized(ctx, res))
                ctx.Succeed(requirement);
            else
                ctx.Fail();
            return Task.CompletedTask;
        }
    }
}