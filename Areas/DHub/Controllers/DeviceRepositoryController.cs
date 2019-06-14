using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using DroHub.Data;
using Dropbox.Api;
using Dropbox.Api.Files;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading.Tasks;

namespace DroHub.Areas.DHub.Controllers
{
    [Area("DHub")]
    public class DeviceRepositoryController : AuthorizedController
    {
        #region Variables
        private readonly DroHubContext _context;
        private readonly UserManager<DroHubUser> _userManager;
        private readonly DropboxRepositorySettings _dropboxRepositorySettings;
        #endregion

        #region Constructor
        public DeviceRepositoryController(DroHubContext context, UserManager<DroHubUser> userManager, IOptions<DropboxRepositorySettings> dropboxRepositorySettings)
        {
            _context = context;
            _userManager = userManager;
            _dropboxRepositorySettings = dropboxRepositorySettings.Value;
        }
        #endregion

        #region Dropbox methods (move them to a dropbox helper Class
        private DropboxClient GetDropboxClient(Device device)
        {
            if (device == null || string.IsNullOrWhiteSpace(device.DropboxToken))
            {
                return null;
            }
            // else
            return new DropboxClient(device.DropboxToken, new DropboxClientConfig(_dropboxRepositorySettings.AppName));
        }
        #endregion

        // GET: /<controller>/
        public async Task<IActionResult> Index(int? id)
        {
            if (id == null) return NotFound();

            DroHubUser currentUser = await _userManager.GetUserAsync(User);
            Device currentDevice = await _context.Devices.FirstOrDefaultAsync(device => device.Id == id && device.User == currentUser);

            if (currentDevice == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(currentDevice.DropboxToken))
            {
                return RedirectToAction(nameof(Gallery), new { id = id });
            }
            // else
            currentDevice.DropboxConnectState = Guid.NewGuid().ToString("N"); // Set new dropbox connect state to this device
            _context.SaveChanges();
            if (_dropboxRepositorySettings.APIKey == null)
                throw new System.InvalidOperationException("API Key is not available. Please set it");

            var redirect = DropboxOAuth2Helper.GetAuthorizeUri(
                OAuthResponseType.Code,
                _dropboxRepositorySettings.APIKey,
                _dropboxRepositorySettings.AuthRedirectUri,
                currentDevice.DropboxConnectState,
                true); // force_reapprove Whether or not to force the user to approve the app again if they've already done so.
                       // If false (default), a user who has already approved the application may be automatically redirected to the URI specified by redirect_uri.
                       // If true, the user will not be automatically redirected and will have to approve the app again.

            return Redirect(redirect.ToString());
        }

        // GET: /<controller>/Auth
        public async Task<ActionResult> Auth(string code, string state)
        {
            try
            {
                DroHubUser currentUser = await _userManager.GetUserAsync(User);
                // Get device by connect state (defined on Index Method)
                Device currentDevice = await _context.Devices.FirstOrDefaultAsync(
                    device => device.DropboxConnectState == state && device.User == currentUser);

                if (currentDevice == null) // same as (currentDevice.ConnectState != state) but not necessary because it's already verified
                                           // above with device.DropboxConnectState == state
                {
                    //this.Flash("There was an error connecting to Dropbox.");
                    return RedirectToAction(nameof(Index), "Devices", new { area = "DHub" });
                }
                // else
                var response = await DropboxOAuth2Helper.ProcessCodeFlowAsync(
                    code,
                    _dropboxRepositorySettings.APIKey,
                    _dropboxRepositorySettings.APISecret,
                    _dropboxRepositorySettings.AuthRedirectUri);

                currentDevice.DropboxToken = response.AccessToken; // Save dropbox token for this device
                currentDevice.DropboxConnectState = string.Empty; // Clear current Dropbox connect state
                await _context.SaveChangesAsync();

                //this.Flash("This account has been connected to Dropbox.", FlashLevel.Success);
                return RedirectToAction(nameof(Index), new { id = currentDevice.Id });
            }
            catch (Exception e)
            {
                var message = string.Format(
                    "code: {0}\nAppKey: {1}\nAppSecret: {2}\nRedirectUri: {3}\nException : {4}",
                    code,
                    _dropboxRepositorySettings.APIKey,
                    _dropboxRepositorySettings.APISecret,
                    _dropboxRepositorySettings.AuthRedirectUri,
                    e);
                //this.Flash(message, FlashLevel.Danger);
                return RedirectToAction(nameof(Index), "Devices", new { area = "DHub" });
            }
        }

        // POST : /Home/Disconnect
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Disconnect(int? id)
        {
            if (id == null) return NotFound();

            DroHubUser currentUser = await _userManager.GetUserAsync(User);
            Device currentDevice = await _context.Devices.FirstOrDefaultAsync(device => device.Id == id && device.User == currentUser);

            if (currentDevice == null) return NotFound();

            currentDevice.DropboxToken = string.Empty;
            await _context.SaveChangesAsync();

            // this.Flash("This account has been disconnected from Dropbox.", FlashLevel.Success);
            return RedirectToAction(nameof(Data), "Devices", new { area = "DHub", id = id });
        }

        // GET: /<controller>/Gallery
        public async Task<IActionResult> Gallery(int? id)
        {
            if (id == null) return NotFound();

            DroHubUser currentUser = await _userManager.GetUserAsync(User);
            Device currentDevice = await _context.Devices.FirstOrDefaultAsync(device => device.Id == id && device.User == currentUser);

            if (currentDevice == null) return NotFound();

            var dropBoxClient = GetDropboxClient(currentDevice);
            if (dropBoxClient == null){
                return RedirectToAction(nameof(Index), new { id = id });
            }


            ViewData["Title"] = "My DroHub Repository";
            ViewData["UserToken"] = currentDevice.DropboxToken;

            return View("~/Areas/DHub/Views/Devices/Gallery.cshtml", currentDevice);
        }

        /// <summary>
        /// Method to Download files from Dropbox
        /// </summary>
        /// <param name="DropboxFolderPath">Dropbox folder path which we want to download</param>
        /// <param name="DropboxFileName"> Dropbox File name availalbe in DropboxFolderPath to download</param>
        /// <param name="DownloadFolderPath"> Local folder path where we want to download file</param>
        /// <param name="DownloadFileName">File name to download Dropbox files in local drive</param>
        /// <returns></returns>
        public async Task<ActionResult> DownloadFile(int? id, string DropboxFolderPath, string DropboxFileName, string DownloadFolderPath, string DownloadFileName)
        {
            if (id == null) NotFound();

            DroHubUser currentUser = await _userManager.GetUserAsync(User);
            Device currentDevice = await _context.Devices.FirstOrDefaultAsync(device => device.Id == id && device.User == currentUser);

            if (currentDevice == null) return NotFound();

            var dropBoxClient = GetDropboxClient(currentDevice);

            if (dropBoxClient == null)
            {
                return RedirectToAction("Index", new { id = id });
            }
            // else
            try
            {
                var response = await dropBoxClient.Files.DownloadAsync(DropboxFolderPath);
                var fileBytes = await response.GetContentAsByteArrayAsync();
                var downloadFile = File(fileBytes, MediaTypeNames.Application.Octet, DownloadFileName);
                return downloadFile;
            }
            catch (Exception ex)
            {
                return RedirectToAction("Index", new { id = id });
            }
        }
    }
}