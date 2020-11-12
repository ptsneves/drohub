using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
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

        public class UploadModel {
            // [Required]
            [FromForm]
            public bool IsPreview { get; set; }

            [FromForm]
            // [Required]
            public string DeviceSerialNumber { get; set; }

            [FromForm]
            public IFormFile File { get; set; }

            [FromForm]
            public long UnixCreationTimeMS { get; set; }
        }

        private readonly DeviceAPI _device_api;
        private readonly SubscriptionAPI _subscription_api;
        private readonly DeviceConnectionAPI _connection_api;
        private readonly MediaObjectAndTagAPI _media_object_and_tag_api;
        private readonly IAuthorizationService _authorization_service;
        private readonly double _rpc_api_version;
        private readonly ILogger<AndroidApplicationController> _logger;

        public const string APPSETTINGS_API_VERSION_KEY = "RPCAPIVersion";
        public const string WRONG_API_DESCRIPTION = "Application needs update";

        public AndroidApplicationController(DeviceAPI device_api, IAuthorizationService authorizationService,
            SubscriptionAPI subscriptionApi, IConfiguration configuration,
            DeviceConnectionAPI connection_api, MediaObjectAndTagAPI media_object_and_tag_api, ILogger<AndroidApplicationController> logger) {

            _device_api = device_api;
            _authorization_service = authorizationService;
            _subscription_api = subscriptionApi;
            _connection_api = connection_api;
            _media_object_and_tag_api = media_object_and_tag_api;
            _logger = logger;
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
            if (!ModelState.IsValid)
                return BadRequest();

            if (!MediaObjectAndTagAPI.isAcceptableExtension(input.File.FileName))
                return new JsonResult(new Dictionary<string, string> {
                    ["error"] = "Format not allowed"
                });

            var device = await _device_api.getDeviceBySerialOrDefault(new DeviceAPI.DeviceSerial(
                input.DeviceSerialNumber));
            if (device == null)
                return new JsonResult(new Dictionary<string, string> {
                    ["error"] = "Device not found"
                });

            if (!await _device_api.authorizeDeviceActions(device, ResourceOperations.Update))
                return new UnauthorizedResult();

            var creation_time = DateTimeOffset.FromUnixTimeMilliseconds(input.UnixCreationTimeMS).UtcDateTime;
            var file_name_on_host =
                $"{(input.IsPreview ? MediaObjectAndTagAPI.PreviewFileNamePrefix : string.Empty)}drone-{input.DeviceSerialNumber}-{input.UnixCreationTimeMS}{Path.GetExtension(input.File.FileName)}";

            try {
                var connection = await _connection_api.getDeviceConnectionByTime(device, creation_time);


                if (!Directory.Exists(MediaObjectAndTagAPI.getConnectionDirectory(connection.Id))) {
                    Directory.CreateDirectory(MediaObjectAndTagAPI.getConnectionDirectory(connection.Id));
                }
                var path_on_host = Path.Join(
                    MediaObjectAndTagAPI.getConnectionDirectory(connection.Id),
                    file_name_on_host);


                if (System.IO.File.Exists(path_on_host))
                    return new JsonResult(new Dictionary<string, string> {
                        ["error"] = "File already exists"
                    });

                await using var filestream = new FileStream(path_on_host, FileMode.CreateNew);
                await input.File.CopyToAsync(filestream);
                System.IO.File.SetCreationTimeUtc(filestream.Name, creation_time);

                var mo = MediaObjectAndTagAPI.generateMediaObject(filestream.Name,
                    creation_time,
                    _subscription_api.getSubscriptionName().Value,
                    connection.Id);

                await _media_object_and_tag_api.addMediaObject(
                    mo,
                    new List<string> {"onboard"},
                    input.IsPreview);

                return new JsonResult(new Dictionary<string, string> {
                    ["result"] = "ok"
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