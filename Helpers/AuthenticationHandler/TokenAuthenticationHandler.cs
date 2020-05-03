using System.Text.Encodings.Web;
using System.Threading.Tasks;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DroHub.Helpers.AuthenticationHandler {
    public static class TokenAuthenticationExtensions {
        public static AuthenticationBuilder AddTokenAuthentication (this AuthenticationBuilder builder) {
            return builder.AddScheme<AuthenticationSchemeOptions, TokenAuthenticationHandler>(
                TokenAuthenticationHandler.SchemeName, options => {});
        }
    }

    public class TokenAuthenticationHandler : TokenAuthenticationBase {
        public const string SchemeName = "Token";

        public TokenAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
            SignInManager<DroHubUser> sign_in_manager,UrlEncoder encoder, ISystemClock clock) :
            base(options, logger, sign_in_manager, encoder, clock) {
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync() {
            return await handleAuthenticatepPrivAsync();
        }
    }
}