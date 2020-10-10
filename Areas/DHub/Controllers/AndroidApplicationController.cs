using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

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

        private readonly DeviceAPI _device_api;
        private readonly SubscriptionAPI _subscription_api;
        private readonly IAuthorizationService _authorization_service;
        private readonly double _rpc_api_version;

        public const string APPSETTINGS_API_VERSION_KEY = "RPCAPIVersion";
        public const string WRONG_API_DESCRIPTION = "Application needs update";

        public AndroidApplicationController(DeviceAPI device_api, IAuthorizationService authorizationService,
            SubscriptionAPI subscriptionApi, IConfiguration configuration) {

            _device_api = device_api;
            _authorization_service = authorizationService;
            _subscription_api = subscriptionApi;
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
        public async Task<IActionResult> QueryDeviceInfo([FromBody] QueryDeviceModel device_query) {
            var result = await _device_api.getDeviceBySerialOrDefault(
                new DeviceAPI.DeviceSerial(device_query.DeviceSerialNumber));

            if (result == null) {
                return new JsonResult(new Dictionary<string, string>() {
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