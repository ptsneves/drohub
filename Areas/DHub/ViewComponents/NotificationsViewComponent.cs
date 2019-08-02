using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using DroHub.Areas.Identity.Data;
using DroHub.Data;
using DroHub.Areas.DHub.Models;

namespace DroHub.Areas.DHub.ViewComponents
{
    public class NotificationsViewComponent : ViewComponent
    {
        private readonly DroHubContext _context;
        private readonly UserManager<DroHubUser> _userManager;

        private readonly ILogger<NotificationsViewComponent> _logger;
        public NotificationsViewComponent(DroHubContext context, ILogger<NotificationsViewComponent> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync() {
            Notification[] notifications = new Notification[4] {
                new Notification(Notification.Error, "An error", 1),
                new Notification(Notification.Warning, "A warning", 2),
                new Notification(Notification.Warning, "A warning", 2),
                new Notification(Notification.Information, "An info", 3)
            };
            return View("Notifications", notifications);
        }
    }
}
