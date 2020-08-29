using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Identity;

namespace DroHub.Areas.Identity.Data
{
    // Add profile data for application users by adding properties to the DroHubUser class
    public class DroHubUser : IdentityUser
    {
        public const string CLAIM_VALID_VALUE = "Yes";

        public const string BASE_ACTING_TYPE_KEY_CLAIM = "ActingType";
        public const string SUBSCRIPTION_KEY_CLAIM = "SubscriptionOrgName";
        public const string ADMIN_POLICY_CLAIM = "ActingAdmin";
        public const string SUBSCRIBER_POLICY_CLAIM = "ActingSubscriber";
        public const string OWNER_POLICY_CLAIM = "ActingOwner";
        public const string PILOT_POLICY_CLAIM = "ActingPilot";
        public const string GUEST_POLICY_CLAIM = "ActingGuest";

        public static string  convertActingTypeToHumanFormat(string acting_type) {
            return acting_type.Replace("Acting", "");
        }

        private static readonly List<Claim> GuestPolicyClaims = new List<Claim>() {
            new Claim(GUEST_POLICY_CLAIM, CLAIM_VALID_VALUE),
            DeviceAuthorizationHandler.DeviceResourceOperations.CameraActionsClaim,
            DeviceAuthorizationHandler.DeviceResourceOperations.ReadClaim,
            MediaObjectAuthorizationHandler.MediaObjectResourceOperations.ReadClaim,
        };

        private static readonly List<Claim> PilotPolicyClaims = new List<Claim>(GuestPolicyClaims) {
            new Claim(PILOT_POLICY_CLAIM, CLAIM_VALID_VALUE),
            DeviceAuthorizationHandler.DeviceResourceOperations.FlightActionsClaim,
            DeviceAuthorizationHandler.DeviceResourceOperations.CreateClaim,
            DeviceAuthorizationHandler.DeviceResourceOperations.UpdateClaim,
            DeviceAuthorizationHandler.DeviceResourceOperations.DeleteClaim,
        };

        private static readonly List<Claim> OwnerPolicyClaims = new List<Claim>(PilotPolicyClaims) {
            new Claim(OWNER_POLICY_CLAIM, CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_SEE_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_EDIT_USERS_IN_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
            MediaObjectAuthorizationHandler.MediaObjectResourceOperations.DeleteClaim,
        };

        private static readonly List<Claim> SubscriberPolicyClaims = new List<Claim>(OwnerPolicyClaims) {
            new Claim(SUBSCRIBER_POLICY_CLAIM, CLAIM_VALID_VALUE),
        };

        private static readonly List<Claim> AdminPolicyClaims = new List<Claim>(SubscriberPolicyClaims) {
            new Claim(ADMIN_POLICY_CLAIM, CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_MODIFY_CLAIM, Subscription.CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_ADD_CLAIM, Subscription.CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_SEE_NOT_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_EDIT_USERS_OUTSIDE_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
        };

        public static readonly Dictionary<string, List<Claim>> UserClaims = new Dictionary<string, List<Claim>>
        {
            { ADMIN_POLICY_CLAIM, AdminPolicyClaims },
            { SUBSCRIBER_POLICY_CLAIM, SubscriberPolicyClaims },
            { OWNER_POLICY_CLAIM, OwnerPolicyClaims },
            { PILOT_POLICY_CLAIM, PilotPolicyClaims },
            { GUEST_POLICY_CLAIM, GuestPolicyClaims }
        };

        public string BaseActingType { get; set; }
        public DateTime LastLogin { get; set; }

        public DateTime CreationDate { get; set; }

        public Subscription Subscription { get; set; }
        public string SubscriptionOrganizationName { get; set; }

        public static async Task<IdentityResult> refreshClaims(SignInManager<DroHubUser> sign_in_manager,
            DroHubUser user, string acting_type) {

            if (!UserClaims.ContainsKey(acting_type))
                return IdentityResult.Failed(new IdentityError {Description = "Invalid acting type"});

            var old_claims = await sign_in_manager.UserManager.GetClaimsAsync(user);
            var remove_result = await sign_in_manager.UserManager.RemoveClaimsAsync(user, old_claims);
            if (!remove_result.Succeeded)
                return remove_result;

            var add_result = await sign_in_manager.UserManager.AddClaimsAsync(user, UserClaims[acting_type]);
            var add_orgname_result = await sign_in_manager.UserManager.AddClaimAsync(user,
                new Claim(SUBSCRIPTION_KEY_CLAIM, user.SubscriptionOrganizationName));
            if (add_result == IdentityResult.Failed())
                return add_result;
            return add_orgname_result == IdentityResult.Failed() ? add_orgname_result : IdentityResult.Success;
        }
    }
}
