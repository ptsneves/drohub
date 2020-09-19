using System.Collections.Generic;

namespace DroHub.Areas.DHub.Models {
    public class DashboardModel {
        public string GoogleAPIKey { get; set; }
        public string FrontEndStunServerURL { get; set; }
        public string FrontEndJanusURL { get; set; }
        public List<InitialTelemetry> InitialTelemetries { get; set; }

        public class InitialTelemetry {
            public string DeviceName { get; set; }
            public string DeviceSerial { get; set; }
            public int DeviceId { get; set; }
            public DronePosition Position { get; set; }
            public DroneBatteryLevel BatteryLevel { get; set; }
            public CameraState CameraState { get; set; }
            public GimbalState GimbalState { get; set; }
            public DroneRadioSignal RadioSignal { get; set; }
            public DroneFlyingState FlyingState { get; set; }
        }
    }
}