using System.Text.Json.Serialization;
using DroHub.Areas.DHub.Models;
public sealed partial class DroneFlyingState : IDroneTelemetry {
        public int Id { get; set; }
        [JsonIgnore]
        public DeviceConnection Connection { get; set; }

        [JsonIgnore]
        public long ConnectionId { get; set; }
}