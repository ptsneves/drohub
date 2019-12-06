using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.SignalR;
using DroHub.Areas.DHub.SignalRHubs;
using DroHub.Data;
using System.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using DroHub.Helpers;
using Microsoft.Extensions.Hosting;
using DroHub.Helpers.Thrift;
namespace DroHub.Areas.DHub.Controllers
{
    [Area("DHub")]
    public class DevicesController : AuthorizedController
    {
        private readonly DroHubContext _context;
        private readonly UserManager<DroHubUser> _userManager;
        // --- Default device settings values for new devices (used on create POST method)
        private const string DefaultApperture = "f/4"; // TODO Get value directly from above lists
        private const string DefaultFocusMode = "Auto"; // TODO Get values directly from above lists
        private const string DefaultIso = "200"; // TODO Get value directly from above lists

        private readonly IHubContext<NotificationsHub> _notifications_hubContext;
        private readonly ILogger<DevicesController> _logger;
        private readonly ConnectionManager _device_connection_manager;

        public DevicesController(DroHubContext context, UserManager<DroHubUser> userManager,
            IHubContext<NotificationsHub> hubContext, ILogger<DevicesController> logger,
             ConnectionManager device_connection_manager)
        {
            _context = context;
            _userManager = userManager;
            _notifications_hubContext = hubContext;
            _logger = logger;
            _device_connection_manager = device_connection_manager;
        }

        // --- SETTINGS SELECT LISTS
        // Just return a list of states - in a real-world application this would call
        // into data access layer to retrieve states from a database.
        private static IEnumerable<string> GetAllIsoOptions()
        {
            return new List<string>
            {
                "Auto",
                "100",
                "200",
                "400",
                "800",
                "1600",
                "3200",
                "6400",
                "7600"
            };
        }

        private static IEnumerable<string> GetAllAppertureOptions()
        {
            return new List<string>
            {
                "f/1.4",
                "f/2",
                "f/2.8",
                "f/4",
                "f/5.6",
                "f/8",
                "f/11",
                "f/16",
                "f/22"
            };
        }

        private static IEnumerable<string> GetAllFocusModeOptions()
        {
            return new List<string>
            {
                "Auto",
                "Continuous",
                "One-shot",
                "Manual"
            };
        }

        // This function takes a list of strings and returns a list of SelectListItem objects.
        // These objects are going to be used later in pages to render the DropDownList.
        private static IEnumerable<SelectListItem> GetSelectListItems(IEnumerable<string> elements)
        {
            // Create an empty list to hold result of the operation
            var selectList = new List<SelectListItem>();

            // For each string in the 'elements' variable, create a new SelectListItem object
            // that has both its Value and Text properties set to a particular value.
            // This will result in MVC rendering each item as:
            //     <option value="State Name">State Name</option>
            foreach (var element in elements)
                selectList.Add(new SelectListItem
                {
                    Value = element,
                    Text = element
                });

            return selectList;
        }

        // GET: DroHub/GetDevicesList
        public async Task<IActionResult> GetDevicesList() {
            var currentUser = await _userManager.GetUserAsync(User);

            var device_list = await _context.UserDevices
                .Where(ud => ud.DroHubUser == currentUser)
                .Select(ud => ud.Device)
                .ToListAsync();

            if (device_list.Any() == false)
                return NoContent();

            return Json(device_list);
        }

        // GET: DroHub/Devices/Data/5
        public async Task<IActionResult> Data(int? id)
        {
            var device = await getDeviceById(id);
            return (device == null ? (IActionResult) NotFound() : View(device));
        }

        public async Task<IActionResult> getDevicePositions(int? id) {
            var device = await getDeviceById(id, true);

            return (device == null ? (IActionResult) NotFound() : Json(device.positions));
        }

        // GET: DroHub/Devices/Camera/5
        public async Task<IActionResult> Camera(int? id)
        {
            if (id == null) return NotFound();
            var device = await getDeviceById(id);
            if (device == null) return NotFound();

            // Get all device settings options available
            var deviceIsos = GetAllIsoOptions();
            var deviceAppertures = GetAllAppertureOptions();
            var deviceFocusModes = GetAllFocusModeOptions();

            ViewData["Isos"] = GetSelectListItems(deviceIsos);
            ViewData["Appertures"] = GetSelectListItems(deviceAppertures);
            ViewData["FocusModes"] = GetSelectListItems(deviceFocusModes);

            return View(device);
        }

        private enum DeviceActions {
            TakeOff = 1000,
            Land = 1001
        }

        private async Task<Device> getDeviceById(int? id, bool include_positions = false) {
            if (id == null) return null;

            if (include_positions)
            {
                return await _context.UserDevices
                    .Where(ud => _userManager.GetUserId(User) == ud.DroHubUserId && ud.Device.Id == id)
                    .Select(ud => ud.Device)
                    .FirstOrDefaultAsync();
            }
            else
                return await _context.UserDevices
                    .Where(ud => _userManager.GetUserId(User) == ud.DroHubUserId && ud.Device.Id == id)
                    .Select(ud => ud.Device)
                    .FirstOrDefaultAsync();
        }

        public async Task<IActionResult> TakeOff(int id) {
            var device = await getDeviceById(id);
            var rpc_session = _device_connection_manager.GetRPCSessionById(device.SerialNumber);
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
        public async Task<IActionResult> Land(int id) {
            var device = await getDeviceById(id);
            var rpc_session = _device_connection_manager.GetRPCSessionById(device.SerialNumber);
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

        public async Task<IActionResult> ReturnToHome(int id)
        {
            var device = await getDeviceById(id);
            var rpc_session = _device_connection_manager.GetRPCSessionById(device.SerialNumber);
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

        public async Task<IActionResult> MoveToPosition(int id, float latitude, float longitude, float altitude, double heading)
        {
            var device = await getDeviceById(id);
            var rpc_session = _device_connection_manager.GetRPCSessionById(device.SerialNumber);
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
        public async Task<IActionResult> GetFileList(int id) {
            var device = await getDeviceById(id);
            var rpc_session = _device_connection_manager.GetRPCSessionById(device.SerialNumber);
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

        // GET: DroHub/Devices/Gallery/5
        public IActionResult Gallery(int? id)
        {
            return RedirectToAction(nameof(Gallery), "DeviceRepository", new { area = "DHub", id = id });
        }

        // GET: DroHub/Devices/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: DroHub/Devices/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,SerialNumber,CreationDate,ISO,Apperture,FocusMode")]
            Device device)
        {
            if (!ModelState.IsValid)
            {
                return View(device);
            }

            bool exists = _context.Devices.Any(d => d.SerialNumber == device.SerialNumber);
            Device device_to_operate = null;
            if (exists)
            {
                device_to_operate = _context.Devices.Single(d => d.SerialNumber == device.SerialNumber);
            }
            else {
                device.CreationDate = DateTime.Now;
                device.ISO = DefaultIso;
                device.Apperture = DefaultApperture;
                device.FocusMode = DefaultFocusMode;
                device.UserDevices = new List<UserDevice>();
                device_to_operate = device;
                _context.Add(device_to_operate);
            }
            return await Edit(device_to_operate);
        }

        private async Task<IActionResult> Edit(Device device) {
            if (device.UserDevices == null)
            {
                device.UserDevices = new List<UserDevice>();
            }

            device.UserDevices.Add(
                new UserDevice
                {
                    Device = device,
                    DroHubUser = await _userManager.GetUserAsync(User)
                }
            );
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {
                _logger.LogWarning(e.ToString());
                return RedirectToAction(nameof(Create), "Devices");
            }
            return RedirectToAction(nameof(Data), new { id = device.Id });
        }

        // GET: DroHub/Devices/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var device = await getDeviceById(id);

            if (device == null) return NotFound();

            // Get all device settings options available
            var deviceIsos = GetAllIsoOptions();
            var deviceAppertures = GetAllAppertureOptions();
            var deviceFocusModes = GetAllFocusModeOptions();

            ViewData["Isos"] = GetSelectListItems(deviceIsos);
            ViewData["Appertures"] = GetSelectListItems(deviceAppertures);
            ViewData["FocusModes"] = GetSelectListItems(deviceFocusModes);

            return View(device);
        }

        // POST: DroHub/Devices/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("Id,Name,SerialNumber,CreationDate,ISO,Apperture,FocusMode")]
            Device device)
        {
            if (id != device.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                return View(device);
            }
            return await Edit(device);
        }

        // GET: DroHub/Devices/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var device = await getDeviceById(id);
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
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ud = _context.UserDevices.Single(d => d.DeviceId == id);
            if (ud == null)
            {
                return NotFound();
            }

            _context.UserDevices.Remove(ud);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Create));
        }

        private bool DeviceExists(int id)
        {
            return _context.Devices.Any(d => d.Id == id);
        }
    }
}
