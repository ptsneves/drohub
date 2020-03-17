using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Security.Claims;
using DroHub.Areas.Identity.Data;
using DroHub.IdentityClaims;

namespace Microsoft.AspNetCore.Authorization
{
    public static class AuthorizationOptionsExtension
    {
        static AuthorizationOptionsExtension() {
            Policies = new Dictionary<string, Action<AuthorizationPolicyBuilder>>();
        }
        public static Dictionary<string, Action<AuthorizationPolicyBuilder>> Policies { get; private set; }

        public static void AddPolicyEx(this AuthorizationOptions options, string name,
            Action<AuthorizationPolicyBuilder> configurePolicy)
        {
            Policies[name] = configurePolicy;
        }

        public static void ConfigureAuthorizationOptions(IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.addIsActingRolesPolicies();
                foreach (var pair in Policies)
                {
                    options.AddPolicy(pair.Key, pair.Value);
                }
            });
        }

        public static void addIsActingRolesPolicies(this AuthorizationOptions options)
        {
            options.AddPolicyEx("CanActAsAdmin", policy =>
                policy.RequireClaim(DroHubUser.ADMIN_POLICY_CLAIMS, DroHubUser.CLAIM_VALID_VALUE)
            );

            options.AddPolicyEx("CanActAsSubscriber", policy =>
                policy.RequireClaim(DroHubUser.SUBSCRIBER_POLICY_CLAIMS, DroHubUser.CLAIM_VALID_VALUE)
            );

            options.AddPolicyEx("CanActAsOwner", policy =>
                policy.RequireClaim(DroHubUser.OWNER_POLICY_CLAIMS, DroHubUser.CLAIM_VALID_VALUE)
            );

            options.AddPolicyEx("CanActAsPilot", policy =>
                policy.RequireClaim(DroHubUser.PILOT_POLICY_CLAIMS, DroHubUser.CLAIM_VALID_VALUE)
            );

            options.AddPolicyEx("CanActAsGuest", policy =>
                policy.RequireClaim(DroHubUser.GUEST_POLICY_CLAIMS, DroHubUser.CLAIM_VALID_VALUE)
            );

            options.AddPolicyEx("CanCreateDevice", policy =>
                policy.RequireClaim("AllowDeviceCreate", DroHubUser.CLAIM_VALID_VALUE)
            );
        }
    }
}
