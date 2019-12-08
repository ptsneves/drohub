using DroHub.Areas.Identity.Data;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using DroHub.Areas.DHub.Models;
public sealed partial class DroneReply : IDroneTelemetry
{
    public int Id { get; set; }
    [JsonIgnore]
    public Device Device { get; set; }
    public string ActionName { get; set; }
}