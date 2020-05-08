using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Models;
using DroHub.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DroHub.Areas.DHub.Controllers
{
    [Area("DHub")]
    public class DeviceRepositoryController : AuthorizedController
    {
        #region Variables

        private readonly JanusServiceOptions _janus_options;
        private readonly RepositoryOptions _repository_settings;
        private readonly DeviceAPI _device_api;
        #endregion

        #region Constructor
        public DeviceRepositoryController(IOptions<RepositoryOptions> repository_settings, DeviceAPI device_api,
            IOptionsMonitor<JanusServiceOptions> janus_options) {
            _repository_settings = repository_settings.Value;
            _device_api = device_api;
            _janus_options = janus_options.CurrentValue;
        }
        #endregion

        public async Task<IActionResult> Index()
        {
            return await Dashboard();
        }

        private async Task<List<Device>> GetDeviceListInternal() {
            var device_list = await _device_api.getSubscribedDevices();
            // TODO: Add initial values
            // foreach (var device in device_list)
            // {
            //     var battery_level = await _context.DroneBatteryLevels.OrderByDescending(l => l.Id).Where(b => b.Serial == device.SerialNumber).FirstOrDefaultAsync();
            //     if (battery_level != null)
            //         device.battery_levels.Add(battery_level);
            //
            //     var radio_signal = await _context.DroneRadioSignals.OrderByDescending(l => l.Id).Where(b => b.Serial == device.SerialNumber).FirstOrDefaultAsync();
            //     if (radio_signal != null)
            //         device.radio_signals.Add(radio_signal);
            //
            //     var flying_state = await _context.DroneFlyingStates.OrderByDescending(l => l.Id).Where(b => b.Serial == device.SerialNumber).FirstOrDefaultAsync();
            //     if (flying_state != null)
            //         device.flying_states.Add(flying_state);
            //
            //     var position = await _context.Positions.OrderByDescending(l => l.Id).Where(b => b.Serial == device.SerialNumber).FirstOrDefaultAsync();
            //     if (position != null)
            //     {
            //         // _logger.LogDebug("Have positions");
            //         device.positions.Add(position);
            //     }
            //
            // }
            return device_list;
        }

        public class GalleryPageModel
        {
            public Device device { get; set; }
            public FileInfo[] video_paths { get; set; }

        }
        // GET: DroHub/Devices/Gallery/5
        public async Task<IActionResult> Gallery([Required]int id)
        {
            var device = await _device_api.getDeviceById(id);
            if (device == null)
                throw new InvalidOperationException("Cannot get device with this ID");

            var di = new DirectoryInfo(_janus_options.RecordingPath);
            var model = new GalleryPageModel
            {
                device = device,
                video_paths = di.GetFiles($"drone-{device.SerialNumber}-*.webm").OrderByDescending(f => f.Name).ToArray()
            };
            return View(model);
        }

        public IActionResult GetLiveStreamRecordingVideo(string video_id) {
            var path = Path.Combine(_janus_options.RecordingPath, video_id.Replace("mjr", "webm"));
            var res = File(System.IO.File.OpenRead(path), "video/webm");
            res.EnableRangeProcessing = true;
            return res;
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