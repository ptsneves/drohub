using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using DroHub.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DroHub.Areas.DHub.Controllers
{
    [Area("DHub")]
    public class DevicesController : AuthorizedController
    {
        private readonly ILogger<DevicesController> _logger;

        private readonly DeviceAPI _device_api;
        private readonly DeviceConnectionAPI _device_connection_api;
        private readonly SubscriptionAPI _subscription_api;

        public DevicesController(ILogger<DevicesController> logger,
            DeviceAPI device_api, DeviceConnectionAPI device_connection_api, SubscriptionAPI subscription_api)
        {
            _logger = logger;
            _device_api = device_api;
            _device_connection_api = device_connection_api;
            _subscription_api = subscription_api;
        }


        // GET: DroHub/GetDevicesList
        public async Task<IActionResult> GetDevicesList(){
            var device_list = await _device_api.getSubscribedDevices();

            if (device_list.Any() == false)
                return NoContent();

            return Json(device_list);
        }

        [NonAction]
        private async Task<IActionResult> GetTelemetry<TDroneTelemetry>([Required] int id,
            DeviceSessionExtensions.IncludeTelemetryDelegate<TDroneTelemetry> include_delegate)
            where TDroneTelemetry : IDroneTelemetry {
            if (!ModelState.IsValid) {
                return BadRequest();
            }
            try {
                var device_connection = await _device_connection_api.getDeviceConnection(id,
                    include_delegate);
                var r = device_connection
                    .GetType()
                    .GetProperties()
                    .SingleOrDefault(d => d.PropertyType == typeof(ICollection<TDroneTelemetry>))
                    ?.GetValue(device_connection, null) as ICollection<TDroneTelemetry>;

                return Json(r);
            }
            catch (DeviceAuthorizationException) {
                return Unauthorized();
            }
            catch (DeviceConnectionException e) {
                return StatusCode(503, new { message = e.Message});
            }
        }

        public async Task<IActionResult> GetLastDeviceConnectionId([Required] int id){
            if (!ModelState.IsValid) {
                return BadRequest();
            }
            try {
                var device = await _device_api.getDeviceById(id);
                var last_connection = await _device_connection_api.getLastConnectionId(device);
                return Json(last_connection.Id);
            }
            catch (DeviceAuthorizationException) {
                return Unauthorized();
            }
            catch (DeviceConnectionException e) {
                return StatusCode(503, new { message = e.Message});
            }
        }

        public async Task<IActionResult> GetDronePositions([Required]int id) {
            return await GetTelemetry(id, source =>
                source.Include(d => d.positions));
        }

        public async Task<IActionResult> GetDroneBatteryLevels([Required]int id) {
            return await GetTelemetry(id,source =>
                source.Include(d => d.battery_levels));
        }

        public async Task<IActionResult> GetCameraStates([Required]int id) {
            return await GetTelemetry(id, source =>
                source.Include(d => d.camera_states));
        }

        public async Task<IActionResult> GetGimbalStates([Required]int id) {
            return await GetTelemetry(id, source =>
                source.Include(d => d.gimbal_states));
        }

        public async Task<IActionResult> GetDroneRadioSignals([Required]int id) {
            return await GetTelemetry(id, source =>
                source.Include(d => d.radio_signals));
        }

        public async Task<IActionResult> GetDroneFlyingStates([Required]int id) {
            return await GetTelemetry(id, source =>
                source.Include(d => d.flying_states));
        }

        public async Task<IActionResult> GetDroneReplys([Required]int id) {
            return await GetTelemetry(id, source =>
                source.Include(d => d.drone_replies));
        }

        public async Task<IActionResult> GetDroneLiveVideoStateResults([Required]int id) {
            return await GetTelemetry(id, source =>
                source.Include(d => d.drone_video_states));
        }

        // GET: DroHub/Devices/Data/5
        public async Task<IActionResult> Data([Required]int id)
        {
            var device = await _device_api.getDeviceById(id);
            return (device == null ? (IActionResult) NotFound() : View(device));
        }

        public async Task<IActionResult> TakeOff([Required]int id) {
            try {
                var device = await _device_api.getDeviceById(id);
                await _device_connection_api.doDeviceAction(device, async client =>
                    await client.doTakeoffAsync(CancellationToken.None));
                return Ok();
            }
            catch (DeviceAuthorizationException e) {
                return Unauthorized(e.Message);
            }
            catch (DeviceConnectionException e) {
                return StatusCode(503, new { message = e.Message});
            }
        }
        public async Task<IActionResult> Land([Required]int id) {
            try {
                var device = await _device_api.getDeviceById(id);
                await _device_connection_api.doDeviceAction(device, async client =>
                    await client.doLandingAsync(CancellationToken.None));
                return Ok();
            }
            catch (DeviceAuthorizationException e) {
                return Unauthorized(e.Message);
            }
            catch (DeviceConnectionException e) {
                return StatusCode(503, new { message = e.Message});
            }
        }

        public async Task<IActionResult> ReturnToHome([Required]int id) {
            try {
                var device = await _device_api.getDeviceById(id);
                await _device_connection_api.doDeviceAction(device, async client =>
                    await client.doReturnToHomeAsync(CancellationToken.None));
                return Ok();
            }
            catch (DeviceAuthorizationException e) {
                return Unauthorized(e.Message);
            }
            catch (DeviceConnectionException e) {
                return StatusCode(503, new { message = e.Message});
            }
        }

        public async Task<IActionResult> SetCameraZoom([Required] string serial, [Required]double zoom_level) {
            if (!ModelState.IsValid)
                return BadRequest();

            try {
                var device = await _device_api.getDeviceBySerial(new DeviceAPI.DeviceSerial(serial));
                await _device_connection_api.doDeviceAction(device, async client =>
                    await client.setCameraZoomAsync(zoom_level, CancellationToken.None));
                return Ok();
            }
            catch (DeviceAuthorizationException e) {
                return Unauthorized(e.Message);
            }
            catch (DeviceConnectionException e) {
                return StatusCode(503, new { message = e.Message});
            }
        }

        public async Task<IActionResult> SetGimbalPitch([Required] string serial, [Required]double pitch) {
            if (!ModelState.IsValid)
                return BadRequest();

            try {
                var device = await _device_api.getDeviceBySerial(new DeviceAPI.DeviceSerial(serial));
                await _device_connection_api.doDeviceAction(device, async client =>
                    await client.setGimbalAttitudeAsync(pitch, 0.0f, 0.0f, CancellationToken.None));
                return Ok();
            }
            catch (DeviceAuthorizationException e) {
                return Unauthorized(e.Message);
            }
            catch (DeviceConnectionException e) {
                return StatusCode(503, new { message = e.Message});
            }
        }

        public async Task<IActionResult> MoveToPosition([Required]int id, [Required]float latitude,
            [Required]float longitude, [Required]float altitude, [Required]double heading) {
            try {
                var device = await _device_api.getDeviceById(id);
                var drone_request = new DroneRequestPosition
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Altitude = altitude,
                    Heading = heading,
                    Serial = device.SerialNumber
                };
                await _device_connection_api.doDeviceAction(device, async client =>
                    await client.moveToPositionAsync(drone_request, CancellationToken.None));
                return Ok();
            }
            catch (DeviceAuthorizationException e) {
                return Unauthorized(e.Message);
            }
            catch (DeviceConnectionException e) {
                return StatusCode(503, new { message = e.Message});
            }
        }

        [HttpPost]
        public async Task<IActionResult> TakePicture([Required]int id,
            [Required][Bind("ActionType")]DroneTakePictureRequest request) {
            try {
                var device = await _device_api.getDeviceById(id);
                await _device_connection_api.doDeviceAction(device, async client =>
                    await client.takePictureAsync(request, CancellationToken.None));
                return Ok();
            }
            catch (DeviceAuthorizationException e) {
                return Unauthorized(e.Message);
            }
            catch (DeviceConnectionException e) {
                return StatusCode(503, new { message = e.Message});
            }
        }

        public async Task<IActionResult> RecordVideo([Required]int id,
            [Required][Bind("ActionType")]DroneRecordVideoRequest request) {
            try {
                var device = await _device_api.getDeviceById(id);
                await _device_connection_api.doDeviceAction(device, async client =>
                    await client.recordVideoAsync(request, CancellationToken.None));
                return Ok();
            }
            catch (DeviceAuthorizationException e) {
                return Unauthorized(e.Message);
            }
            catch (DeviceConnectionException e) {
                return StatusCode(503, new { message = e.Message});
            }
        }

        public async Task<IActionResult> GetFileList([Required]int id) {
            try {
                var device = await _device_api.getDeviceById(id);
                return Json(await _device_connection_api.doDeviceAction(device, async client =>
                    await client.getFileListAsync(CancellationToken.None)));
            }
            catch (DeviceAuthorizationException e) {
                return Unauthorized(e.Message);
            }
            catch (DeviceConnectionException e) {
                return StatusCode(503, new { message = e.Message});
            }
        }

        public async Task<IActionResult> GetDeviceFlightStartTime([Required] int id) {
            try {
                var device = await _device_api.getDeviceByIdOrDefault(id);
                if (device == null)
                    return Json(null);

                var start_time_or_default = DeviceConnectionAPI.getConnectionStartTimeOrDefault(device);
                return start_time_or_default.HasValue ?
                    Json(((DateTimeOffset)start_time_or_default.Value.ToUniversalTime()).ToUnixTimeMilliseconds()) :
                    Json(null);
            }
            catch (DeviceAuthorizationException e) {
                return Unauthorized(e.Message);
            }
        }

        [ClaimRequirement(DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.CLAIM_VALID_VALUE)]
        public async Task<IActionResult> DeleteAllFlightSessions() {
            var subscription = _subscription_api.getSubscriptionName();
            try {
                await _device_connection_api.deleteFlightSessions(subscription);
            }
            catch (DeviceConnectionException e) {
                return StatusCode(503, new { message = e.Message});
            }

            return Ok();
        }


        [ClaimRequirement(DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.CLAIM_VALID_VALUE)]
        public async Task<IActionResult> DeleteDeviceFlightSessions(int device_id) {
            var device = await _device_api.getDeviceById(device_id);
            try {
                await _device_connection_api.deleteDeviceFlightSessions(device);
            }
            catch (DeviceConnectionException e) {
                return StatusCode(503, new { message = e.Message});
            }
            return Ok();
        }


        // GET: DroHub/Devices/Edit/5
        public async Task<IActionResult> Edit([Required]int id) {
            var device = await _device_api.getDeviceById(id);
            if (device == null) return NotFound();
            return View(device);
        }

        // POST: DroHub/Devices/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Required]int id,
            [Required][Bind("Id,Name,SerialNumber")] Device device) {
            if (id != device.Id) return NotFound();

            if (!ModelState.IsValid)  {
                return View(device);
            }

            try {
                await _device_api.updateDevice(device);
            }
            catch (DeviceAuthorizationException e) {
                ModelState.AddModelError("", e.Message);
            }
            catch (DbUpdateException e) {
                ModelState.AddModelError("", "Failed to add this device to the user");
            }

            return View(device);
        }

        // GET: DroHub/Devices/Delete/5
        public async Task<IActionResult> Delete([Required]int id) {

            var device = await _device_api.getDeviceById(id);
            if (device == null)
            {
                return NotFound();
            }

            return View(new DeleteDeviceConfirmedModel{Device = device});
        }

        public class DeleteDeviceConfirmedModel {
            public Device Device;
            public bool DeleteFlightSessions;
        }

        // POST: DroHub/Devices/Delete/5
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed([Required]int id, bool delete_flight_sessions) {
            try {
                var d = await _device_api.getDeviceById(id);
                if (delete_flight_sessions)
                    await _device_connection_api.deleteDeviceFlightSessions(d);

                await _device_api.deleteDevice(d);
            }
            catch (DeviceAuthorizationException e) {
                ModelState.AddModelError("", e.Message);
                return View();
            }

            return RedirectToAction("Dashboard", "DeviceRepository");
        }
    }
}
