using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Web.Http;
using System.Collections.Generic;

namespace DroHub.Areas.DHub.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class UserController : ControllerBase
    {

        public class InputModel
        {
            [Required]
            public string UserName { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }
        }

        private SignInManager<DroHubUser> _signin_manager;
        public UserController(SignInManager<DroHubUser> signin_manager) : base()
        {
            _signin_manager = signin_manager;
            Console.WriteLine("sadss");
        }

        [AllowAnonymous]
        [HttpPost]
        
        public async Task<IActionResult> Authenticate([FromBody]InputModel input)
        {
            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, set lockoutOnFailure: true
            Console.WriteLine("he.");
            var result = await _signin_manager.PasswordSignInAsync(input.UserName, input.Password, false, lockoutOnFailure: true);
            var response = new Dictionary<string, string>();
            if (result.Succeeded)
            {
                response["result"] = "ok";
                return new JsonResult(response);
            }
            else
            {
                response["result"] = "nok";
                Console.WriteLine("hen");
                return new JsonResult(response);
            }
        }
    }
}