using System;
using System.Collections.Generic;
using DroHub.Areas.DHub.API;

namespace DroHub.Areas.DHub.Models {
    public struct AccountManagementModel {
        public struct UserPermissionsModel {
            public string photo_src;
            public string user_name;
            public string user_email;
            public string user_type;
            public long last_login;
            public bool email_confirmed;
            public bool can_be_excluded;
            public bool can_have_permissions_managed;
        };

        public Dictionary<string, SubscriptionAPI.UserTypeAttributes> user_attributes;
        public string user_name { get; set; }
        public string user_email;
        public string subscription_name  { get; set; }
        public int allowed_users { get; set; }
        public TimeSpan allowed_flight_time { get; set; }
        public List<UserPermissionsModel> user_permissions;
    }
}