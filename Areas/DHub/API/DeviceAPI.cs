using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using DroHub.Data;
using DroHub.Helpers.Thrift;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace DroHub.Areas.DHub.API {
    public static class DeviceExtensions
    {
        public static void AddDeviceAPI (this IServiceCollection services) {
            services.AddTransient<DeviceAPI>();
        }
    }

    public class DeviceAPI {
        public readonly struct DeviceSerial {
            public bool Equals(DeviceSerial other) {
                return Value == other.Value;
            }

            public override bool Equals(object obj) {
                return obj is DeviceSerial other && Equals(other);
            }

            public override int GetHashCode() {
                return Value.GetHashCode();
            }

            internal string Value { get;}

            public DeviceSerial(string org_name) {
                if (StringValues.IsNullOrEmpty(org_name))
                    throw new InvalidDataException("Cannot create a null device serial");
                Value = org_name;
            }

            public static bool operator ==(DeviceSerial a, DeviceSerial b) {
                return Equals(a, b);
            }

            public static bool operator !=(DeviceSerial a, DeviceSerial b) {
                return !Equals(a, b);
            }
        }

        private readonly SubscriptionAPI _subscription_api;
        private readonly DroHubContext _db_context;
        private readonly IAuthorizationService _authorization_service;

        public DeviceAPI(DroHubContext db_context, SubscriptionAPI subscription_api,
        IAuthorizationService authorization_service) {
            _db_context = db_context;
            _subscription_api = subscription_api;
            _authorization_service = authorization_service;
        }

        private IQueryable<Device> queryDeviceBySerial(DeviceSerial device_serial) {
            return _db_context.Devices
                .Where(d => d.SerialNumber == device_serial.Value);
        }

        public Task<Device> getDeviceBySerialOrDefault(DeviceSerial device_serial) {
            return queryDeviceBySerial(device_serial).SingleOrDefaultAsync();
        }

        public Task<Device> getDeviceBySerial(DeviceSerial device_serial) {
            return queryDeviceBySerial(device_serial).SingleAsync();
        }

        private IIncludableQueryable<Device, Subscription> getDeviceWithSubscription(DeviceSerial device_serial) {
            return queryDeviceBySerial(device_serial)
                .Include(d => d.Subscription);
        }

        public SubscriptionAPI.OrganizationName getDeviceSubscriptionName(Device device) {
            return new SubscriptionAPI.OrganizationName(device.SubscriptionOrganizationName);
        }

        public async Task<SubscriptionAPI.OrganizationName> getDeviceSubscriptionName(DeviceSerial serial) {
            return getDeviceSubscriptionName(await getDeviceBySerial(serial));
        }

        private IQueryable<Device> queryDeviceById(int id) {
            return _db_context.Devices
                .Where(d => d.Id == id);
        }

        public Task<List<DroHubUser>> getDeviceUsers(DeviceSerial device_serial) {
            return getDeviceWithSubscription(device_serial)
                .ThenInclude(s => s.Users)
                .SelectMany(d => d.Subscription.Users)
                .ToListAsync();
        }

        public async Task<Device> getDeviceById(int id) {
            var device = await queryDeviceById(id).SingleAsync();
            if (!await authorizeDeviceActions(device, ResourceOperations.Read))
                throw new DeviceAuthorizationException("User is not authorized to see see this connection");
            return device;
        }

        [ItemCanBeNull]
        public async Task<Device> getDeviceByIdOrDefault(int id) {
            var device = await queryDeviceById(id).SingleOrDefaultAsync();
            if (device == null)
                return null;

            if (!await authorizeDeviceActions(device, ResourceOperations.Read))
                throw new DeviceAuthorizationException("User is not authorized to see see this connection");

            return device;
        }

        public async Task<bool> authorizeDeviceFlightActions(DeviceSerial serial) {
            var device = await queryDeviceBySerial(serial).SingleOrDefaultAsync();
            if (device == null)
                return false;
            return await authorizeDeviceActions(device, DeviceAuthorizationHandler.DeviceResourceOperations.CanPerformFlightActions);
        }

        public async Task<bool> authorizeDeviceActions(Device device, IAuthorizationRequirement op) {
            if (device == null)
                return true;

            var r= await _authorization_service.AuthorizeAsync(
                _subscription_api.getClaimsPrincipal(), device, op);
            return r.Succeeded;
        }

        private Task loadEntity(DeviceConnection session,
            Expression<Func<DeviceConnection, IEnumerable<IDroneTelemetry>>> e) {
            return _db_context.Entry(session)
                .Collection(e)
                .Query()
                .OrderByDescending(p => p.Timestamp)
                .Take(1)
                .LoadAsync();
        }

        public async Task<IEnumerable<Device>> getSubscribedDevicesLastTelemetry(
            List<Expression<Func<DeviceConnection, IEnumerable<IDroneTelemetry>>>> telemetry_to_load) {
            var result = await _subscription_api
                .querySubscribedDevices()
                .Select(d => new {
                    Device = d,
                    DeviceConnection = d
                        .DeviceConnections
                        .OrderByDescending((dc => dc.Id))
                        .FirstOrDefault()})
                .Where(i => i.DeviceConnection != null)
                .ToListAsync();

            var return_value = new List<Device>();
            foreach (var r in result) {
                if (!await authorizeDeviceActions(r.Device, ResourceOperations.Read))
                    continue;
                foreach (var t in telemetry_to_load) {
                    await loadEntity(r.DeviceConnection, t);
                    r.Device.DeviceConnections = new List<DeviceConnection> {r.DeviceConnection};
                }
                return_value.Add(r.Device);
            }

            return return_value;
        }

        public Task<List<Device>> getSubscribedDevices() {
            return _subscription_api
                .querySubscribedDevices()
                .ToListAsync();
        }

        public DeviceSerial getDeviceSerialNumberFromConnectionClaim() {
            return new DeviceSerial(_subscription_api.getCurrentUserClaims()
                .Single(c => c.Type == DeviceAuthorizationHandler.TELEMETRY_SERIAL_NUMBER_CLAIM)
                .Value);
        }

        public async Task updateDevice(Device device, bool authorize = true) {
            if (authorize && !await authorizeDeviceActions(device, ResourceOperations.Update))
                throw new DeviceAuthorizationException("Unauthorized update device");

            if (_db_context.Devices.Local.All(d => d.Id != device.Id))
                await _db_context.Devices.AddAsync(device);
            await _db_context.SaveChangesAsync();
        }

        public async Task deleteDevice(Device device, bool authorize = true) {
            if (authorize && !await authorizeDeviceActions(device, ResourceOperations.Delete))
                throw new DeviceAuthorizationException("Unauthorized delete device");

            if (DeviceConnectionAPI.getRPCSessionOrDefault(device) != null)
                throw new DeviceConnectionException("Cannot delete device with flight session ongoing");

            _db_context.Devices.Remove(device);
            await _db_context.SaveChangesAsync();
        }

        public async Task Create(Device device, bool authorize = true) {
            device.SubscriptionOrganizationName ??= _subscription_api.getSubscriptionName().Value;

            if (authorize && !await authorizeDeviceActions(device, ResourceOperations.Create))
                throw new DeviceAuthorizationException("Unauthorized create device");

            if(await _db_context.Devices.AnyAsync(d => d.SerialNumber == device.SerialNumber))
                throw new InvalidDataException("Cannot create device that already exists");

            device.SubscriptionOrganizationName = _subscription_api.getSubscriptionName().Value;
            device.CreationDate = DateTimeOffset.UtcNow;
            await _db_context.Devices.AddAsync(device);
            await _db_context.SaveChangesAsync();
        }
    }
}