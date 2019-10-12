using Microsoft.AspNetCore.Mvc;
using DroHub.Areas.DHub.Models;
using System.Threading.Tasks;

namespace DroHub.Areas.DHub.ViewComponents {
    public class VideoViewComponent : ViewComponent {
        public async Task<IViewComponentResult> InvokeAsync(Device device) {
            return View("Video", device);
        }
    }
}