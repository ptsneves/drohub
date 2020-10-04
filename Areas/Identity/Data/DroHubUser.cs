using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
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

        private static readonly ReadOnlyCollection<Claim> GuestPolicyClaims = new List<Claim>() {
            new Claim(GUEST_POLICY_CLAIM, CLAIM_VALID_VALUE),
            DeviceAuthorizationHandler.DeviceResourceOperations.CameraActionsClaim,
            DeviceAuthorizationHandler.DeviceResourceOperations.ReadClaim,
            MediaObjectAuthorizationHandler.MediaObjectResourceOperations.ReadClaim,
        }.AsReadOnly();

        private static readonly ReadOnlyCollection<Claim> PilotPolicyClaims = new List<Claim>(GuestPolicyClaims) {
            new Claim(PILOT_POLICY_CLAIM, CLAIM_VALID_VALUE),
            DeviceAuthorizationHandler.DeviceResourceOperations.FlightActionsClaim,
            DeviceAuthorizationHandler.DeviceResourceOperations.CreateClaim,
            DeviceAuthorizationHandler.DeviceResourceOperations.UpdateClaim,
            DeviceAuthorizationHandler.DeviceResourceOperations.DeleteClaim,
        }.AsReadOnly();

        private static readonly ReadOnlyCollection<Claim> OwnerPolicyClaims = new List<Claim>(PilotPolicyClaims) {
            new Claim(OWNER_POLICY_CLAIM, CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_SEE_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_EDIT_USERS_IN_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
            MediaObjectAuthorizationHandler.MediaObjectResourceOperations.DeleteClaim,
        }.AsReadOnly();

        private static readonly ReadOnlyCollection<Claim> SubscriberPolicyClaims = new List<Claim>(OwnerPolicyClaims) {
            new Claim(SUBSCRIBER_POLICY_CLAIM, CLAIM_VALID_VALUE),
        }.AsReadOnly();

        private static readonly ReadOnlyCollection<Claim> AdminPolicyClaims = new List<Claim>(SubscriberPolicyClaims) {
            new Claim(ADMIN_POLICY_CLAIM, CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_MODIFY_CLAIM, Subscription.CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_ADD_CLAIM, Subscription.CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_SEE_NOT_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_EDIT_USERS_OUTSIDE_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
        }.AsReadOnly();

        public static readonly IReadOnlyDictionary<string, IReadOnlyCollection<Claim>> UserClaims =
            new Dictionary<string, IReadOnlyCollection<Claim>> {
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
            DroHubUser user) {
            IdentityResult ret;
            var user_manager = sign_in_manager.UserManager;

            if (!UserClaims.ContainsKey(user.BaseActingType))
                return IdentityResult.Failed(new IdentityError {Description = "Invalid acting type"});

            var old_claims = new ReadOnlyCollectionBuilder<Claim>(await user_manager.GetClaimsAsync(user))
                    .ToReadOnlyCollection();
            ret = await user_manager.RemoveClaimsAsync(user, old_claims);
            if (ret == IdentityResult.Failed()) {
                return ret;
            }

            var claims = new ReadOnlyCollectionBuilder<Claim>(UserClaims[user.BaseActingType]) {
                new Claim(SUBSCRIPTION_KEY_CLAIM, user.SubscriptionOrganizationName)
            }.ToReadOnlyCollection();

            ret = await user_manager.AddClaimsAsync(user, claims);
            if (ret == IdentityResult.Failed())
                return ret;

            await sign_in_manager.UserManager.UpdateSecurityStampAsync(user);


            return IdentityResult.Success;
        }
    }
}
