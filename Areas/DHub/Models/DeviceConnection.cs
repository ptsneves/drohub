using System;
using System.Collections.Generic;

namespace DroHub.Areas.DHub.Models {
    public class DeviceConnection {
        public long Id { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }

        public int DeviceId { get; set; }
        public Device Device { get; set; }


        public string SubscriptionOrganizationName { get; set; }
        public Subscription Subscription { get; set; }

        public ICollection<MediaObject> MediaObjects { get; set; }

        public ICollection<DronePosition> positions { get; set; }
        public ICollection<DroneBatteryLevel> battery_levels { get; set; }
        public ICollection<CameraState> camera_states { get; set; }
        public ICollection<GimbalState> gimbal_states { get; set; }
        public ICollection<DroneRadioSignal> radio_signals { get; set; }
        public ICollection<DroneFlyingState> flying_states { get; set; }
        public ICollection<DroneReply> drone_replies { get; set; }
        public ICollection<DroneLiveVideoStateResult> drone_video_states { get; set; }

        public bool isTimePointInConnection(DateTimeOffset time) {
            // Case where the connection is still ongoing
            if (EndTime < StartTime) {
                return time >= StartTime;
            }
            return time >= StartTime
                   && time <= EndTime;
        }

        public bool contains(DeviceConnection other) {
            if (other == null) {
                return false;
            }
            var same_device_id = DeviceId == other.DeviceId;
            if (!same_device_id)
                return false;
            // Case where the connection is still ongoing
            if (EndTime < StartTime) {
                return StartTime <= other.StartTime;
            }

            return StartTime <= other.StartTime
                   && EndTime >= other.EndTime;
        }
    }
}