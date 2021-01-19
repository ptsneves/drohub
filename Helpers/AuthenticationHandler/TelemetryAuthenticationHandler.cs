using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace DroHub.Helpers.AuthenticationHandler {
    public static class TelemetryAuthenticationExtensions {
        public static AuthenticationBuilder AddTelemetryAuthentication (this AuthenticationBuilder builder) {
            return builder.AddScheme<AuthenticationSchemeOptions, TelemetryAuthenticationHandler>(
                TelemetryAuthenticationHandler.SchemeName, options => {});
        }
    }

    public class TelemetryAuthenticationHandler : TokenAuthenticationBase {
        private readonly ILogger _logger;
        public const string SchemeName = "ThriftAuthentication";

        public TelemetryAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
            SignInManager<DroHubUser> sign_in_manager, UrlEncoder encoder, ISystemClock clock) :
            base(options, logger, sign_in_manager, encoder, clock) {

            _logger = logger.CreateLogger(typeof(TelemetryAuthenticationHandler));
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync() {
            var res = await handleAuthenticatepPrivAsync();
            if (!res.Succeeded)
                return res;
            var serial_header = Request.Headers["x-device-expected-serial"];
            if (serial_header == StringValues.Empty) {
                _logger.LogInformation("User was authenticated but did not provide a serial header");
                return AuthenticateResult.Fail(FAIL_MESSAGE);
            }

            if (res.Ticket.Principal.Identity is ClaimsIdentity)
                return res;

            _logger.LogInformation("Identity type different than claims identity are not supported");
            return AuthenticateResult.Fail(FAIL_MESSAGE);

            // identity.AddClaim(new Claim(Device.TELEMETRY_SERIAL_NUMBER_CLAIM, serial_header));
        }
    }
}