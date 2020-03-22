using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using DroHub.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DroHub.Areas.DHub.Controllers
{
    [Area("DHub")]
    public class DeviceRepositoryController : AuthorizedController
    {
        #region Variables
        private readonly DroHubContext _context;
        private readonly UserManager<DroHubUser> _user_manager;
        private readonly RepositoryOptions _repository_settings;
        private readonly ILogger<DeviceRepositoryController> _logger;
        #endregion

        #region Constructor
        public DeviceRepositoryController(DroHubContext context, UserManager<DroHubUser> userManager,
            IOptions<RepositoryOptions> repository_settings,
            ILogger<DeviceRepositoryController> logger)
        {
            _context = context;
            _user_manager = userManager;
            _logger = logger;
            _repository_settings = repository_settings.Value;
        }
        #endregion

        private async Task<Device> getDeviceById(int? id) {
            return await DevicesController.getDeviceById(_user_manager, User, id);
        }

        public async Task<IActionResult> Index(int? id)
        {
            return await Dashboard();
        }

        private async Task<List<Device>> GetDeviceListInternal() {
            var device_list = await DevicesController.getSubscribedDevices(_user_manager, User);

            foreach (var device in device_list)
            {
                var battery_level = await _context.DroneBatteryLevels.OrderByDescending(l => l.Id).Where(b => b.Serial == device.SerialNumber).FirstOrDefaultAsync();
                if (battery_level != null)
                    device.battery_levels.Add(battery_level);

                var radio_signal = await _context.DroneRadioSignals.OrderByDescending(l => l.Id).Where(b => b.Serial == device.SerialNumber).FirstOrDefaultAsync();
                if (radio_signal != null)
                    device.radio_signals.Add(radio_signal);

                var flying_state = await _context.DroneFlyingStates.OrderByDescending(l => l.Id).Where(b => b.Serial == device.SerialNumber).FirstOrDefaultAsync();
                if (flying_state != null)
                    device.flying_states.Add(flying_state);

                var position = await _context.Positions.OrderByDescending(l => l.Id).Where(b => b.Serial == device.SerialNumber).FirstOrDefaultAsync();
                if (position != null)
                {
                    // _logger.LogDebug("Have positions");
                    device.positions.Add(position);
                }

            }
            return device_list;
        }
        public async Task<IActionResult> Dashboard() {
            var google_api_key = _repository_settings.GoogleMapsAPIKey;
            if (!String.IsNullOrEmpty(google_api_key))
            {
                ViewData["GoogleAPIKey"] = $"key={google_api_key}";
            }
            else {
                ViewData["GoogleAPIKey"] = "";
            }
            ViewData["FrontEndStunServerUrl"] = _repository_settings.FrontEndStunServerUrl;
            ViewData["FrontEndJanusUrl"] = _repository_settings.FrontEndJanusUrl;

            return View(await GetDeviceListInternal());
        }
    }
}