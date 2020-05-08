using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using DroHub.Helpers.Thrift;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DroHub.Areas.DHub.Controllers
{
    [Area("DHub")]
    public class DevicesController : AuthorizedController
    {
        private readonly ILogger<DevicesController> _logger;
        private readonly ConnectionManager _device_connection_manager;

        private readonly DeviceAPI _device_api;

        public DevicesController(ILogger<DevicesController> logger,
             ConnectionManager device_connection_manager,
            DeviceAPI device_api)
        {
            _logger = logger;
            _device_connection_manager = device_connection_manager;
            _device_api = device_api;
        }


        // GET: DroHub/GetDevicesList
        public async Task<IActionResult> GetDevicesList(){
            var device_list = await _device_api.getSubscribedDevices();

            if (device_list.Any() == false)
                return NoContent();

            return Json(device_list);
        }

        [NonAction]
        private async Task<IActionResult> GetTelemetry<TelemetryType>(int id, int start_index, int end_index,
            DeviceExtensions.IncludeTelemetryDelegate<TelemetryType> include_delegate) where TelemetryType : IDroneTelemetry {

            if (start_index < 1 || end_index < start_index ) return BadRequest();
            var telemetries = await _device_api.getTelemetry<TelemetryType>(
                id, new Range(start_index, end_index), include_delegate);
            return Json(telemetries);
        }

        public async Task<IActionResult> GetDronePositions([Required]int id, [Required]int start_index,
            [Required]int end_index) {
            static IQueryable<DronePosition> Del(IQueryable<Device> source) {
                return source.SelectMany(d => d.positions);
            }

            return await GetTelemetry(id, start_index, end_index, (DeviceExtensions.IncludeTelemetryDelegate<DronePosition>) Del);
        }

        public async Task<IActionResult> GetDroneBatteryLevels([Required]int id, [Required]int start_index,
            [Required]int end_index)
        {
            DeviceExtensions.IncludeTelemetryDelegate<DroneBatteryLevel> del = (source =>
            {
                return source.SelectMany(d => d.battery_levels);
            });
            return await GetTelemetry(id, start_index, end_index, del);
        }

        public async Task<IActionResult> GetDroneRadioSignals([Required]int id, [Required]int start_index,
            [Required]int end_index)
        {
            DeviceExtensions.IncludeTelemetryDelegate<DroneRadioSignal> del = (source =>
            {
                return source.SelectMany(d => d.radio_signals);
            });
            return await GetTelemetry(id, start_index, end_index, del);
        }

        public async Task<IActionResult> GetDroneFlyingStates([Required]int id, [Required]int start_index,
            [Required]int end_index)
        {
            DeviceExtensions.IncludeTelemetryDelegate<DroneFlyingState> del = (source =>
            {
                return source.SelectMany(d => d.flying_states);
            });
            return await GetTelemetry(id, start_index, end_index, del);
        }

        public async Task<IActionResult> GetDroneReplys([Required]int id, [Required]int start_index,
            [Required]int end_index)
        {
            DeviceExtensions.IncludeTelemetryDelegate<DroneReply> del = (source =>
            {
                return source.SelectMany(d => d.drone_replies);
            });
            return await GetTelemetry(id, start_index, end_index, del);
        }

        public async Task<IActionResult> GetDroneLiveVideoStateResults([Required]int id, [Required]int start_index,
            [Required]int end_index)
        {
            DeviceExtensions.IncludeTelemetryDelegate<DroneLiveVideoStateResult> del = (source =>
            {
                return source.SelectMany(d => d.drone_video_states);
            });
            return await GetTelemetry(id, start_index, end_index, del);
        }

        // GET: DroHub/Devices/Data/5
        public async Task<IActionResult> Data([Required]int id)
        {
            var device = await _device_api.getDeviceById(id);
            return (device == null ? (IActionResult) NotFound() : View(device));
        }

        public async Task<IActionResult> TakeOff([Required]int id) {
            var device = await _device_api.getDeviceById(id);
            var rpc_session = _device_connection_manager.GetRPCSessionBySerial(
                new DeviceAPI.DeviceSerial(device.SerialNumber));
            if (rpc_session != null)
            {
                using (var client = rpc_session.getClient<Drone.Client>(_logger))
                {
                    _logger.LogDebug("Retrieved client handle");
                    await client.Client.doTakeoffAsync(CancellationToken.None);
                    _logger.LogDebug("Finie");
                }
                return Ok();
            }
            return Ok();
        }
        public async Task<IActionResult> Land([Required]int id) {
            var device = await _device_api.getDeviceById(id);
            var rpc_session = _device_connection_manager.GetRPCSessionBySerial(
                new DeviceAPI.DeviceSerial(device.SerialNumber));
            if (rpc_session != null)
            {
                using (var client = rpc_session.getClient<Drone.Client>(_logger))
                {
                    _logger.LogDebug("Retrieved client handle");
                    await client.Client.doLandingAsync(CancellationToken.None);
                    _logger.LogDebug("Finie");
                }
                return Ok();
            }
            return Ok();
        }

        public async Task<IActionResult> ReturnToHome([Required]int id)
        {
            var device = await _device_api.getDeviceById(id);
            var rpc_session = _device_connection_manager.GetRPCSessionBySerial(
                new DeviceAPI.DeviceSerial(device.SerialNumber));
            if (rpc_session != null)
            {
                using (var client = rpc_session.getClient<Drone.Client>(_logger))
                {
                    _logger.LogDebug("Retrieved client handle");
                    await client.Client.doReturnToHomeAsync(CancellationToken.None);
                    _logger.LogDebug("Finie");
                }
                return Ok();
            }
            return Ok();
        }

        public async Task<IActionResult> GetDeviceFlightStartTime([Required] int id) {
            var device = await _device_api.getDeviceByIdOrDefault(id);
            if (!await _device_api.authorizeDeviceActions(device, ResourceOperations.Read))
                return Forbid();

            var start_time_or_default = _device_api.getConnectionStartTimeOrDefault(device);

            return start_time_or_default.HasValue ?
                Json(((DateTimeOffset)start_time_or_default.Value.ToUniversalTime()).ToUnixTimeMilliseconds()) :
                Json(null);
        }

        public async Task<IActionResult> MoveToPosition([Required]int id, [Required]float latitude,
            [Required]float longitude, [Required]float altitude, [Required]double heading)
        {
            var device = await _device_api.getDeviceById(id);
            var rpc_session = _device_connection_manager.GetRPCSessionBySerial(
                new DeviceAPI.DeviceSerial(device.SerialNumber));
            if (rpc_session != null)
            {
                using (var client = rpc_session.getClient<Drone.Client>(_logger))
                {
                    _logger.LogDebug("Retrieved client handle");
                    var drone_request = new DroneRequestPosition
                    {
                        Latitude = latitude,
                        Longitude = longitude,
                        Altitude = altitude,
                        Heading = heading,
                        Serial = device.SerialNumber
                    };
                    await client.Client.moveToPositionAsync(drone_request, CancellationToken.None);
                    _logger.LogDebug("Finie");
                }
                return Ok();
            }
            return Ok(); ;
        }

        [HttpPost]
        public async Task<IActionResult> TakePicture([Required]int id,
            [Required][Bind("ActionType")]DroneTakePictureRequest request) {

            var device = await _device_api.getDeviceById(id);
            var rpc_session = _device_connection_manager.GetRPCSessionBySerial(
                new DeviceAPI.DeviceSerial(device.SerialNumber));
            if (rpc_session != null && request != null)
            {
                using (var client = rpc_session.getClient<Drone.Client>(_logger))
                {
                    await client.Client.takePictureAsync(request, CancellationToken.None);
                }
                return Ok();
            }
            return Ok();
        }

        public async Task<IActionResult> RecordVideo([Required]int id,
            [Required][Bind("ActionType")]DroneRecordVideoRequest request) {

            var device = await _device_api.getDeviceById(id);
            var rpc_session = _device_connection_manager.GetRPCSessionBySerial(
                new DeviceAPI.DeviceSerial(device.SerialNumber));
            if (rpc_session != null && request != null)
            {
                using (var client = rpc_session.getClient<Drone.Client>(_logger))
                {
                    await client.Client.recordVideoAsync(request, CancellationToken.None);
                }
                return Ok();
            }
            return Ok();
        }

        public async Task<IActionResult> GetFileList([Required]int id) {
            var device = await _device_api.getDeviceById(id);
            var rpc_session = _device_connection_manager.GetRPCSessionBySerial(
                new DeviceAPI.DeviceSerial(device.SerialNumber));
            if (rpc_session != null)
            {
                using (var client = rpc_session.getClient<Drone.Client>(_logger))
                {
                    _logger.LogDebug("Retrieved client handle");
                    return Json(await client.Client.getFileListAsync(CancellationToken.None));
                }
            }
            return Ok(); ;
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
            [Required][Bind("Id,Name,SerialNumber,CreationDate,ISO,Apperture,FocusMode")]
            Device device)
        {
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

            return View(device);
        }

        // POST: DroHub/Devices/Delete/5
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed([Required]int id) {
            try {
                await _device_api.deleteDevice(id);
            }
            catch (DeviceAuthorizationException e) {
                ModelState.AddModelError("", e.Message);
                return View();
            }

            return RedirectToAction("Dashboard", "DeviceRepository");
        }
    }
}
