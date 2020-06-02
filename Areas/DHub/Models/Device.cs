using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DroHub.Areas.DHub.Models
{
    public class Device {
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

        public string SubscriptionOrganizationName { get; set; }
        public Subscription Subscription { get; set; }

        public ICollection<DeviceConnection> DeviceConnections { get; set; }
    }
}