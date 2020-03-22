using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using DroHub.Data;
using DroHub.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DroHub.Areas.Identity.Pages.Account
{
    [ClaimRequirement(DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.CLAIM_VALID_VALUE)]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<DroHubUser> _signin_manager;
        private readonly UserManager<DroHubUser> _user_manager;
        private readonly DroHubContext _db_context;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _email_sender;

        public UserManager<DroHubUser> UserManager => _user_manager;
        public List<DroHubUser> Users { get; private set; }
        public string CurrentUserOrganizationName { get; private set; }

        public RegisterModel(
            UserManager<DroHubUser> user_manager,
            SignInManager<DroHubUser> signin_manager,
            DroHubContext db_context,
            ILogger<RegisterModel> logger,
            IEmailSender email_sender) {
            Users = new List<DroHubUser>();
            _user_manager = user_manager;
            _signin_manager = signin_manager;
            _db_context = db_context;
            _logger = logger;
            _email_sender = email_sender;
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

            [Display(Name = "Name of your organization")]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 2)]
            [DataType(DataType.Text)]
            public string OrganizationName { get; set; }

            [Display(Name = "Allowed Organization Flight Time in Minutes")]
            [Range(1, 50338)] // MySQL allows maximum 838:59:59.000000 so in minutes 838*60+58 = 50338 minutes
            public int? AllowedFlightTime { get; set; }

            [Display(Name = "Number of allowed users")]
            [Range(1, Int32.MaxValue)]
            public int? AllowedUserCount { get; set; }
        }

        private async Task prepareProperties(){
            var user_id = _user_manager.GetUserId(User);
            var can_see_not_own_subs = HttpContext.User.HasClaim(Subscription.CAN_SEE_NOT_OWN_SUBSCRIPTION,
                Subscription.CLAIM_VALID_VALUE);

            if (!can_see_not_own_subs) {
                var cur_user = await _user_manager.Users
                    .Include(u => u.Subscription)
                    .SingleAsync(u => u.Id == user_id);

                CurrentUserOrganizationName = cur_user.Subscription.OrganizationName;
            }

            Users = _user_manager.Users
                .Include(u => u.Subscription)
                .ThenInclude(s => s.Devices)
                .Where(u => can_see_not_own_subs || u.Subscription.OrganizationName == CurrentUserOrganizationName)
                .ToList();
        }

        public async Task OnGetAsync(string return_url = null){
            await prepareProperties();
            ReturnUrl = return_url;
        }

        public SelectList getAuthorizedUsersToAdd() {

            var authorized_roles = new List<string>();

            if (User.HasClaim(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.CLAIM_VALID_VALUE))
                authorized_roles.Add(DroHubUser.ADMIN_POLICY_CLAIM);

            if (User.HasClaim(DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.CLAIM_VALID_VALUE))
                authorized_roles.Add(DroHubUser.SUBSCRIBER_POLICY_CLAIM);

            if (User.HasClaim(DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.CLAIM_VALID_VALUE))
                authorized_roles.Add(DroHubUser.OWNER_POLICY_CLAIM);

            authorized_roles.Add(DroHubUser.PILOT_POLICY_CLAIM);

            authorized_roles.Add(DroHubUser.GUEST_POLICY_CLAIM);
            return new SelectList(authorized_roles);
        }

        private bool isValidActingClaim(){
            var authorized_users_to_add = getAuthorizedUsersToAdd();

            if (DroHubUser.UserClaims.ContainsKey(Input.ActingType) && authorized_users_to_add.Any(i => i.Text == Input.ActingType))
                return true;

            ModelState.AddModelError("InvalidPolicy", "You chose an invalid option for user act");
            return false;
        }

        private bool isAuthorizedToAddSubscription()
        {
            if (User.HasClaim(Subscription.CAN_ADD_CLAIM, Subscription.CLAIM_VALID_VALUE))
                return true;

            return false;
        }

        private bool isAuthorizedToAddUserToSubscription() {
            if (User.HasClaim(Subscription.CAN_EDIT_USERS_IN_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE))
                return true;

            ModelState.AddModelError("Not authorized", "Not authorized to edit users in subscription");
            return false;
        }

        private bool doesSubscriptionAuthorizeNewUser(Subscription subscription) {
            if (subscription.AllowedUserCount > subscription.Users.Count)
                return true;

            ModelState.AddModelError("Not authorized",
                $"You have reached the maximum number of users in your subscription of {subscription.AllowedUserCount}");
            return false;
        }

        private class UserCreateException : InvalidOperationException {
        }

        [ClaimRequirement(Subscription.CAN_EDIT_USERS_IN_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE)]
        private async Task<Subscription> createSubscriptionOrDefault(){
            var subscription = await _db_context.Subscriptions.SingleOrDefaultAsync(
                d => d.OrganizationName == Input.OrganizationName);

            if (subscription != null) return subscription;

            if (Input.AllowedFlightTime == null || Input.AllowedUserCount == null || Input.OrganizationName == null)
            {
                ModelState.AddModelError(string.Empty,
                    "Allowed Organization Name, Flight time and Allowed User Count are mandatory");
                throw new UserCreateException();
            }

            subscription = new Subscription()
            {
                OrganizationName = Input.OrganizationName,
                AllowedFlightTime = TimeSpan.FromMinutes((double) Input.AllowedFlightTime),
                AllowedUserCount = (int) Input.AllowedUserCount
            };
            _db_context.Subscriptions.Add(subscription);
            await _db_context.SaveChangesAsync();

            return subscription;
        }

        public async Task<IActionResult> OnPostAsync(string return_url = null)
        {
            try {
                await prepareProperties();
                if (!ModelState.IsValid)
                    throw new UserCreateException();
                if (!isValidActingClaim())
                    throw new UserCreateException();

                Subscription subscription;
                if (isAuthorizedToAddSubscription()) {
                    subscription = await createSubscriptionOrDefault();
                }
                else {
                    if (!isAuthorizedToAddUserToSubscription())
                        throw new UserCreateException();

                    var cur_user = await _user_manager.GetUserAsync(User);
                    subscription = cur_user.Subscription;
                    if (!doesSubscriptionAuthorizeNewUser(subscription))
                        throw new UserCreateException();
                }

                var user = new DroHubUser {
                    UserName = Input.Email,
                    Email = Input.Email,
                    Subscription = subscription,
                    BaseActingType = Input.ActingType
                };

                var result = await _user_manager.CreateAsync(user, Input.Password);
                if (!result.Succeeded) {
                    foreach (var error in result.Errors) {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    throw new UserCreateException();
                }

                _logger.LogInformation("User created a new account with password.");

                var code = await _user_manager.GenerateEmailConfirmationTokenAsync(user);
                var callback_url = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new {userId = user.Id},
                    protocol: Request.Scheme);

                await _email_sender.SendEmailAsync(Input.Email, "Confirm your email",
                    $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callback_url)}'>clicking here</a>.");


                foreach (var claim in DroHubUser.UserClaims[Input.ActingType])
                    await _user_manager.AddClaimAsync(user, claim);

                await _signin_manager.SignInAsync(user, isPersistent: false);

                return_url ??= Url.Content("~/");
                return LocalRedirect(return_url);
            }
            catch (UserCreateException) {
                return Page();
            }
        }
    }
}
