namespace DroHub.Areas.DHub.Models {
    public interface IDroneTelemetry {
        string Serial { get; set; }
        uint Timestamp { get; set; }
    }
}