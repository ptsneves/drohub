using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace DroHub.Areas.Identity.Data
{
    // Add profile data for application users by adding properties to the DroHubUser class
    public class DroHubUser : IdentityUser
    {
        public const string CLAIM_VALID_VALUE = "Yes";

        public const string ADMIN_POLICY_CLAIM = "ActingAdmin";
        public const string SUBSCRIBER_POLICY_CLAIM = "ActingSubscriber";
        public const string OWNER_POLICY_CLAIM = "ActingOwner";
        public const string PILOT_POLICY_CLAIM = "ActingPilot";
        public const string GUEST_POLICY_CLAIM = "ActingGuest";

        private static readonly List<Claim> GuestPolicyClaims = new List<Claim>() {
            new Claim(GUEST_POLICY_CLAIM, CLAIM_VALID_VALUE),
            new Claim(Device.CAN_PERFORM_CAMERA_ACTION, Device.CLAIM_VALID_VALUE),
        };

        private static readonly List<Claim> PilotPolicyClaims = new List<Claim>(GuestPolicyClaims) {
            new Claim(PILOT_POLICY_CLAIM, CLAIM_VALID_VALUE),
            new Claim(Device.CAN_ADD_CLAIM, Device.CLAIM_VALID_VALUE),
            new Claim(Device.CAN_MODIFY_CLAIM, Device.CLAIM_VALID_VALUE),
            new Claim(Device.CAN_PERFORM_FLIGHT_ACTIONS, Device.CLAIM_VALID_VALUE),
        };

        private static readonly List<Claim> OwnerPolicyClaims = new List<Claim>(PilotPolicyClaims) {
            new Claim(OWNER_POLICY_CLAIM, CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_SEE_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_EDIT_USERS_IN_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
        };

        private static readonly List<Claim> SubscriberPolicyClaims = new List<Claim>(OwnerPolicyClaims) {
            new Claim(SUBSCRIBER_POLICY_CLAIM, CLAIM_VALID_VALUE),
        };

        private static readonly List<Claim> AdminPolicyClaims = new List<Claim>(SubscriberPolicyClaims) {
            new Claim(ADMIN_POLICY_CLAIM, CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_MODIFY_CLAIM, Subscription.CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_ADD_CLAIM, Subscription.CLAIM_VALID_VALUE),
            new Claim(Subscription.CAN_SEE_NOT_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
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
        public DateTime LastLogin { get; }

        public Subscription Subscription { get; set; }
    }

    public static class DroHubUserLinqExtensions {
        public static IIncludableQueryable<DroHubUser, Subscription> getCurrentUserWithSubscription(UserManager<DroHubUser> user_manager, ClaimsPrincipal user){
            return user_manager.Users
                .Where(u => u.Id == user_manager.GetUserId(user))
                .Include(u => u.Subscription);
        }

        public static IQueryable<Subscription> getCurrentUserSubscription(this IIncludableQueryable<DroHubUser, Subscription> users) {
            return users
                .ThenInclude(s => s.Devices)
                .Select(u => u.Subscription);
        }

        public static IQueryable<Device> getSubscriptionDevices(this IQueryable<Subscription> subscriptions) {
            return subscriptions.SelectMany(s => s.Devices);
        }
    }
}
