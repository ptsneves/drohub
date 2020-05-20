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

        public class GalleryPageModel {
            public class MediaInfo {
                public enum MediaTypeEnum {
                    LIVE_VIDEO,
                    VIDEO,
                    PICTURE
                }

                public FileInfo VideoPath { get; internal set; }
                public MediaTypeEnum MediaType { get; internal set; }
                public long CaptureDateTime { get; internal set; }
            }
            public class FileInfoModel {
                public Device device { get; internal set; }
                public MediaInfo media_info { get; internal set; }
            }

            public Dictionary<long, Dictionary<string, List<FileInfoModel>>> FilesPerTimestamp { get; internal set; }
        }

        public async Task<IActionResult> Gallery() {
            var di = new DirectoryInfo(_janus_options.RecordingPath);
            var devices = await _device_api.getSubscribedDevices();
            var files_per_timestamp = new Dictionary<long, Dictionary<string, List<GalleryPageModel.FileInfoModel>>>();
            foreach (var device in devices) {
                var video_pattern = $"drone-{device.SerialNumber}-*.webm";
                var video_paths = di.GetFiles(video_pattern)
                    .OrderByDescending(f => f.Name)
                    .ToArray();

                foreach (var video_file in video_paths) {
                    var video_timestamp = Convert.ToInt64(video_file.Name.Split('-')[2]);

                    //This is the milliseconds of the UTC midnight of the day of the media
                    var video_timestamp_datetime = (long)(DateTimeOffset.FromUnixTimeMilliseconds(video_timestamp).Date.Date.ToUniversalTime() -
                            new DateTime(1970,1,1,0,0,0,0,System.DateTimeKind.Utc))
                        .TotalMilliseconds;

                    var file_info_model = new GalleryPageModel.FileInfoModel() {
                        media_info = new GalleryPageModel.MediaInfo() {
                            VideoPath = video_file,
                            MediaType = GalleryPageModel.MediaInfo.MediaTypeEnum.LIVE_VIDEO,
                            CaptureDateTime = video_timestamp
                        },
                        device = device
                    };

                    if (!files_per_timestamp.ContainsKey(video_timestamp_datetime)) {
                        files_per_timestamp[video_timestamp_datetime] =
                            new Dictionary<string, List<GalleryPageModel.FileInfoModel>>();
                    }

                    if (!files_per_timestamp[video_timestamp_datetime].ContainsKey(device.Name)) {
                        files_per_timestamp[video_timestamp_datetime][device.Name] = new List<GalleryPageModel.FileInfoModel>();
                    }

                    files_per_timestamp[video_timestamp_datetime][device.Name].Add(file_info_model);

                }
            }
            return View(new GalleryPageModel(){FilesPerTimestamp = files_per_timestamp});
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