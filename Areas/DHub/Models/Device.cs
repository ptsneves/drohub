using DroHub.Areas.Identity.Data;
using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace DroHub.Areas.DHub.Models
{
    public class Device
    {
        public int Id { get; set; }
        public string Name { get; set; }
        [Display(Name = "Serial Number")]
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
        [JsonIgnore]
        public DroHubUser User { get; set; }
    }
}
