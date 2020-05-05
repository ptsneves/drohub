using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public static IQueryable<TDroneTelemetry> IncludeTelemetry<TDroneTelemetry>(
            this IQueryable<Device> source, IncludeTelemetryDelegate<TDroneTelemetry> dele)
            where TDroneTelemetry : IDroneTelemetry {
            return dele(source);
        }

        public delegate IQueryable<TDroneTelemetry> IncludeTelemetryDelegate<out TDroneTelemetry>(IQueryable<Device> source)
            where TDroneTelemetry : IDroneTelemetry;

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
        private readonly ConnectionManager _connection_manager;

        public DeviceAPI(DroHubContext db_context, SubscriptionAPI subscription_api,
        IAuthorizationService authorization_service, ConnectionManager connection_manager) {
            _db_context = db_context;
            _subscription_api = subscription_api;
            _authorization_service = authorization_service;
            _connection_manager = connection_manager;
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

        public Task<Device> getDeviceById(int id) {
            return queryDeviceById(id).SingleAsync();
        }

        [ItemCanBeNull]
        public Task<Device> getDeviceByIdOrDefault(int id) {
            return queryDeviceById(id).SingleOrDefaultAsync();
        }

        public Task<List<TDroneTelemetry>> getTelemetry<TDroneTelemetry>(int id, Range range,
            DeviceExtensions.IncludeTelemetryDelegate<TDroneTelemetry> include_delegate) where TDroneTelemetry : IDroneTelemetry {

            return queryDeviceById(id)
                .IncludeTelemetry(include_delegate)
                .Skip(range.Start.Value-1)
                .Take(Math.Min(range.End.Value+1, 10))
                .ToListAsync();
        }

        public DateTime? getConnectionStartTimeOrDefault(Device device) {
            return device == null ? null :
                _connection_manager.GetConnectionStartTimeOrDefault(new DeviceSerial(device.SerialNumber));
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

        public async Task recordDeviceTelemetry(IDroneTelemetry telemetry_data) {
            await _db_context.AddAsync(telemetry_data);
            await _db_context.SaveChangesAsync();
        }

        public Task<List<Device>> getSubscribedDevices() {
            return _subscription_api
                .querySubscribedDevices()
                .ToListAsync();
        }

        public DeviceSerial getDeviceSerialNumberFromClaim() {
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

        public async Task deleteDevice(int id, bool authorize = true) {
            var d = await getDeviceById(id);
            if (authorize && !await authorizeDeviceActions(d, ResourceOperations.Delete))
                throw new DeviceAuthorizationException("Unauthorized delete device");

            _db_context.Devices.Remove(d);
            await _db_context.SaveChangesAsync();
        }

        public async Task Create(Device device, bool authorize = true) {
            device.SubscriptionOrganizationName ??= _subscription_api.getSubscriptionName().Value;

            if (authorize && !await authorizeDeviceActions(device, ResourceOperations.Create))
                throw new DeviceAuthorizationException("Unauthorized create device");

            if(await _db_context.Devices.AnyAsync(d => d.SerialNumber == device.SerialNumber))
                throw new InvalidDataException("Cannot create device that already exists");

            device.SubscriptionOrganizationName = _subscription_api.getSubscriptionName().Value;

            await _db_context.Devices.AddAsync(device);
            await _db_context.SaveChangesAsync();
        }
    }
}