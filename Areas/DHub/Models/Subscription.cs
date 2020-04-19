using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DroHub.Areas.Identity.Data;

namespace DroHub.Areas.DHub.Models
{
    public class Subscription
    {
        public const string CLAIM_VALID_VALUE = "Yes";
        public const string CAN_MODIFY_CLAIM = "CanModifySubscription";
        public const string CAN_ADD_CLAIM = "CanAddSubscription";
        public const string CAN_EDIT_USERS_IN_OWN_SUBSCRIPTION = "CanEditUsersInOwnSubscription";
        public const string CAN_EDIT_USERS_OUTSIDE_OWN_SUBSCRIPTION = "CanEditUsersInOwnSubscription";
        public const string CAN_SEE_NOT_OWN_SUBSCRIPTION = "CanSeeNotOwnSubscription";
        public const string CAN_SEE_OWN_SUBSCRIPTION = "CanSeeOwnSubscription";

        [Key]
        [Required]
        public string OrganizationName { get; set; }
        [Required]
        public int AllowedUserCount { get; set; }

        [Required]
        public TimeSpan AllowedFlightTime { get; set; }

        public ICollection<DroHubUser> Users { get; set; }

        public ICollection<Device> Devices { get; set; }

    }

}