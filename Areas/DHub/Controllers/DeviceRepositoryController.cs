using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using static DroHub.Areas.DHub.API.MediaObjectAndTagAPI.LocalStorageHelper;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

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

        public class GalleryPageModel {
            public class MediaInfo {
                public string MediaPath { get; internal set; }
                public string PreviewMediaPath { get; internal set; }
                public long CaptureDateTime { get; internal set; }
                public IEnumerable<string> Tags { get; internal set; }
            }
            public class FileInfoModel {
                public String device_name { get; internal set; }
                public MediaInfo media_object { get; internal set; }
            }

            public Dictionary<long, Dictionary<string, List<FileInfoModel>>> FilesPerTimestamp { get; set; }
        }

        public async Task<IActionResult> Gallery() {
            var sessions = await _device_connection_api.getSubscribedDeviceConnections();

            var files_per_timestamp = new Dictionary<long, Dictionary<string, List<GalleryPageModel.FileInfoModel>>>();
            foreach (var session in sessions) {

                foreach (var media_file in session.MediaObjects) {
                    var media_start_time = (DateTimeOffset)media_file.CaptureDateTimeUTC;


                    if (!doesPreviewExist(media_file))
                        continue;

                    //This is the milliseconds of the UTC midnight of the day of the media
                    var video_timestamp_datetime = ((DateTimeOffset) media_start_time.Date).ToUnixTimeMilliseconds();

                    var file_info_model = new GalleryPageModel.FileInfoModel() {
                        media_object = new GalleryPageModel.MediaInfo {
                            MediaPath = doesFileExist(media_file) ? convertToFrontEndFilePath(media_file) : string.Empty,
                            PreviewMediaPath = convertToPreviewFrontEndFilePath(media_file),
                            CaptureDateTime = media_start_time.ToUnixTimeMilliseconds(),
                            Tags = media_file.MediaObjectTags.Select(s => s.TagName),
                        },
                        device_name = session.Device.Name
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

            ViewBag.disable_normal_margin = true;
            ViewBag.disable_bar_margin = true;
            return View(new GalleryPageModel(){FilesPerTimestamp = files_per_timestamp});
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMediaObjects([Required]IEnumerable<string> MediaIdList) {
            try {
                if (!ModelState.IsValid)
                    return BadRequest();
                foreach (var raw_media_path in MediaIdList) {
                    var media_path = convertToBackEndFilePath(raw_media_path);
                    await _media_objectAnd_tag_api.deleteMediaObject(media_path);
                }

                return RedirectToAction("Gallery");
            }
            catch (MediaObjectAuthorizationException) {
                return Unauthorized();
            }
            catch (MediaObjectAndTagException e) {
                return NotFound(e.Message);
            }
        }

        private enum DownloadType {
            VIDEO_STREAM,
            DOWNLOAD,
            JPEG,
        }

        [NonAction]
        private static byte[] generateZipArchive(IEnumerable<string> files)
        {
            using var archiveStream = new MemoryStream();
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, false)) {
                foreach (var file in files) {
                    var entry = archive.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.NoCompression);
                    entry.ExternalAttributes |= (Convert.ToInt32("664", 8) << 16);
                }
            }

            return archiveStream.ToArray();
        }

        [NonAction]
        private async Task<IActionResult> getFiles(string[] media_ids) {
            try {
                if (!ModelState.IsValid || !media_ids.Any())
                    return BadRequest();

                var file_list = new List<string>();
                foreach (var media_id in media_ids) {
                    var converted_media_id = convertToBackEndFilePath(media_id);

                    if (!await _media_objectAnd_tag_api.authorizeMediaObjectOperation(converted_media_id,
                            ResourceOperations.Read))
                        return Unauthorized();

                    file_list.Add(converted_media_id);
                }

                var res = File(generateZipArchive(file_list),
                    "application/zip", "drohub-media.zip");


                res.EnableRangeProcessing = true;
                return res;
            }
            catch (MediaObjectAuthorizationException) {
                return Unauthorized();
            }
            catch (MediaObjectAndTagException e) {
                return NotFound(e.Message);
            }
        }

        [NonAction]
        private async Task<IActionResult> getFile(string video_id, DownloadType t) {
            try {
                if (!ModelState.IsValid)
                    return BadRequest();
                video_id = convertToBackEndFilePath(video_id);
                var stream = await _media_objectAnd_tag_api.getFileForStreaming(video_id);
                var res = t switch {
                    DownloadType.VIDEO_STREAM => File(stream, "video/webm"),
                    DownloadType.JPEG => File(stream, "image/jpeg"),
                    DownloadType.DOWNLOAD => File(stream, "application/octet-stream", Path.GetFileName(video_id)),
                    _ => throw new InvalidProgramException("Unreachable code")
                };

                res.EnableRangeProcessing = true;
                return res;
            }
            catch (MediaObjectAuthorizationException e) {
                return Unauthorized();
            }
            catch (MediaObjectAndTagException e) {
                return NotFound(e.Message);
            }
        }

        public async Task<IActionResult> GetLiveStreamRecordingVideo([Required]string video_id) {
            if (!ModelState.IsValid && !(video_id.EndsWith(".webm") || video_id.EndsWith(".mp4")))
                return BadRequest();
            return await getFile(video_id, DownloadType.VIDEO_STREAM);
        }

        public async Task<IActionResult> DownloadVideo([Required]string video_id) {
            if (!ModelState.IsValid && !(video_id.EndsWith(".webm") || video_id.EndsWith(".mp4")))
                return BadRequest();
            return await getFile(video_id, DownloadType.DOWNLOAD);
        }

        public async Task<IActionResult> GetPhoto([Required]string picture_id) {
            if (!ModelState.IsValid || !picture_id.EndsWith(".jpeg"))
                return BadRequest();
            return await getFile(picture_id, DownloadType.JPEG);
        }

        public async Task<IActionResult> DownloadMedias([Required][FromQuery(Name="MediaIdList")]string[] MediaIdList) {
            return await getFiles(MediaIdList.ToArray());
        }

        public class AddTagsModel {
            [Required]
            public string[] TagList { get; set; }

            [Required]
            public string[] MediaIdList { get; set; }

            [Required]
            public bool UseTimeStamp { get; set; }

            // [Range(typeof(TimeSpan), "00:00", "23:59")]
            public TimeSpan TimeStampInSeconds { get; set; }

        }

        public async Task<ActionResult> AddTags([Required] AddTagsModel tags) {
            if (!ModelState.IsValid)
                return BadRequest();

            try {

                if (tags.UseTimeStamp && tags.TagList.Length != 1) {
                    return BadRequest();
                }

                foreach (var media_id in tags.MediaIdList) {
                    var converted_media_id = convertToBackEndFilePath(media_id);
                    await _media_objectAnd_tag_api.addTags(converted_media_id, tags.TagList, null,
                        media_id == tags.MediaIdList.Last());
                }

                return RedirectToAction("Gallery");
            }
            catch (MediaObjectAuthorizationException e) {
                return Unauthorized(e.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTag([Required] string tag_name,[Required] string media_id) {
            if (!ModelState.IsValid)
                return BadRequest();
            try {
                var converted_media_id = convertToBackEndFilePath(media_id);
                await _media_objectAnd_tag_api.removeTag(tag_name, converted_media_id);
                return Ok();
            }
            catch (MediaObjectAuthorizationException e) {
                return Unauthorized(e.Message);
            }
            catch (System.InvalidOperationException) {
                return BadRequest($"{media_id} could not be retrieved");
            }
        }

        public async Task<IActionResult> Dashboard() {
            var google_api_key = _repository_settings.GoogleMapsAPIKey;
            google_api_key = !String.IsNullOrEmpty(google_api_key) ? $"key={google_api_key}" : "";


            var to_load = new List<Expression<Func<DeviceConnection, IEnumerable<IDroneTelemetry>>>>() {
                (connection => connection.battery_levels),
                (connection => connection.camera_states),
                (connection => connection.gimbal_states),
                (connection => connection.flying_states),
                (connection => connection.positions),
                (connection => connection.radio_signals),
            };
            var telemetries = await _device_api.getSubscribedDevicesLastTelemetry(to_load);
            var model = new DashboardModel() {
                GoogleAPIKey =  google_api_key,
                FrontEndStunServerURL = _repository_settings.FrontEndStunServerUrl,
                FrontEndJanusURL = _repository_settings.FrontEndJanusUrl,
                InitialTelemetries = telemetries
                    .Select(t => new DashboardModel.InitialTelemetry {
                        DeviceName = t.Name,
                        DeviceSerial = t.SerialNumber,
                        DeviceId = t.Id,
                        Position = t.DeviceConnections.First().positions?.SingleOrDefault(),
                        BatteryLevel = t.DeviceConnections.First().battery_levels?.SingleOrDefault(),
                        CameraState = t.DeviceConnections.First().camera_states?.SingleOrDefault(),
                        RadioSignal = t.DeviceConnections.First().radio_signals?.SingleOrDefault(),
                        FlyingState = t.DeviceConnections.First().flying_states?.SingleOrDefault(),
                        GimbalState = t.DeviceConnections.First().gimbal_states?.SingleOrDefault(),
                }).ToList()
            };

            return View(model);
        }
    }
}