using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using DroHub.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DroHub.Areas.Identity.Pages.Account {
    [AttributeUsage(AttributeTargets.Property)]
    public class MustBeTrueAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            return value != null && value is bool && (bool)value;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class MustBeValidBillingPlan : ValidationAttribute
    {
        private Dictionary<string, CreateSubscription.SubscriptionInfo> _subscription_info;
        private bool isValidSubscriptionType(string subscription_type) {
            return _subscription_info.ContainsKey(subscription_type);
        }
        protected override ValidationResult IsValid(object value, ValidationContext context) {
            if (!(context.GetService(typeof(IConfiguration)) is IConfiguration configuration))
                throw new InvalidProgramException("No valid IConfiguration injected.");

            _subscription_info = configuration
                .GetSection("SubscriptionInfo")
                .Get<Dictionary<string, CreateSubscription.SubscriptionInfo>>();

            return value is string subscription_type && isValidSubscriptionType(subscription_type)
                ? ValidationResult.Success
                : new ValidationResult(FormatErrorMessage(context.DisplayName), null);
        }
    }

    [AllowAnonymous]
    public class CreateSubscription : PageModel {

        internal class SubscriptionInfo {
            public int AllowedFlightTimeInMinutes { get; set; }
            public int AllowedUserCount{ get; set; }

            public int AllowedVotes { get; set; }

            public int Price { get; set; }
        }

        public class InputModel {
            [Required]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string ConfirmPassword { get; set; }

            public string OrganizationName { get; set; }

            [Required]
            [MustBeTrue(ErrorMessage = "You must agree with the terms")]
            public bool TermsAgreement { get; set; }
        }

        private readonly ILogger<CreateSubscription> _logger;
        private readonly DroHubContext _context;
        private readonly UserManager<DroHubUser> _user_manager;
        private readonly Dictionary<string, SubscriptionInfo> _subscription_info;
        private readonly SignInManager<DroHubUser> _sigin_manager;

        public CreateSubscription(IConfiguration configuration,
            DroHubContext context, UserManager<DroHubUser> userManager, SignInManager<DroHubUser> siginManager, ILogger<CreateSubscription> logger) {
            _context = context;
            _user_manager = userManager;
            _sigin_manager = siginManager;
            _logger = logger;
            _subscription_info = configuration
                .GetSection("SubscriptionInfo")
                .Get<Dictionary<string, SubscriptionInfo>>();
        }

        public int Price {
            get {
                if (_subscription_info.TryGetValue(Type, out var subscription_info))
                    return subscription_info.Price;
                throw new InvalidProgramException();
            }
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [Required]
        [MustBeValidBillingPlan(ErrorMessage = "Billing Plan is not valid. Go back to the main page.")]
        [BindProperty(SupportsGet = true)]
        public string Type { get; set; } = "FREE";

        public TimeSpan getFlightMinutes() {
            if (_subscription_info.TryGetValue(Type, out var subscription_info))
                return new TimeSpan(0, subscription_info.AllowedFlightTimeInMinutes, 0);
            throw new InvalidProgramException();
        }

        public int getAllowedUserCount() {
            if (_subscription_info.TryGetValue(Type, out var subscription_info))
                return subscription_info.AllowedUserCount;
            throw new InvalidProgramException();
        }

        public IActionResult OnGet() {
            if (!ModelState.IsValid)
                return BadRequest();

            this.Type = Type;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync() {
            if (!ModelState.IsValid)
                return Page();

            if (string.IsNullOrWhiteSpace(Input.OrganizationName)) {
                Input.OrganizationName = Guid.NewGuid().ToString();
            }

            if (Input.Password != Input.ConfirmPassword) {
                ModelState.AddModelError(string.Empty, "Password does not match confirmation password");
                return Page();
            }

            var is_organization_name_valid = !await _context.Subscriptions
                .AnyAsync(s => s.OrganizationName == Input.OrganizationName);

            if (!is_organization_name_valid) {
                ModelState.AddModelError(string.Empty, "Organization Name already exists");
                return Page();
            }

            var subscription = new Subscription() {
                OrganizationName = Input.OrganizationName,
                AllowedFlightTime = getFlightMinutes(),
                AllowedUserCount = getAllowedUserCount(),
                BillingPlanName = Type
            };
            await _context.AddAsync(subscription);
            await _context.SaveChangesAsync();

            var user = new DroHubUser {
                UserName = Input.Email,
                Email = Input.Email,
                Subscription = subscription,
                SubscriptionOrganizationName = subscription.OrganizationName,
                BaseActingType = DroHubUser.SUBSCRIBER_POLICY_CLAIM,
                EmailConfirmed = true
            };

            var result = await _user_manager.CreateAsync(user, Input.Password);
            if (!result.Succeeded) {
                foreach (var error in result.Errors) {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            _logger.LogInformation("User created a new account with password.");

            await DroHubUser.refreshClaims(_sigin_manager, user);
            await _sigin_manager.SignInAsync(user, true);

            return RedirectToAction("Index", "AccountManagement", new { area = "DHub" });
        }
    }
}