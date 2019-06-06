using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DroHub.Areas.Identity.Data
{
    // Add profile data for application users by adding properties to the DroHubUser class
    public class DroHubUser : IdentityUser
    {
        public string DropboxToken { get; set; }
        [StringLength(32)]
        public string ConnectState { get; set; }
        [Display(Name = "Creation Date")]
        public DateTime CreationDate { get; set; }
        public DateTime LastLogin { get; set; }

        public ICollection<Device> Devices { get; set; }
    }
}
