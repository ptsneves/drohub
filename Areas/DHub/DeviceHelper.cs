using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity;
using DroHub.Areas.Identity.Data;
using DroHub.Data;
using DroHub.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

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
            var user_principal = await sign_in_manager.CreateUserPrincipalAsync(user);
            var device = await getDeviceBySerial(
                sign_in_manager.UserManager.getCurrentUserWithSubscription(user_principal), device_serial);
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

        private static async Task<Device> getDeviceBySerial(IIncludableQueryable<DroHubUser,Subscription> user,
            string serial_number){
            return await user
                .getCurrentUserSubscription()
                .getSubscriptionDevices()
                .SingleOrDefaultAsync(d => d.SerialNumber == serial_number);
        }

        [ClaimRequirement(Device.CAN_ADD_CLAIM, Device.CLAIM_VALID_VALUE)]
        public static async Task Create(SignInManager<DroHubUser> sign_in_manager, string user_name,
            DroHubContext context, Device device) {
            var user = sign_in_manager
                .UserManager
                .getCurrentUserWithSubscription(user_name);

            await Create(user, context, device);
        }

        [ClaimRequirement(Device.CAN_ADD_CLAIM, Device.CLAIM_VALID_VALUE)]
        public static async Task Create(UserManager<DroHubUser> user_manager, ClaimsPrincipal user,
            DroHubContext context, Device device) {
            await Create(user_manager.getCurrentUserWithSubscription(user), context, device);
        }

        private static async Task Create(IIncludableQueryable<DroHubUser,Subscription> user,
            DroHubContext context, Device device) {

            if(await context.Devices.AnyAsync(d => d.SerialNumber == device.SerialNumber))
                throw new InvalidDataException("Cannot create device that already exists");

            device.Subscription = await user
                .getCurrentUserSubscription()
                .SingleAsync();

            await context.Devices.AddAsync(device);
            await context.SaveChangesAsync();
        }
    }
}