using System.Linq;
using System.ComponentModel.DataAnnotations;
using DroHub.Helpers;
using Newtonsoft.Json;

namespace DroHub.Areas.DHub.Models
{
    public class LogEntry
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public string Message { get; set; }

        [Required]
        public string Level { get; set; }

        [Required]
        public System.DateTime Timestamp { get; set; }
        public string EventId { get; set; }

        public string Exception { get; set; }

        [Required]
        [JsonIgnore]
        [StringLength(100)]
        public string SourceContext { get; set; }
    }
}