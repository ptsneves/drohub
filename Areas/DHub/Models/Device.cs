using DroHub.Areas.Identity.Data;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Newtonsoft.Json;


namespace DroHub.Areas.DHub.Models
{
    public class Device
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Display(Name = "Serial Number")]
        [Required]
        public string SerialNumber { get; set; }
        public string DropboxToken { get; set; }
        [StringLength(32)]
        public string DropboxConnectState { get; set; }
        public string ISO { get; set; }
        public string Apperture { get; set; }
        [Display(Name = "Focus Mode")]
        public string FocusMode { get; set; }
        [Display(Name = "Registration Date")]
        public DateTime CreationDate { get; set; }
        public string LiveVideoSecret { get; set; }
        public string LiveVideoRTPUrl { get; set; }
        public int LiveVideoPt { get; set; }
        public string LiveVideoRTPMap { get; set; }
        public string LiveVideoFMTProfile { get; set; }

        public IList<UserDevice> UserDevices { get; set; }

        public ICollection<DronePosition> positions { get; set; }
        public ICollection<DroneBatteryLevel> battery_levels { get; set; }
        public ICollection<DroneRadioSignal> radio_signals { get; set; }
        public ICollection<DroneFlyingState> flying_states { get; set; }
        public ICollection<DroneReply> drone_replies { get; set; }
        public ICollection<DroneVideoStateResult> drone_video_states { get; set; }

    }
}