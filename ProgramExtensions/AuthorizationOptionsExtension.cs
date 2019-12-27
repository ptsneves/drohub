using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System;
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

        public static void ConfigureAuthorizationOptions(IServiceCollection services) {
            services.AddAuthorization(options =>
            {
                options.AddIsAdminPolicy();
                foreach (var pair in Policies)
                {
                    options.AddPolicy(pair.Key, pair.Value);
                }
            });
        }
    }
}