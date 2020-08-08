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
    [AllowAnonymous]
    public class CreateSubscription : PageModel {

        private class SubscriptionInfo {
            public int AllowedFlightTimeInMinutes { get; set; }
            public int AllowedUserCount{ get; set; }
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

        [BindProperty]
        public InputModel Input { get; set; }

        private bool isValidSubscriptionType(string subscription_type) {
            return _subscription_info.ContainsKey(subscription_type);
        }

        public string SubscriptionType { get; set; }

        private TimeSpan getFlightMinutes(string subscription_type) {
            if (_subscription_info.TryGetValue(subscription_type, out var subscription_info))
                return new TimeSpan(0, subscription_info.AllowedFlightTimeInMinutes, 0);
            throw new InvalidProgramException();
        }

        private int getAllowedUserCount(string subscription_type) {
            if (_subscription_info.TryGetValue(subscription_type, out var subscription_info))
                return subscription_info.AllowedUserCount;
            throw new InvalidProgramException();
        }

        public IActionResult OnGet([Required]string Type) {
            if (!ModelState.IsValid || !isValidSubscriptionType(Type))
                return BadRequest();

            SubscriptionType = Type;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync([Required]string Type) {
            if (!isValidSubscriptionType(Type))
                return BadRequest();

            if (!ModelState.IsValid)
                return Page();

            if (string.IsNullOrWhiteSpace(Input.OrganizationName)) {
                Input.OrganizationName = Guid.NewGuid().ToString();
            }

            if (Input.TermsAgreement == false) {
                ModelState.AddModelError(string.Empty, "You need to accept the conditions, to proceed.");
                return Page();
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
                AllowedFlightTime = getFlightMinutes(Type),
                AllowedUserCount = getAllowedUserCount(Type)
            };
            await _context.AddAsync(subscription);
            await _context.SaveChangesAsync();

            var user = new DroHubUser {
                UserName = Input.Email,
                Email = Input.Email,
                Subscription = subscription,
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

            foreach (var claim in DroHubUser.UserClaims[DroHubUser.SUBSCRIBER_POLICY_CLAIM])
                await _user_manager.AddClaimAsync(user, claim);

            await _user_manager.AddClaimAsync(user, new Claim(DroHubUser.SUBSCRIPTION_KEY_CLAIM,
                subscription.OrganizationName));

            await _sigin_manager.SignInAsync(user, true);

            return RedirectToAction("Index", "AccountManagement", new { area = "DHub" });
        }
    }
}