using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DroHub.Areas.DHub {
    public static class DeviceHelper {
        public class DeviceHelperException : InvalidOperationException {
            public static DeviceHelperException PilotCheckFailed => new DeviceHelperException("Only pilots can get tokens");

            private DeviceHelperException(string value) : base(value) { }
        }

        public static async Task<Device> queryDeviceInfo(SignInManager<DroHubUser> sign_in_manager,
            string user_name, string token, string device_serial) {
            if (!await sign_in_manager.isTokenValid(user_name, token)) {
                return null;
            }

            var user = await sign_in_manager.UserManager.FindByNameAsync(user_name);
            var device = await getDeviceBySerial(sign_in_manager.UserManager,
                await sign_in_manager.CreateUserPrincipalAsync(user),
                device_serial);
            return device;
        }

        public static Task<List<Device>> getSubscribedDevices(UserManager<DroHubUser> user_manager,
            ClaimsPrincipal user) {
            user_manager.getCurrentUserWithSubscription(user)
                .getCurrentUserSubscription()
                .getSubscriptionDevices()
                .ToListAsync().GetAwaiter().GetResult();
            return user_manager.getCurrentUserWithSubscription(user)
                .getCurrentUserSubscription()
                .getSubscriptionDevices()
                .ToListAsync();
        }

        public static Task<Device> getDeviceById(UserManager<DroHubUser> user_manager, ClaimsPrincipal user, int id) {
            return user_manager.getCurrentUserWithSubscription(user)
                .getCurrentUserSubscription()
                .getSubscriptionDevices()
                .SingleAsync(d => d.Id == id);
        }

        private static async Task<Device> getDeviceBySerial(UserManager<DroHubUser> user_manager, ClaimsPrincipal user,
            string serial_number){
            return await user_manager.getCurrentUserWithSubscription(user)
                .getCurrentUserSubscription()
                .getSubscriptionDevices()
                .SingleOrDefaultAsync(d => d.SerialNumber == serial_number);
        }
    }
}