using DroHub.Areas.Identity.Data;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using DroHub.Areas.DHub.Models;
public sealed partial class DroneFileList : IDroneTelemetry {
        public int Id { get; set; }
        [JsonIgnore]
        public Device Device { get; set; }
}