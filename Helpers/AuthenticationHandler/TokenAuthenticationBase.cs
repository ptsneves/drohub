using System.Linq;
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

    public abstract class TokenAuthenticationBase  : AuthenticationHandler<AuthenticationSchemeOptions> {
        private readonly SignInManager<DroHubUser> _signin_manager;
        private readonly ILogger _logger;

        internal TokenAuthenticationBase(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
            SignInManager<DroHubUser> sign_in_manager,UrlEncoder encoder, ISystemClock clock) :
            base(options, logger, encoder, clock) {
            _signin_manager = sign_in_manager;
            _logger = logger.CreateLogger(typeof(TokenAuthenticationHandler));
        }

        protected const string FAIL_MESSAGE = "Authentication Failed";

        private async Task<bool> isTokenValid(string user_name, string token) {
            var user = await _signin_manager.UserManager.FindByNameAsync(user_name);
            if (user == null)
                return false;

            var claims = await _signin_manager.UserManager.GetClaimsAsync(user);
            if (!claims.Any(c =>
                c.Type == DroHubUser.PILOT_POLICY_CLAIM && c.Value == DroHubUser.CLAIM_VALID_VALUE)) {
                return false;
            }

            return user.PasswordHash == token;
        }

        protected async Task<AuthenticateResult> handleAuthenticatepPrivAsync() {
            if (Request.Headers["x-drohub-user"] == StringValues.Empty ||
                Request.Headers["x-drohub-token"] == StringValues.Empty) {
                _logger.LogInformation("User did not provide a user or a token");
                return AuthenticateResult.Fail(FAIL_MESSAGE);
            }

            var user_name = Request.Headers["x-drohub-user"];
            var token = Request.Headers["x-drohub-token"];

            var user = await _signin_manager.UserManager.FindByNameAsync(Request.Headers["x-drohub-user"]);
            if (user == null) {
                _logger.LogInformation("User is not registered");
                return AuthenticateResult.Fail(FAIL_MESSAGE);
            }

            if (!await isTokenValid(user_name, token)) {
                _logger.LogInformation("Token is not valid");
                return AuthenticateResult.Fail(FAIL_MESSAGE);
            }

            var user_principal = await _signin_manager.CreateUserPrincipalAsync(user);
            var ticket = new AuthenticationTicket(user_principal, Scheme.Name);

            if (Request.Headers["x-device-expected-serial"] != StringValues.Empty) {
                var identity = user_principal.Identity as ClaimsIdentity;
                identity?.AddClaim(new Claim("AuthorizedDeviceSerialNumber",
                    Request.Headers["x-device-expected-serial"]));
            }

            return AuthenticateResult.Success(ticket);
        }
    }
}