using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;

namespace DroHub.Areas.Identity.Pages.Account
{
    [Authorize(Policy = "CanActAsOwner")]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<DroHubUser> _signInManager;
        private readonly UserManager<DroHubUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        public UserManager<DroHubUser> UserManager { get { return _userManager; } }
        public List<DroHubUser> Users { get; private set; }

        public RegisterModel(
            UserManager<DroHubUser> userManager,
            SignInManager<DroHubUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            Users = _userManager.Users.Include(u => u.UserDevices).ThenInclude(ud => ud.Device).ToList();
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 1)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
            [Required]
            [Display(Name = "User wil act as")]
            [DataType(DataType.Text)]
            public string ActingType { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public static async Task<IEnumerable<SelectListItem>> getAuthorizedUsersToAdd(
                IAuthorizationService authorization_service, IUserClaimsPrincipalFactory<DroHubUser> claims_principal_factory,
                DroHubUser user) {
            var authorized_roles = new List<SelectListItem>();
            var s = await claims_principal_factory.CreateAsync(user);

            var authorized = (await authorization_service.AuthorizeAsync(s, null,
                "CanActAsAdmin")).Succeeded;
            authorized_roles.Add(new SelectListItem(DroHubUser.ADMIN_POLICY_CLAIMS,
                DroHubUser.ADMIN_POLICY_CLAIMS, authorized));

            authorized = (await authorization_service.AuthorizeAsync(s, null,
                "CanActAsSubscriber")).Succeeded;
            authorized_roles.Add(new SelectListItem(DroHubUser.SUBSCRIBER_POLICY_CLAIMS,
                DroHubUser.SUBSCRIBER_POLICY_CLAIMS, authorized));

            authorized = (await authorization_service.AuthorizeAsync(s, null, "CanActAsOwner")).Succeeded;
            authorized_roles.Add(new SelectListItem(DroHubUser.OWNER_POLICY_CLAIMS,
                DroHubUser.OWNER_POLICY_CLAIMS, authorized));

            authorized_roles.Add(new SelectListItem(DroHubUser.PILOT_POLICY_CLAIMS,
                DroHubUser.PILOT_POLICY_CLAIMS, true));

            authorized_roles.Add(new SelectListItem(DroHubUser.GUEST_POLICY_CLAIMS,
                DroHubUser.GUEST_POLICY_CLAIMS, true));
            return authorized_roles;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            if (!DroHubUser.UserClaims.ContainsKey(Input.ActingType))
                ModelState.AddModelError("InvalidPolicy", "You chose an invalid option for user act");
            else if (ModelState.IsValid)
            {
                var user = new DroHubUser { UserName = Input.Email, Email = Input.Email };
                var result = await _userManager.CreateAsync(user, Input.Password);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { userId = user.Id, code = code },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    foreach (var claim in DroHubUser.UserClaims[Input.ActingType])
                        await _userManager.AddClaimAsync(user, claim);

                    await _signInManager.SignInAsync(user, isPersistent: false);

                    return LocalRedirect(returnUrl);
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
