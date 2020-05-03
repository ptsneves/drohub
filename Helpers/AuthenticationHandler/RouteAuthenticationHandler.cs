using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DroHub.Helpers.AuthenticationHandler {
    public struct AuthenticationSchemeRoute {
        public string SchemeName { get; set; }
        public IEnumerable<PathString> StartPaths { get; set; }
    }
    public static class RouteAuthenticationSchemeExtensions {
        public static AuthenticationBuilder RouteAuthentication (this AuthenticationBuilder builder,
            string default_scheme, IEnumerable<AuthenticationSchemeRoute> scheme_routes) {

            return builder.AddScheme<AuthenticationSchemeOptions, RouteAuthenticationHandler>(
                RouteAuthenticationHandler.SchemeName, options => {
                    options.ForwardDefaultSelector = ctx => {
                        foreach (var route in scheme_routes) {
                            if (route.StartPaths.Any(path => ctx.Request.Path.StartsWithSegments(path))) {
                                return route.SchemeName;
                            }
                        }
                        return default_scheme;
                    };
                });
        }
    }

    public class RouteAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions> {
        public const string SchemeName = "RouteAuthenticationScheme";
        public RouteAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock) {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
            throw new NotImplementedException();
        }
    }
}