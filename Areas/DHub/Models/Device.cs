using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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

        public string ISO { get; set; }

        public string Apperture { get; set; }

        [Display(Name = "Focus Mode")]
        public string FocusMode { get; set; }

        [Display(Name = "Registration Date")]
        public DateTime CreationDate { get; set; }

        public string LiveVideoSecret { get; set; }
        public IList<UserDevice> UserDevices { get; set; }

        public ICollection<DronePosition> positions { get; set; }
        public ICollection<DroneBatteryLevel> battery_levels { get; set; }
        public ICollection<DroneRadioSignal> radio_signals { get; set; }
        public ICollection<DroneFlyingState> flying_states { get; set; }
        public ICollection<DroneReply> drone_replies { get; set; }
        public ICollection<DroneLiveVideoStateResult> drone_video_states { get; set; }

        public const string CLAIM_VALID_VALUE = "Yes";
        public const string CAN_MODIFY_CLAIM = "CanModifyDevice";
        public const string CAN_ADD_CLAIM = "CanAddDevice";
        public const string CAN_PERFORM_FLIGHT_ACTIONS = "CanPerformFlightActions";
        public const string CAN_PERFORM_CAMERA_ACTION = "CanPerformCameraActions";
    }
}