using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace DroHub.Areas.Identity.Data
{
    // Add profile data for application users by adding properties to the DroHubUser class
    public class DroHubUser : IdentityUser
    {
        public string DropboxToken { get; set; }
        [StringLength(32)]
        public string ConnectState { get; set; }
        [Display(Name = "Creation Date")]
        public DateTime CreationDate { get; set; }
        public DateTime LastLogin { get; set; }
        public static readonly string CLAIM_VALID_VALUE = "Yes";

        public const string ADMIN_POLICY_CLAIMS = "ActingAdmin";
        public const string SUBSCRIBER_POLICY_CLAIMS = "ActingSubscriber";
        public const string OWNER_POLICY_CLAIMS = "ActingOwner";
        public const string PILOT_POLICY_CLAIMS = "ActingPilot";
        public const string GUEST_POLICY_CLAIMS = "ActingGuest";

        public static Dictionary<string, List<Claim>> UserClaims = new Dictionary<string, List<Claim>>
        {
            {
                ADMIN_POLICY_CLAIMS, new List<Claim>()
                {
                    new Claim(ADMIN_POLICY_CLAIMS, CLAIM_VALID_VALUE),
                    new Claim(SUBSCRIBER_POLICY_CLAIMS, CLAIM_VALID_VALUE),
                    new Claim(OWNER_POLICY_CLAIMS, CLAIM_VALID_VALUE),
                    new Claim(PILOT_POLICY_CLAIMS, CLAIM_VALID_VALUE),
                    new Claim(GUEST_POLICY_CLAIMS, CLAIM_VALID_VALUE)
                }
            },

            {
                SUBSCRIBER_POLICY_CLAIMS, new List<Claim>()
                {
                    new Claim(SUBSCRIBER_POLICY_CLAIMS, CLAIM_VALID_VALUE),
                    new Claim(OWNER_POLICY_CLAIMS, CLAIM_VALID_VALUE),
                    new Claim(PILOT_POLICY_CLAIMS, CLAIM_VALID_VALUE),
                    new Claim(GUEST_POLICY_CLAIMS, CLAIM_VALID_VALUE)
                }
            },

            {
                OWNER_POLICY_CLAIMS, new List<Claim>()
                {
                    new Claim(OWNER_POLICY_CLAIMS, CLAIM_VALID_VALUE),
                    new Claim(PILOT_POLICY_CLAIMS, CLAIM_VALID_VALUE),
                    new Claim(GUEST_POLICY_CLAIMS, CLAIM_VALID_VALUE)
                }
            },

            {
                PILOT_POLICY_CLAIMS,  new List<Claim>()
                {
                    new Claim(PILOT_POLICY_CLAIMS, CLAIM_VALID_VALUE)
                }
            },

            {
                GUEST_POLICY_CLAIMS, new List<Claim>()
                {
                    new Claim(GUEST_POLICY_CLAIMS, CLAIM_VALID_VALUE)
                }
            }

        };

        public DateTime LastLogin { get; }

        public IList<UserDevice> UserDevices { get; set; }
    }
}
