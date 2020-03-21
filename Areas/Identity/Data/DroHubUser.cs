using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

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

        public static readonly Dictionary<string, List<Claim>> UserClaims = new Dictionary<string, List<Claim>>
        {
            {
                ADMIN_POLICY_CLAIM, new List<Claim>()
                {
                    new Claim(ADMIN_POLICY_CLAIM, CLAIM_VALID_VALUE),
                    new Claim(SUBSCRIBER_POLICY_CLAIM, CLAIM_VALID_VALUE),
                    new Claim(OWNER_POLICY_CLAIM, CLAIM_VALID_VALUE),
                    new Claim(PILOT_POLICY_CLAIM, CLAIM_VALID_VALUE),
                    new Claim(GUEST_POLICY_CLAIM, CLAIM_VALID_VALUE),
                    new Claim(Subscription.CAN_MODIFY_CLAIM, Subscription.CLAIM_VALID_VALUE),
                    new Claim(Subscription.CAN_ADD_CLAIM, Subscription.CLAIM_VALID_VALUE),
                    new Claim(Subscription.CAN_SEE_NOT_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
                    new Claim(Subscription.CAN_SEE_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
                    new Claim(Subscription.CAN_EDIT_USERS_IN_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
                }
            },

            {
                SUBSCRIBER_POLICY_CLAIM, new List<Claim>()
                {
                    new Claim(SUBSCRIBER_POLICY_CLAIM, CLAIM_VALID_VALUE),
                    new Claim(OWNER_POLICY_CLAIM, CLAIM_VALID_VALUE),
                    new Claim(PILOT_POLICY_CLAIM, CLAIM_VALID_VALUE),
                    new Claim(GUEST_POLICY_CLAIM, CLAIM_VALID_VALUE),
                    new Claim(Subscription.CAN_SEE_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE),
                    new Claim(Subscription.CAN_EDIT_USERS_IN_OWN_SUBSCRIPTION, Subscription.CLAIM_VALID_VALUE)
                }
            },

            {
                OWNER_POLICY_CLAIM, new List<Claim>()
                {
                    new Claim(OWNER_POLICY_CLAIM, CLAIM_VALID_VALUE),
                    new Claim(PILOT_POLICY_CLAIM, CLAIM_VALID_VALUE),
                    new Claim(GUEST_POLICY_CLAIM, CLAIM_VALID_VALUE)
                }
            },

            {
                PILOT_POLICY_CLAIM,  new List<Claim>()
                {
                    new Claim(PILOT_POLICY_CLAIM, CLAIM_VALID_VALUE)
                }
            },

            {
                GUEST_POLICY_CLAIM, new List<Claim>()
                {
                    new Claim(GUEST_POLICY_CLAIM, CLAIM_VALID_VALUE)
                }
            }

        };

        public DateTime LastLogin { get; }

        public IList<UserDevice> UserDevices { get; set; }
        public Subscription Subscription { get; set; }
    }
}
