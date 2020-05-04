using System;
using System.Security.Claims;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers {
    public static class DeviceAuthorizationExtensions {
        public static void AddDeviceResourceAuthorization (this IServiceCollection services) {
            services.AddTransient<IAuthorizationHandler, DeviceAuthorizationHandler>();
        }
    }

    public class DeviceAuthorizationException : InvalidOperationException {
        public DeviceAuthorizationException(string error) : base(error) {}
    }

    public class DeviceAuthorizationHandler  : AuthorizationHandler<OperationAuthorizationRequirement, Device> {
        public class DeviceResourceOperations : ResourceOperations {
            public static OperationAuthorizationRequirement CanPerformFlightActions =
                new OperationAuthorizationRequirement { Name = nameof(CanPerformFlightActions) };
            public static OperationAuthorizationRequirement CanPerformCameraActions =
                new OperationAuthorizationRequirement { Name = nameof(CanPerformCameraActions) };

            private const string DEFAULT_CLAIM_VALUE = "Yes";
            public static readonly Claim CreateClaim = new Claim($"Can{Create.Name}Device", DEFAULT_CLAIM_VALUE);
            public static readonly Claim ReadClaim = new Claim($"Can{Read.Name}Device", DEFAULT_CLAIM_VALUE);
            public static readonly Claim UpdateClaim = new Claim($"Can{Update.Name}Device", DEFAULT_CLAIM_VALUE);
            public static readonly Claim DeleteClaim = new Claim($"Can{Delete.Name}Device", DEFAULT_CLAIM_VALUE);
            public static readonly Claim FlightActionsClaim = new Claim($"{CanPerformFlightActions.Name}Device",
                DEFAULT_CLAIM_VALUE);

            public static readonly Claim CameraActionsClaim = new Claim($"{CanPerformCameraActions.Name}Device",
                DEFAULT_CLAIM_VALUE);
        }

        private readonly IServiceProvider _service_provider;
        private SubscriptionAPI _subscription_api;

        public DeviceAuthorizationHandler(IServiceProvider service_provider) {
            _service_provider = service_provider;
        }

        private bool isSameSubscription(Device res) {
            return _subscription_api.isSameSubscriptionOrganization(
                new SubscriptionAPI.OrganizationName(res.SubscriptionOrganizationName));
        }

        private bool isClaimAuthorized(AuthorizationHandlerContext ctx, Device res, Claim claim) {
            return isSameSubscription(res) && ctx.User.HasClaim(c => c.Type == claim.Type && c.Value == claim.Value);
        }

        private bool isCreateAuthorized(AuthorizationHandlerContext ctx, Device res) {
            return isClaimAuthorized(ctx, res, DeviceResourceOperations.CreateClaim);
        }

        private bool isReadAuthorized(AuthorizationHandlerContext ctx, Device res) {
            return isClaimAuthorized(ctx, res, DeviceResourceOperations.ReadClaim);
        }

        private bool isUpdateAuthorized(AuthorizationHandlerContext ctx, Device res) {
            return isClaimAuthorized(ctx, res, DeviceResourceOperations.UpdateClaim);
        }

        private bool isDeleteAuthorized(AuthorizationHandlerContext ctx, Device res) {
            return isClaimAuthorized(ctx, res, DeviceResourceOperations.DeleteClaim);
        }

        private async Task<bool> areFlightActionsAuthorized(AuthorizationHandlerContext ctx, Device res) {
            var org_name = new SubscriptionAPI.OrganizationName(res.SubscriptionOrganizationName);
            return isClaimAuthorized(ctx, res, DeviceResourceOperations.FlightActionsClaim) &&
                   await _subscription_api.isTimeLeft(org_name);

        }

        private bool areCameraActionsAuthorized(AuthorizationHandlerContext ctx, Device res) {
            return isClaimAuthorized(ctx, res, DeviceResourceOperations.CameraActionsClaim);
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext ctx,
        OperationAuthorizationRequirement requirement, Device res) {
            _subscription_api = _service_provider.GetRequiredService<SubscriptionAPI>();
            if (requirement.Name == ResourceOperations.Create.Name && isCreateAuthorized(ctx, res))
                ctx.Succeed(requirement);
            else if (requirement.Name == ResourceOperations.Read.Name && isReadAuthorized(ctx, res))
                ctx.Succeed(requirement);
            else if (requirement.Name == ResourceOperations.Update.Name && isUpdateAuthorized(ctx, res))
                ctx.Succeed(requirement);
            else if (requirement.Name == ResourceOperations.Delete.Name && isDeleteAuthorized(ctx, res)) //No difference
                ctx.Succeed(requirement);
            else if (requirement.Name == DeviceResourceOperations.CanPerformFlightActions.Name &&
                     await areFlightActionsAuthorized(ctx, res))
                ctx.Succeed(requirement);
            else if (requirement.Name == DeviceResourceOperations.CanPerformCameraActions.Name &&
                     areCameraActionsAuthorized(ctx, res))
                ctx.Succeed(requirement);
            else
                ctx.Fail();
        }

        public const string TELEMETRY_SERIAL_NUMBER_CLAIM = "AuthorizedDeviceSerialNumber";
    }
}