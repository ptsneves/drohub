using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using DroHub.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DroHub.Areas.DHub.Controllers {
    [Area("DHub")]
    public class AccountManagementController : Controller {
        private readonly SubscriptionAPI _subscription_api;
        private readonly SignInManager<DroHubUser> _signin_manager;
        private readonly ILogger<AccountManagementController> _logger;
        private readonly IAuthorizationService _authorization_service;
        private readonly IEmailSender _email_sender;
        private readonly IWebHostEnvironment _app_environment;

        public AccountManagementController(SubscriptionAPI subscription_api,
            SignInManager<DroHubUser> signin_manager, ILogger<AccountManagementController> logger,
            IAuthorizationService authorizationService, IEmailSender emailSender,
            IWebHostEnvironment appEnvironment) {

            _subscription_api = subscription_api;
            _signin_manager = signin_manager;
            _logger = logger;
            _authorization_service = authorizationService;
            _email_sender = emailSender;
            _app_environment = appEnvironment;
        }

        public async Task<IActionResult> Index() {
            var model = new AccountManagementModel {
                user_permissions = new List<AccountManagementModel.UserPermissionsModel>()
            };

            if (User.HasClaim(DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.CLAIM_VALID_VALUE)) {
                model.user_attributes = _subscription_api.getAvailableUserTypes();
            }

            var drohub_user = await _signin_manager.UserManager.GetUserAsync(_subscription_api.getClaimsPrincipal());
            model.user_email = drohub_user.Email;
            model.user_name = drohub_user.UserName;
            model.allowed_flight_time = await _subscription_api.getSubscriptionTimeLeft();
            model.allowed_users = await _subscription_api.getRemainingUserCount();
            model.subscription_name = _subscription_api.getSubscriptionName().Value;

            var subscription_users = await _subscription_api
                .getSubscriptionUsers(_subscription_api.getSubscriptionName())
                .ToListAsync();

            foreach(var user in subscription_users) {
                model.user_permissions.Add(new AccountManagementModel.UserPermissionsModel {
                    photo_src = "/AdminLTE/dist/img/user2-160x160.jpg",
                    user_name = user.UserName,
                    user_email = user.Email,
                    email_confirmed = user.EmailConfirmed,
                    user_type = user.BaseActingType.Replace("Acting", "").ToLower(),
                    last_login = ((DateTimeOffset)user.LastLogin.ToUniversalTime()).ToUnixTimeMilliseconds(),
                    can_be_excluded = (await _authorization_service.AuthorizeAsync(_subscription_api.getClaimsPrincipal(), user,
                    ResourceOperations.Delete)).Succeeded,
                    can_have_permissions_managed =(await _authorization_service.AuthorizeAsync(_subscription_api.getClaimsPrincipal(), user,
                        ResourceOperations.Update)).Succeeded
                });
            }

            return View(model);
        }

        public IActionResult SendInvitation() {
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async  Task<IActionResult> SendInvitation([Required]string[] emails) {
            if (!ModelState.IsValid)
                return BadRequest();

            emails = emails.Distinct().ToArray();

            if (emails.Any(email => !EmailValidator.IsValidEmail(email))) {
                return BadRequest();
            }

            var new_user = new DroHubUser {
                SubscriptionOrganizationName = _subscription_api.getSubscriptionName().Value,
                BaseActingType = DroHubUser.GUEST_POLICY_CLAIM,
            };

            if (!(await _authorization_service.AuthorizeAsync(_subscription_api.getClaimsPrincipal(), new_user,
                ResourceOperations.Create)).Succeeded)
                return Unauthorized();

            foreach (var email in emails) {

                // emails = _drohub_context.DroHubUsers.Select(u => u.Email).Except(emails).ToArray();
                var find_user = await _signin_manager.UserManager.FindByEmailAsync(email);
                if (find_user != null)
                    continue;

                try {
                    new_user.Email = email;
                    new_user.UserName = email;
                    new_user.CreationDate = DateTime.UtcNow;

                    var result = await _signin_manager.UserManager.CreateAsync(new_user);
                    if (!result.Succeeded) {
                        foreach (var error in result.Errors) {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                        continue;
                    }

                    var refresh_result = await DroHubUser.refreshClaims(_signin_manager, new_user);
                    if (refresh_result == IdentityResult.Failed())
                        _logger.LogError("Failed to refresh claims on creating a user.");

                    _logger.LogInformation("User created a new account without password for invitation.");

                    var confirmation_code = await _signin_manager.UserManager
                        .GenerateEmailConfirmationTokenAsync(new_user);

                    var callback_url = Url.Page(
                        "/Account/InvitationConfirmation",
                        "null",
                        new {
                            user_email = new_user.Email,
                            code = confirmation_code,
                            area = "Identity"
                        },
                        Request.Scheme
                        );

                    var template = await System.IO.File.ReadAllTextAsync(
                        Path.Join(_app_environment.WebRootPath, "mail", "EmailInvitation.html"));

                    template = template
                        .Replace("{{ USER }}", User.Identity.Name)
                        .Replace("{{ CODE }}", HtmlEncoder.Default.Encode(callback_url));


                    await _email_sender.SendEmailAsync(email, $"DROHUB.xyz - Activate your account",
                        template);
                    _logger.LogInformation("{who} invited {invited}", User.Identity.Name, email);
                }
                catch (Exception e) {
                    _logger.LogInformation(e.Message);
                }
            }
            return RedirectToAction("Index");
        }

        public IActionResult ChangeUserPermissions() {
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ChangeUserPermissions(
            [Required][DataType(DataType.EmailAddress, ErrorMessage = "E-mail is not valid")]string user_email,
            [Required]string permission_type) {

            if (!ModelState.IsValid)
                return BadRequest();

            if (!_subscription_api.getAvailableUserTypes().ContainsKey(permission_type))
                return BadRequest();

            var sanitized_acting_type = "Acting" + char.ToUpper(permission_type.First()) + permission_type.Substring(1).ToLower();

            var current_user = await _signin_manager.UserManager.GetUserAsync(_subscription_api.getClaimsPrincipal());

            var user = await _signin_manager.UserManager.FindByEmailAsync(user_email);


            if (user == null)
                return BadRequest();
            if (user.BaseActingType == sanitized_acting_type)
                return BadRequest();



            if (current_user.Email == user_email)
                return BadRequest();


            if (!(await _authorization_service.AuthorizeAsync(_subscription_api.getClaimsPrincipal(), user,
                ResourceOperations.Update)).Succeeded)
                return Unauthorized();

            user.BaseActingType = sanitized_acting_type;
            if (!(await _authorization_service.AuthorizeAsync(_subscription_api.getClaimsPrincipal(), user,
                ResourceOperations.Update)).Succeeded)
                return Unauthorized();

            var result = await _signin_manager.UserManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Unexpected error updating the permissions of user ${user_email}");


            if (await DroHubUser.refreshClaims(_signin_manager, user) == IdentityResult.Failed())
                _logger.LogError("Failed to refresh claims.");

            return RedirectToAction("Index");
        }

        public IActionResult ExcludeUser() {
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ExcludeUser(
                [Required][DataType(DataType.EmailAddress, ErrorMessage = "E-mail is not valid")]string user_email) {

            if (!ModelState.IsValid)
                return BadRequest();

            var current_user = await _signin_manager.UserManager.GetUserAsync(_subscription_api.getClaimsPrincipal());
            var victim_user = await _signin_manager.UserManager.FindByEmailAsync(user_email);
            if (victim_user == null)
                return BadRequest();

            if (current_user.Email == user_email)
                return BadRequest();


            if (!(await _authorization_service.AuthorizeAsync(_subscription_api.getClaimsPrincipal(), victim_user,
                ResourceOperations.Delete)).Succeeded)
                return Unauthorized();

            var result = await _signin_manager.UserManager.DeleteAsync(victim_user);
            if (!result.Succeeded) {
                throw new InvalidOperationException($"Unexpected error occurred deleting user with ID '{user_email}'.");
            }
            _logger.LogWarning("Removed {user_email}", user_email);

            try {
                await _subscription_api.deleteSubscription(
                    new SubscriptionAPI.OrganizationName(victim_user.SubscriptionOrganizationName));

            }
            catch (SubscriptionAPIException e) {
                _logger.LogInformation(e.Message);
            }

            return RedirectToAction("Index");
        }
    }
}