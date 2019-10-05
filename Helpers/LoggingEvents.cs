namespace DroHub.Helpers
{
    public class LoggingEvents
    {
        public const int Telemetry = 1000;
        public const int PositionTelemetry = 1001;
        public const int BatteryLevelTelemetry = 1002;
        public const int RadioSignalTelemetry = 1003;
        public const int FlyingStateTelemetry = 1004;
        public const int FileListTelemetry = 1004;

        public const int GrpcUserAction = 2000;
        public const int TakeOffAction = 2001;
        public const int LandAction = 2002;
    }
}