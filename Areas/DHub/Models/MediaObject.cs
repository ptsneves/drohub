using System;
using System.Collections.Generic;

namespace DroHub.Areas.DHub.Models {
    public class MediaObject {

            public string SubscriptionOrganizationName { get; set; }
            public Subscription Subscription { get; set; }

            public long DeviceConnectionId { get; set; }
            public DeviceConnection DeviceConnection { get; set; }

            public string MediaPath { get; set; }
            public DateTime CaptureDateTimeUTC { get; internal set; }
            public ICollection<MediaObjectTag> MediaObjectTags { get; set; }
    }
}