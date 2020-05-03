using System;

namespace DroHub.Areas.DHub.Models {
    public class DeviceConnectionData : IDroneTelemetry {
        public DateTime ConnectionStart { get; set; }
        public string Serial { get; set; }
        public long Timestamp { get; set; }
    }
}