using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DroHub.Areas.DHub.Models
{
    public class RepositoryGalleryItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Thumbnail { get; set; }
        public string Size { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime DateTaken { get; set; }
    }
}
