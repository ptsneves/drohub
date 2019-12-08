namespace DroHub.Areas.DHub.Models {
    public interface IDroneTelemetry {
        string Serial { get; set; }
        long Timestamp { get; set; }
    }
}