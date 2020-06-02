using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DroHub.Areas.DHub.Controllers
{
    [Area("DHub")]
    public class DeviceRepositoryController : AuthorizedController
    {
        #region Variables

        private readonly RepositoryOptions _repository_settings;
        private readonly DeviceAPI _device_api;
        private readonly SubscriptionAPI _subscription_api;
        private readonly DeviceConnectionAPI _device_connection_api;
        #endregion

        #region Constructor
        public DeviceRepositoryController(IOptions<RepositoryOptions> repository_settings, DeviceAPI device_api,
            SubscriptionAPI subscription_api, DeviceConnectionAPI device_connection_api) {
            _repository_settings = repository_settings.Value;
            _device_api = device_api;
            _subscription_api = subscription_api;
            _device_connection_api = device_connection_api;
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

            var sessions = await _subscription_api.getSubscribedDeviceConnections(true);

            var files_per_timestamp = new Dictionary<long, Dictionary<string, List<GalleryPageModel.FileInfoModel>>>();
            foreach (var session in sessions) {
                var di = new DirectoryInfo(_device_connection_api.getConnectionMediaDir(session.Id));
                var video_pattern = $"*.webm";
                var video_paths = di.GetFiles(video_pattern)
                    .OrderByDescending(f => f.Name)
                    .ToArray();

                var audio_pattern = $"*.opus";
                var audio_paths = di.GetFiles(audio_pattern)
                    .OrderByDescending(f => f.Name)
                    .ToArray();

                var media_paths = audio_paths.Concat(video_paths);

                foreach (var media_file in media_paths) {
                    var media_start_time = (DateTimeOffset)media_file.CreationTimeUtc;

                    //This is the milliseconds of the UTC midnight of the day of the media
                    var video_timestamp_datetime = ((DateTimeOffset) media_start_time.Date).ToUnixTimeMilliseconds();

                    var file_info_model = new GalleryPageModel.FileInfoModel() {
                        media_info = new GalleryPageModel.MediaInfo() {
                            VideoPath = media_file,
                            MediaType = GalleryPageModel.MediaInfo.MediaTypeEnum.LIVE_VIDEO,
                            CaptureDateTime = media_start_time.ToUnixTimeMilliseconds()
                        },
                        device = session.Device
                    };

                    if (!files_per_timestamp.ContainsKey(video_timestamp_datetime)) {
                        files_per_timestamp[video_timestamp_datetime] =
                            new Dictionary<string, List<GalleryPageModel.FileInfoModel>>();
                    }

                    if (!files_per_timestamp[video_timestamp_datetime].ContainsKey(session.Device.Name)) {
                        files_per_timestamp[video_timestamp_datetime][session.Device.Name] = new List<GalleryPageModel.FileInfoModel>();
                    }
                    files_per_timestamp[video_timestamp_datetime][session.Device.Name].Add(file_info_model);
                }
            }
            return View(new GalleryPageModel(){FilesPerTimestamp = files_per_timestamp});
        }

        public IActionResult GetLiveStreamRecordingVideo(string video_id) {
            var path = Path.Combine(DeviceConnectionAPI.MediaDir, video_id.Replace("mjr", "webm"));
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