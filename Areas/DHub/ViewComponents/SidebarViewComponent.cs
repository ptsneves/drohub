using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using DroHub.Areas.Identity.Data;
using DroHub.Data;

namespace DroHub.Areas.DHub.ViewComponents
{
    public class SidebarViewComponent : ViewComponent
    {
        private readonly DroHubContext _context;
        private readonly UserManager<DroHubUser> _userManager;

        public SidebarViewComponent(DroHubContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync() {
            return View("Sidebar");
        }
    }
}
