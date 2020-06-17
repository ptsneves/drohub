using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
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
        private readonly DeviceConnectionAPI _device_connection_api;
        private readonly MediaObjectAndTagAPI _media_objectAnd_tag_api;
        #endregion

        #region Constructor
        public DeviceRepositoryController(IOptions<RepositoryOptions> repository_settings, DeviceAPI device_api,
            DeviceConnectionAPI device_connection_api, MediaObjectAndTagAPI media_object_and_tag_api) {
            _repository_settings = repository_settings.Value;
            _device_api = device_api;
            _device_connection_api = device_connection_api;
            _media_objectAnd_tag_api = media_object_and_tag_api;
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
                public string VideoPath { get; internal set; }
                public long CaptureDateTime { get; internal set; }
                public IEnumerable<string> Tags { get; internal set; }
            }
            public class FileInfoModel {
                public Device device { get; internal set; }
                public MediaInfo media_object { get; internal set; }
            }

            public Dictionary<long, Dictionary<string, List<FileInfoModel>>> FilesPerTimestamp { get; internal set; }
        }

        public async Task<IActionResult> Gallery() {
            var sessions = await _device_connection_api.getSubscribedDeviceConnections();

            var files_per_timestamp = new Dictionary<long, Dictionary<string, List<GalleryPageModel.FileInfoModel>>>();
            foreach (var session in sessions) {

                foreach (var media_file in session.MediaObjects) {
                    var media_start_time = (DateTimeOffset)media_file.CaptureDateTimeUTC;
                    if (!System.IO.File.Exists(media_file.MediaPath))
                        continue;

                    //This is the milliseconds of the UTC midnight of the day of the media
                    var video_timestamp_datetime = ((DateTimeOffset) media_start_time.Date).ToUnixTimeMilliseconds();

                    var file_info_model = new GalleryPageModel.FileInfoModel() {
                        media_object = new GalleryPageModel.MediaInfo() {
                            VideoPath = MediaObjectAndTagAPI.convertToFrontEndFilePath(media_file),
                            CaptureDateTime = media_start_time.ToUnixTimeMilliseconds(),
                            Tags = media_file.MediaObjectTags.Select(s => s.TagName)
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

        [HttpGet]
        public IActionResult DeleteMediaObject() {
            return RedirectToAction("Gallery");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMediaObject([Required]string media_path) {
            try {
                if (!ModelState.IsValid)
                    return BadRequest();
                media_path = MediaObjectAndTagAPI.convertToBackEndFilePath(media_path);
                await _media_objectAnd_tag_api.deleteMediaObject(media_path);
                return RedirectToAction("Gallery");
            }
            catch (MediaObjectAuthorizationException e) {
                return new ForbidResult(e.Message);
            }
            catch (MediaObjectAndTagException e) {
                return NotFound(e.Message);
            }
        }

        enum DownloadType {
            STREAM,
            DOWNLOAD,
        }

        [NonAction]
        private async Task<IActionResult> getFile(string video_id, DownloadType t) {
            try {
                if (!ModelState.IsValid)
                    return BadRequest();
                video_id = MediaObjectAndTagAPI.convertToBackEndFilePath(video_id);
                var stream = await _media_objectAnd_tag_api.getFileForStreaming(video_id);
                var res = t switch {
                    DownloadType.STREAM => File(stream, "video/webm"),
                    DownloadType.DOWNLOAD => File(stream, "application/octet-stream", Path.GetFileName(video_id)),
                    _ => throw new InvalidProgramException("Unreachable code")
                };

                res.EnableRangeProcessing = true;
                return res;
            }
            catch (MediaObjectAuthorizationException e) {
                return new ForbidResult(e.Message);
            }
            catch (MediaObjectAndTagException e) {
                return NotFound(e.Message);
            }
        }
        public async Task<IActionResult> GetLiveStreamRecordingVideo([Required]string video_id) {
            if (!ModelState.IsValid)
                return BadRequest();
            return await getFile(video_id, DownloadType.STREAM);
        }

        public async Task<IActionResult> DownloadVideo([Required]string video_id) {
            if (!ModelState.IsValid)
                return BadRequest();
            return await getFile(video_id, DownloadType.DOWNLOAD);
        }

        public class AddTagsModel {
            [Required]
            public string TagListJSON { get; set; }

            [Required]
            public string MediaId { get; set; }

            [Required]
            public bool UseTimeStamp { get; set; }

            [Range(typeof(TimeSpan), "00:00", "23:59")]
            public TimeSpan TimeStampInSeconds { get; set; }

        }

        public async Task<ActionResult> AddTags([Required] AddTagsModel tags) {
            if (!ModelState.IsValid)
                return BadRequest();

            if (tags.UseTimeStamp) {

            }
            var tag_list = JsonSerializer.Deserialize<IEnumerable<string>>(tags.TagListJSON);
            try {
                var media_id = MediaObjectAndTagAPI.convertToBackEndFilePath(tags.MediaId);

                await _media_objectAnd_tag_api.addTags(media_id, tag_list, null);
                return RedirectToAction("Gallery");
            }
            catch (MediaObjectAuthorizationException e) {
                return Forbid(e.Message);
            }
            catch (JsonException) {
                return BadRequest();
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTag([Required] string tag_name, string media_id) {
            if (!ModelState.IsValid)
                return BadRequest();
            try {
                media_id = MediaObjectAndTagAPI.convertToBackEndFilePath(media_id);
                await _media_objectAnd_tag_api.removeTag(tag_name, media_id, true);
                return Ok();
            }
            catch (MediaObjectAuthorizationException e) {
                return Forbid(e.Message);
            }
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