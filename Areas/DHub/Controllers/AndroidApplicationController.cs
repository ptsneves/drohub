using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using DroHub.Data;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DroHub.Areas.DHub.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class AndroidApplicationController : ControllerBase
    {
        private const string _APP_TOKEN_PURPOSE = "DroHubApp";
        private static readonly string TokenProvider = TokenOptions.DefaultProvider;

        public class GetTokenModel
        {
            [Required]
            public string UserName { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }
        }

        public class AuthenticateTokenModel {
            [Required]
            public string UserName { get; set; }

            [Required]
            public string Token { get; set; }
        }

        public class QueryDeviceInfoModel : AuthenticateTokenModel {
            [Required]
            public string DeviceSerialNumber { get; set; }
        }

        public class DeviceCreateModel : AuthenticateTokenModel {
            [Required]
            public Device Device { get; set; }
        }

        private readonly DroHubContext _context;
        private readonly SignInManager<DroHubUser> _signin_manager;
        public AndroidApplicationController(DroHubContext context, SignInManager<DroHubUser> signin_manager) : base() {
            _context = context;
            _signin_manager = signin_manager;
        }

        public static async Task<bool> authenticateToken(SignInManager<DroHubUser> sign_in_manager, string user_name,
            string token) {
            var user = await sign_in_manager.UserManager.FindByNameAsync(user_name);
            var claims = await sign_in_manager.UserManager.GetClaimsAsync(user);
            if (!claims.Any(c =>
                c.Type == DroHubUser.PILOT_POLICY_CLAIM && c.Value == DroHubUser.CLAIM_VALID_VALUE)) {
                return false;
            }
            var verified = await sign_in_manager.UserManager.VerifyUserTokenAsync(user, TokenProvider, _APP_TOKEN_PURPOSE,
                token);
            if (verified)
                await sign_in_manager.SignInAsync(user, false);

            return verified;
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> CreateDevice([FromBody] DeviceCreateModel device_model) {
            if (!await authenticateToken(_signin_manager, device_model.UserName, device_model.Token)) {
                return BadRequest();
            }

            var user = await _signin_manager.UserManager.FindByNameAsync(device_model.UserName);
            device_model.Device.Subscription = await DroHubUserLinqExtensions
                .getCurrentUserWithSubscription((_signin_manager.UserManager),
                    await _signin_manager.CreateUserPrincipalAsync(user))
                .getCurrentUserSubscription()
                .SingleAsync();
            await DevicesController.Create(_context, device_model.Device);
            return new JsonResult(new Dictionary<string, string>() {{"result", "ok"}});
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> QueryDeviceInfo([FromBody] QueryDeviceInfoModel authenticate_token) {
            if (!await authenticateToken(_signin_manager, authenticate_token.UserName, authenticate_token.Token)) {
                return BadRequest();
            }

            var user = await _signin_manager.UserManager.FindByNameAsync(authenticate_token.UserName);
            var r = await DevicesController.getDeviceBySerial(_signin_manager.UserManager,
                await _signin_manager.CreateUserPrincipalAsync(user),
                authenticate_token.DeviceSerialNumber);
            var response = new Dictionary<string, Device> {
                ["result"] = r
            };
            return new JsonResult(response);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> AuthenticateToken([FromBody] AuthenticateTokenModel authenticate_token) {
            var response = new Dictionary<string, string>();
            if (await authenticateToken(_signin_manager, authenticate_token.UserName, authenticate_token.Token))
                response["result"] = "ok";
            else
                response["result"] = "nok";
            return new JsonResult(response);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> GetApplicationToken([FromBody]GetTokenModel get_token)
        {
            var result = await _signin_manager.PasswordSignInAsync(get_token.UserName, get_token.Password,
                false, true);
            var response = new Dictionary<string, string>();
            response["result"] = "nok";
            if (!result.Succeeded)
                return new JsonResult(response);

            var user = await _signin_manager.UserManager.FindByNameAsync(get_token.UserName);
            var claims = await _signin_manager.UserManager.GetClaimsAsync(user);
            if (!claims.Any(c =>
                c.Type == DroHubUser.PILOT_POLICY_CLAIM && c.Value == DroHubUser.CLAIM_VALID_VALUE)) {
                return new JsonResult(response);
            }

            var token = await _signin_manager
                .UserManager
                .GenerateUserTokenAsync(user, TokenProvider, _APP_TOKEN_PURPOSE);

            if (string.IsNullOrEmpty(token))
                return BadRequest();
            response["result"] = token;

            return new JsonResult(response);
        }
    }
}