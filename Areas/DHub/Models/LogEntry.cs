using System.Linq;
using System.ComponentModel.DataAnnotations;
namespace DroHub.Areas.DHub.Models
{
    public class LogEntry  {
        [Required]
        public int Id {get; set;}

        [Required]
        public string Message {get; set;}

        [Required]
        public string MessageTemplate {get; set;}

        [Required]
        public string Level {get; set;}

        [Required]
        public System.DateTime Timestamp {get; set;}

        public string Exception {get; set;}

        [Required]
        public string Properties {get; set;}
    }
}