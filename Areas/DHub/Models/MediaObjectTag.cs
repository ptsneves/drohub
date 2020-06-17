using System;

namespace DroHub.Areas.DHub.Models {
    public class MediaObjectTag {
        public DateTime? Timestamp { get; set; }
        public string MediaPath { get; set; }
        public MediaObject MediaObject { get; set; }

        public string TagName { get; set; }
        public string SubscriptionOrganizationName { get; set; }
        public Subscription Subscription { get; set; }
    }
}