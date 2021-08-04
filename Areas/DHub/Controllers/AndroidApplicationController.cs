using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DroHub.Areas.DHub.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class AndroidApplicationController : ControllerBase
    {
        public const string CHUNK_TOO_SMALL = "Chunk too small.";
        public const string SIZE_TOO_SMALL = "File length or assembled file size lt 0.";
        public const string BAD_PREVIEW_FORMAT = "Preview extension not allowed.";
        public const string FORBIDDEN_PREVIEW = "Cannot generate previews for this file.";

        public class QueryDeviceModel {
            [Required]
            public string DeviceSerialNumber { get; set; }
        }

        public class DeviceCreateModel {
            [Required]
            public Device Device { get; set; }
        }

        public class ValidateTokenModel {
            [Required]
            public double Version { get; set; }
        }

        [UploadModelValidator]
        public class UploadModel {
            [FromForm]
            public bool IsPreview { get; set; }

            [Required]
            public string DeviceSerialNumber { get; set; }

            public IFormFile File { get; set; }

            [FromForm]
            public long UnixCreationTimeMS { get; set; }

            [FromForm]
            public long RangeStartBytes { get; set; }

            [FromForm]
            public long AssembledFileSize { get; set; }
        }

        private class UploadModelValidator : ValidationAttribute {
            public override bool IsValid(object value) {
                if (!(value is UploadModel model))
                    return false;

                if (model.File.Length <= 0 || model.AssembledFileSize <= 0) {
                    ErrorMessage = SIZE_TOO_SMALL;
                    return false;
                }

                if (model.File.Length > model.AssembledFileSize) {
                    ErrorMessage = "Chunk or file bigger than the reported assembled file size";
                    return false;
                }

                if (model.RangeStartBytes > model.AssembledFileSize) {
                    ErrorMessage = "Range is bigger than Assembled file size";
                    return false;
                }

                if (model.File.Length + model.RangeStartBytes > model.AssembledFileSize) {
                    ErrorMessage = "Chunk would overflow assembled file size";
                    return false;
                }

                if (model.UnixCreationTimeMS <= 0) {
                    ErrorMessage = "CreationTime is invalid";
                    return false;
                }

                if (string.IsNullOrEmpty(model.DeviceSerialNumber)) {
                    ErrorMessage = "No device serial number found";
                    return false;
                }

                if (!MediaObjectAndTagAPI.isAllowedExtension(model.File.FileName)) {
                    ErrorMessage = "Format not allowed";
                    return false;
                }

                if (model.IsPreview) {
                    if (!MediaObjectAndTagAPI.isAllowedPreviewExtension(model.File.FileName)) {
                        ErrorMessage = BAD_PREVIEW_FORMAT;
                        return false;
                    }
                }

                if (model.File.Length < MINIMUM_CHUNK_SIZE_IN_BYTES && model.AssembledFileSize > MINIMUM_CHUNK_SIZE_IN_BYTES) {
                    ErrorMessage = CHUNK_TOO_SMALL;
                    return false;
                }

                return true;

            }
        }

        private readonly DeviceAPI _device_api;
        private readonly DeviceConnectionAPI _device_connection_api;
        private readonly SubscriptionAPI _subscription_api;
        private readonly DeviceConnectionAPI _connection_api;
        private readonly MediaObjectAndTagAPI _media_object_and_tag_api;
        private readonly IAuthorizationService _authorization_service;
        private readonly double _rpc_api_version;
        private readonly ILogger<AndroidApplicationController> _logger;

        public const string APPSETTINGS_API_VERSION_KEY = "RPCAPIVersion";
        public const string WRONG_API_DESCRIPTION = "Application needs update";
        public const long MINIMUM_CHUNK_SIZE_IN_BYTES = 4096;

        public AndroidApplicationController(DeviceAPI device_api, IAuthorizationService authorizationService,
            SubscriptionAPI subscriptionApi, IConfiguration configuration,
            DeviceConnectionAPI connection_api,
            MediaObjectAndTagAPI media_object_and_tag_api,
            DeviceConnectionAPI device_connection_api,
            ILogger<AndroidApplicationController> logger) {

            _device_api = device_api;
            _authorization_service = authorizationService;
            _subscription_api = subscriptionApi;
            _connection_api = connection_api;
            _media_object_and_tag_api = media_object_and_tag_api;
            _logger = logger;
            _device_connection_api = device_connection_api;
            _rpc_api_version = configuration.GetValue<double>(APPSETTINGS_API_VERSION_KEY);
        }

        [HttpPost]
        public async Task<IActionResult> CreateDevice([FromBody] DeviceCreateModel device_model) {

            try {
                if (device_model.Device.SerialNumber == "NODEVICE")
                    throw new InvalidDataException("NODEVICE is a reserved device and cannot be created");
                await _device_api.Create(device_model.Device);
            }
            catch (InvalidDataException e) {
                return new JsonResult(new Dictionary<string, string>() {{"result", e.Message}});
            }
            catch (DeviceAuthorizationException e) {
                return new JsonResult(new Dictionary<string, string>() {{"result", e.Message}});
            }
            return new JsonResult(new Dictionary<string, string>() {{"result", "ok"}});
        }

        public IActionResult ValidateToken([FromBody] ValidateTokenModel model) {
            if (!ModelState.IsValid || model.Version < _rpc_api_version)
                return new JsonResult(new Dictionary<string, string> {
                    ["error"] = WRONG_API_DESCRIPTION,
                });
            //If we got here it means the authentication middleware allowed
            return new JsonResult(new Dictionary<string, string>() {{"result", "ok"}});
        }

        public async Task<IActionResult> GetSubscriptionMediaInfo() {
            return new JsonResult(new Dictionary<string, Dictionary<string, Dictionary<string, GalleryModel.Session>>> {
                ["result"] = (await MediaObjectAndTagAPI.getGalleryModel(_device_connection_api)).FilesPerTimestamp,
            }, new JsonSerializerOptions { PropertyNamingPolicy = null });
        }

        public async Task<IActionResult> GetPreview(
            [Required]
            [MediaObjectAndTagAPI.MediaExtensionValidator(MediaType = MediaObjectAndTagAPI.MediaType.ANY)]
            string media_id) {

            try {
                return await _media_object_and_tag_api.getFileForDownload(media_id,
                    MediaObjectAndTagAPI.DownloadType.PREVIEW, this);
            }
            catch (Exception) {
                return NotFound(new Dictionary<string, string> {
                    ["error"] = "Request is invalid or not for a picture"
                });
            }
        }

        public async Task<IActionResult> DownloadFile([Required]
            [MediaObjectAndTagAPI.MediaExtensionValidator(MediaType = MediaObjectAndTagAPI.MediaType.ANY)]
            string media_id) {

            try {
                return await _media_object_and_tag_api.getFileForDownload(
                        media_id, MediaObjectAndTagAPI.DownloadType.DOWNLOAD, this);
            }
            catch (Exception) {
                return NotFound(new Dictionary<string, string> {
                    ["error"] = "Request is invalid or not for a picture"
                });
            }
        }

        public async Task<IActionResult> GetSubscriptionInfo() {
            var subscription = await _subscription_api.getSubscription();
            var model = new AccountManagementModel {
                user_name = User.Identity.Name,
                subscription_name = subscription.OrganizationName,
                allowed_flight_time = await _subscription_api.getSubscriptionTimeLeft(),
                allowed_users = await _subscription_api.getUserCount()
            };
            return new JsonResult(new Dictionary<string, AccountManagementModel> {
                ["result"] = model
            });
        }

        [HttpPost]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadMedia([FromForm]UploadModel input) {
            var device = await _device_api.getDeviceBySerialOrDefault(new DeviceAPI.DeviceSerial(
                input.DeviceSerialNumber));
            if (device == null)
                return new JsonResult(new Dictionary<string, string> {
                    ["error"] = "Device not found"
                });

            if (!await _device_api.authorizeDeviceActions(device, ResourceOperations.Update))
                return new UnauthorizedResult();

            var creation_time = DateTimeOffset.FromUnixTimeMilliseconds(input.UnixCreationTimeMS);

            try {
                var connection = await _connection_api.getDeviceConnectionByTime(device, creation_time);
                var local_storage_helper = new MediaObjectAndTagAPI.LocalStorageHelper(
                    connection.Id,
                    input.RangeStartBytes,
                    input.IsPreview,
                    input.DeviceSerialNumber,
                    input.UnixCreationTimeMS,
                    Path.GetExtension(input.File.FileName));

                local_storage_helper.createDirectory();

                if (input.IsPreview && local_storage_helper.isFrontEndFileNamePreviewOfExistingFile()) {
                    return new JsonResult(new Dictionary<string, string> {
                        ["error"] = FORBIDDEN_PREVIEW
                    });
                }

                if (local_storage_helper.doesAssembledFileExist())
                    return new JsonResult(new Dictionary<string, string> {
                        ["error"] = "File already exists"
                    });

                if (local_storage_helper.shouldSendNext(input.RangeStartBytes + input.File.Length)) {
                    return new JsonResult(new Dictionary<string, dynamic> {
                        ["result"] = "send-next",
                        ["begin"] = local_storage_helper.calculateNextChunkOffset()
                    });
                }

                await using var chunked_file_stream = new FileStream(local_storage_helper.calculateChunkedFilePath(),
                    FileMode.CreateNew);

                await input.File.CopyToAsync(chunked_file_stream);

                if (input.RangeStartBytes + input.File.Length == input.AssembledFileSize) {
                    var assembled_file_name = await local_storage_helper.generateAssembledFile();
                    System.IO.File.SetCreationTimeUtc(assembled_file_name, creation_time.UtcDateTime);
                    var mo = MediaObjectAndTagAPI.generateMediaObject(
                        assembled_file_name,
                        creation_time,
                        _subscription_api.getSubscriptionName().Value,
                        connection.Id);

                    await _media_object_and_tag_api.addMediaObject(mo,
                        new List<string> {"onboard"},
                        input.IsPreview);

                    return new JsonResult(new Dictionary<string, string> {
                        ["result"] = "ok"
                    });
                }
                else if (input.RangeStartBytes + input.File.Length > input.AssembledFileSize) {
                    return new JsonResult(new Dictionary<string, string> {
                        ["error"] = "Chunk is bigger than reported file size"
                    });
                }

                return new JsonResult(new Dictionary<string, dynamic> {
                    ["result"] = "send-next",
                    ["begin"] = local_storage_helper.calculateNextChunkOffset()
                });
            }
            catch (DeviceConnectionException e) {
                _logger.LogError(e.Message);
                return new JsonResult(new Dictionary<string, string> {
                    ["error"] = "Media does not correspond to any known flight"
                });
            }
            catch (MediaObjectAndTagException e) {
                _logger.LogError(e.Message);
                return new JsonResult(new Dictionary<string, string> {
                    ["error"] = "Failed to add"
                });
            }
        }


        [HttpPost]
        public async Task<IActionResult> QueryDeviceInfo([FromBody] QueryDeviceModel device_query) {
            var result = await _device_api.getDeviceBySerialOrDefault(
                new DeviceAPI.DeviceSerial(device_query.DeviceSerialNumber));

            if (result == null) {
                return new JsonResult(new Dictionary<string, string> {
                    ["error"] = "Device does not exist."
                });
            }

            if (!(await _authorization_service.AuthorizeAsync(User, result, DeviceAuthorizationHandler.DeviceResourceOperations.CanPerformFlightActions)).Succeeded) {
                return new UnauthorizedResult();
            }

            return new JsonResult(new Dictionary<string, Device> {
                ["result"] = result
            });
        }
    }
}