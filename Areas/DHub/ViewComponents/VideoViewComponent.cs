using Microsoft.AspNetCore.Mvc;
using DroHub.Areas.DHub.Models;
using System.Threading.Tasks;

namespace DroHub.Areas.DHub.ViewComponents {
    public class VideoViewComponent : ViewComponent {
        public IViewComponentResult Invoke(Device device) {
            return View("Video", device);
        }
    }
}