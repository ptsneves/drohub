using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DroHub.Areas.DHub.Controllers {
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class GetTokenController : ControllerBase {

        private const string _APP_TOKEN_PURPOSE = "DroHubApp";
        private static readonly string TokenProvider = TokenOptions.DefaultProvider;
        private readonly SignInManager<DroHubUser> _signin_manager;

        public GetTokenController(SignInManager<DroHubUser> signin_manager) {
            _signin_manager = signin_manager;
        }

        public class GetTokenModel
        {
            [Required]
            public string UserName { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }
        }


        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> GetApplicationToken([FromBody]GetTokenModel get_token)
        {
            var result = await _signin_manager.PasswordSignInAsync(get_token.UserName, get_token.Password,
                false, true);
            var response = new Dictionary<string, string> {["result"] = "nok"};
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