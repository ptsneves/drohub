using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Controllers;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace DroHub.Areas.Identity.Pages.Account {
    [AllowAnonymous]
    public class InvitationConfirmation : PageModel {
        public class InvitationConfirmationModel {
            [Required]
            public string verification_code { get; set;}

            [Required]
            [DataType(DataType.EmailAddress, ErrorMessage = "E-mail is not valid")]
            public string user_email { get; set;}

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "New password")]
            public string new_password { get; set;}

            [Required] [DataType(DataType.Password)]
            [Display(Name = "Confirmed New password")]
            public string confirmed_new_password { get; set;}
        }
        private readonly SignInManager<DroHubUser> _signin_manager;
        private readonly ILogger<InvitationConfirmation> _logger;

        [Required]
        [BindProperty]
        public InvitationConfirmationModel Input { get; set; }

        [TempData]
        public string VerificationCode { get; set; }

        [TempData]
        public string UserEmail { get; set; }

        public InvitationConfirmation(SignInManager<DroHubUser> signinManager, ILogger<InvitationConfirmation> logger) {
            _signin_manager = signinManager;
            _logger = logger;
            Input = null;
        }

        public async Task<IActionResult> OnGetAsync([Required] string user_email, [Required] string code) {
            if (!ModelState.IsValid)
                return BadRequest();

            var user = await _signin_manager.UserManager.FindByEmailAsync(user_email);
            if (user == null) {
                return NotFound($"Unable to load user with ID '{user_email}'.");
            }

            if (user.EmailConfirmed) {
                return Page();
            }

            UserEmail = user_email;
            VerificationCode = code;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync() {
            if (!ModelState.IsValid)
                return Page();

            if (Input.new_password != Input.confirmed_new_password) {
                ModelState.AddModelError(string.Empty, "Inserted password and confirmed one do not match");
                return Page();
            }

            var user = await _signin_manager.UserManager.FindByEmailAsync(Input.user_email);
            if (user == null || user.PasswordHash != null) {
                return NotFound($"Unable to load user with email '{Input.user_email}'.");
            }

            var result = await _signin_manager.UserManager.ConfirmEmailAsync(user, Input.verification_code);
            if (!result.Succeeded) {
                ModelState.AddModelError(string.Empty,$"Error confirming email for user with ID '{Input.user_email}':");
                return Page();
            }

            var change_password_result = await _signin_manager.UserManager
                .AddPasswordAsync(user, Input.new_password);

            if (!change_password_result.Succeeded) {
                foreach (var error in change_password_result.Errors) {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            await _signin_manager.RefreshSignInAsync(user);

            await _signin_manager.SignInAsync(user, false);

            user.LastLogin = DateTimeOffset.UtcNow;
            await _signin_manager.UserManager.UpdateAsync(user);

            _logger.LogInformation("User changed it's password successfully.");

            return RedirectToAction("Index", "AccountManagement", new { area = "DHub" });
        }
    }
}