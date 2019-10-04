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
using System.Threading.Tasks;
using Newtonsoft.Json;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using DroHub.Helpers;
using Microsoft.Extensions.Options;

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
        private readonly DeviceMicroServiceOptions _device_micro_service_options;
        private readonly ILogger<DevicesController> _logger;
        public DevicesController(DroHubContext context, UserManager<DroHubUser> userManager,
            IHubContext<NotificationsHub> hubContext, ILogger<DevicesController> logger,
            IOptionsMonitor<DeviceMicroServiceOptions> device_micro_service_options)
        {
            _context = context;
            _userManager = userManager;
            _notifications_hubContext = hubContext;
            _logger = logger;
            _device_micro_service_options = device_micro_service_options.CurrentValue;
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
            var devices = await _context.Devices.Where(d => d.User == currentUser).ToListAsync();
            List<Device> device_list = new List<Device>();
            foreach(var device in devices) {
                device_list.Add(device);
            }

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

            var currentUser = await _userManager.GetUserAsync(User);

            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id && d.User == currentUser);
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

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                throw new System.InvalidOperationException("Could not find what user are we, so we cannot query associated devices");

            if (include_positions)
            {
                return await _context.Devices
                    .Include(d => d.positions)
                    .FirstOrDefaultAsync(d => d.Id == id && d.User == currentUser);
            }
            else
                return await _context.Devices
                    .FirstOrDefaultAsync(d => d.Id == id && d.User == currentUser);
        }

        private delegate DroneReply DroneActionDelegate(Drone.DroneClient my_client);
        private async Task<bool> DoDeviceAction(int id, DroneActionDelegate action, string action_name_for_logging) {
            var device = await getDeviceById(id);
            Channel channel = new Channel($"{_device_micro_service_options.Address}:{_device_micro_service_options.Port}",
                ChannelCredentials.Insecure);
            var client = new Drone.DroneClient(channel);
            DroneReply reply;
            int logging_action = 0;
            reply = action(client);

            channel.ShutdownAsync().Wait();
            var result = reply.Message ? true : false;

            _logger.LogWarning(logging_action, "{action} attemped on {device}. Result was {result}",
                action, device.Name, result, JsonConvert.SerializeObject(device));
            return result;
        }

        public async Task<IActionResult> TakeOff(int id) {
            DroneActionDelegate takeoff_delegate = (_client =>  { return _client.doTakeoff( new DroneRequest{}); });

            await DoDeviceAction(id, takeoff_delegate, "Takeoff");
            return Ok();
        }
        public async Task<IActionResult> Land(int id) {
            DroneActionDelegate landing_delegate = (_client =>  { return _client.doLanding( new DroneRequest{}); });
            await DoDeviceAction(id, landing_delegate, "Landing");
            return Ok();
        }

        public async Task<IActionResult> MoveToPosition(int id, float latitude, float longitude, float altitude, double heading)
        {
            var new_position = new DroneRequestPosition
            {
                Latitude = latitude,
                Longitude = longitude,
                Altitude = altitude,
                Heading = heading
            };
            _logger.LogDebug("Called move to position with position {latitude} {longitude} {altitude} {heading}", latitude, longitude, altitude, heading);
            DroneActionDelegate movetoposition = (_client => { return _client.moveToPosition(new_position); });
            await DoDeviceAction(id, movetoposition, "Move to Position");
            return Ok();
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
            device.CreationDate = DateTime.Now;
            device.User = await _userManager.GetUserAsync(User);
            device.ISO = DefaultIso;
            device.Apperture = DefaultApperture;
            device.FocusMode = DefaultFocusMode;

            if (!ModelState.IsValid)
            {
                return View(device);
            }

            _context.Add(device);
            try {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException e) {
                _logger.LogWarning(e.ToString());
                return RedirectToAction(nameof(Create), "Devices");
            }

            return RedirectToAction(nameof(Data), new {id = device.Id});
        }

        // GET: DroHub/Devices/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id && d.User == currentUser);

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

            try
            {
                _context.Update(device);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DeviceExists(device.Id))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Data), new { id = id });

        }

        // GET: DroHub/Devices/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id && d.User == currentUser);
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
            var currentUser = await _userManager.GetUserAsync(User);
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id && d.User == currentUser);
            if (device == null)
            {
                return NotFound();
            }

            _context.Devices.Remove(device);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Create));
        }

        private bool DeviceExists(int id)
        {
            return _context.Devices.Any(d => d.Id == id);
        }
    }
}
