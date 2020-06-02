using System;
using System.Collections.Generic;

namespace DroHub.Areas.DHub.Models {
    public class DeviceConnection {
        public long Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public int DeviceId { get; set; }
        public Device Device { get; set; }


        public string SubscriptionOrganizationName { get; set; }
        public Subscription Subscription { get; set; }

        public ICollection<DronePosition> positions { get; set; }
        public ICollection<DroneBatteryLevel> battery_levels { get; set; }
        public ICollection<DroneRadioSignal> radio_signals { get; set; }
        public ICollection<DroneFlyingState> flying_states { get; set; }
        public ICollection<DroneReply> drone_replies { get; set; }
        public ICollection<DroneLiveVideoStateResult> drone_video_states { get; set; }
    }
}