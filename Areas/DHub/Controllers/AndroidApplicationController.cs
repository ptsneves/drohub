using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        private readonly DeviceAPI _device_api;

        public AndroidApplicationController(DeviceAPI device_api) {
            _device_api = device_api;
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

        [HttpPost]
        public async Task<IActionResult> QueryDeviceInfo([FromBody] QueryDeviceModel device_query) {
            var response = new Dictionary<string, Device> {
                ["result"] = await _device_api.getDeviceBySerialOrDefault(
                    new DeviceAPI.DeviceSerial(device_query.DeviceSerialNumber))
            };
            return new JsonResult(response);
        }
    }
}