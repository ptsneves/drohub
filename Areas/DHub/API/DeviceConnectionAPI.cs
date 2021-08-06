using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Helpers;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using DroHub.Data;
using DroHub.Helpers.Thrift;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DroHub.Areas.DHub.API {

    public static class DeviceSessionExtensions {
        public static IIncludableQueryable<DeviceConnection, ICollection<TTelemetry>> IncludeTelemetry<TTelemetry>(
            this IQueryable<DeviceConnection> source,
            IncludeTelemetryDelegate<TTelemetry> d) where TTelemetry : IDroneTelemetry {
            return d(source);
        }

        public static Task<TTelemetry> doDeviceAction<TTelemetry>(this Drone.Client client,
        DeviceActionDelegate<TTelemetry> d)
            where TTelemetry : IDroneTelemetry {
            return d(client);
        }

        public delegate Task<TTelemetry> DeviceActionDelegate<TTelemetry>(Drone.Client client)
            where TTelemetry : IDroneTelemetry;

        public delegate IIncludableQueryable<DeviceConnection, ICollection<TTelemetry>> IncludeTelemetryDelegate<TTelemetry>
        (IQueryable<DeviceConnection> source) where TTelemetry : IDroneTelemetry;

        public static void AddDeviceConnectionSessionAPI (this IServiceCollection services) {
            services.AddTransient<DeviceConnectionAPI>();
        }
    }

    public class DeviceConnectionException : Exception {
        internal DeviceConnectionException(string message) : base(message) {
        }
    }

    public class DeviceConnectionAPI {

        private static readonly ConcurrentDictionary<ThriftMessageHandler, DeviceConnection> _connections =
            new ConcurrentDictionary<ThriftMessageHandler, DeviceConnection>();

        private readonly DroHubContext _db_context;
        private readonly DeviceAPI _device_api;
        private readonly ILogger<DeviceConnectionAPI> _logger;
        private readonly SubscriptionAPI _subscription_api;

        public DeviceConnectionAPI(DroHubContext db_context, DeviceAPI device_api,
            ILogger<DeviceConnectionAPI> logger, SubscriptionAPI subscription_api) {

            _db_context = db_context;
            _device_api = device_api;
            _logger = logger;
            _subscription_api = subscription_api;
        }

        private IEnumerable<ThriftMessageHandler> getActiveSubscriptionConnections() {
            var devices_serials = _subscription_api
                .querySubscribedDevices()
                .Select(d => d.SerialNumber);

            return _connections.Keys
                .Where(h => devices_serials.Contains(h.SerialNumber.Value));
        }

        public DeviceConnection getCurrentConnectionOrDefault() {
            var handler = getRPCSessionOrDefault(getDeviceSerialNumberFromCurrentConnectionClaim());
            if (handler != null) {
                return _connections.TryGetValue(handler, out var result) ? result : null;
            }

            return null;
        }

        private static DeviceConnection getActiveDeviceConnection(Device device) {
            var handler = getRPCSessionOrDefault(device);
            if (handler != null) {
                return _connections.TryGetValue(handler, out var result) ? result : null;
            }

            return null;
        }

        public static DateTimeOffset? getConnectionStartTimeOrDefault(Device device) {
            var device_connection = getActiveDeviceConnection(device);
            return device_connection?.StartTime;
        }

        public async Task<TTelemetry> doDeviceAction<TTelemetry>(Device device,
            DeviceSessionExtensions.DeviceActionDelegate<TTelemetry> d) where TTelemetry : IDroneTelemetry {

            var rpc_session = getRPCSessionOrDefault(device);
            if (rpc_session == null)
                throw new DeviceConnectionException("The device requested is not connected to drohub");

            using var client = rpc_session.getClient<Drone.Client>(_logger);
            return await client.Client.doDeviceAction(d);
        }

        private DeviceAPI.DeviceSerial getDeviceSerialNumberFromCurrentConnectionClaim() {
            return new DeviceAPI.DeviceSerial(_subscription_api.getCurrentUserClaims()
                .Single(c => c.Type == DeviceAuthorizationHandler.TELEMETRY_SERIAL_NUMBER_CLAIM)
                .Value);
        }

        public async Task<Device> getDeviceFromCurrentConnectionClaim() {
            var serial = getDeviceSerialNumberFromCurrentConnectionClaim();
            return await _db_context.Devices
                .Where(d => d.SerialNumber == serial.Value)
                .SingleAsync();
        }


        public async Task<IEnumerable<DeviceConnection>> getSubscribedDeviceConnections() {
            var org_name = _subscription_api.getSubscriptionName();
            var is_admin = _subscription_api.getCurrentUserClaims().Any(c =>
                c.Type == DroHubUser.ADMIN_POLICY_CLAIM && c.Value == DroHubUser.CLAIM_VALID_VALUE);

            IQueryable<DeviceConnection> device_connections = _db_context.DeviceConnections;
            if (!is_admin)
                device_connections = device_connections
                    .Where(cd => cd.SubscriptionOrganizationName == org_name.Value);

            return await device_connections
                .Include(cd => cd.Device)
                .Include(cd => cd.MediaObjects)
                .ThenInclude(media => media.MediaObjectTags)
                .ToArrayAsync();

        }

        public static ThriftMessageHandler getRPCSessionOrDefault(Device device) {
            return getRPCSessionOrDefault(new DeviceAPI.DeviceSerial(device.SerialNumber));
        }

        private static ThriftMessageHandler getRPCSessionOrDefault(DeviceAPI.DeviceSerial device_serial) {
            return _connections.Keys.SingleOrDefault(k => k.SerialNumber.Value == device_serial.Value);
        }

        private ThriftMessageHandler getRPCSessionOrDefault() {
            var serial = getDeviceSerialNumberFromCurrentConnectionClaim();
            return getRPCSessionOrDefault(serial);
        }

        public async Task addRPCSessionHandler(ThriftMessageHandler rpc_handler) {
            if (_connections.Any(r => r.Key.SerialNumber == rpc_handler.SerialNumber))
                throw new InvalidDataException("Cannot have duplicate connections");

            var device = await _device_api.getDeviceBySerial(rpc_handler.SerialNumber);
            var connection = new DeviceConnection {
                StartTime = DateTimeOffset.UtcNow,
                DeviceId = device.Id,
                SubscriptionOrganizationName = _subscription_api.getSubscriptionName().Value
            };

            if (!_connections.TryAdd(rpc_handler, connection)) {
                throw new InvalidProgramException(
                    "For some reason we tried to add an RPC handler which was already existing.");
            }

            await _db_context.DeviceConnections.AddAsync(connection);
            await _db_context.SaveChangesAsync();
            Directory.CreateDirectory(
                LocalStorageHelper.calculateConnectionDirectory(connection.Id));
        }

        public async Task removeRPCSessionHandler(ThriftMessageHandler rpc_handler) {
            if (_connections.TryRemove(rpc_handler, out var connection)) {
                connection.EndTime = DateTimeOffset.UtcNow;
                try {
                    _db_context.DeviceConnections.Update(connection);
                    await _db_context.SaveChangesAsync();
                    _logger.LogInformation("Flight session of {serial} ended", rpc_handler.SerialNumber.Value);
                }
                catch (DbUpdateException e) {
                    foreach (var entry in e.Entries) {
                        if (!(entry.Entity is DeviceConnection))
                            throw;
                        _logger.LogError("Could not close device connection. Or device was removed in the mean time?");
                    }
                }
            }
        }

        public async Task<DeviceConnection> getDeviceConnection<TDroneTelemetry>(long session_id,
            DeviceSessionExtensions.IncludeTelemetryDelegate<TDroneTelemetry> include_delegate)
                where TDroneTelemetry : IDroneTelemetry {
            var session = await queryDeviceConnectionSessionById(session_id)
                .IncludeTelemetry(include_delegate)
                .SingleAsync();

            if (await _device_api.authorizeDeviceActions(session.Device, ResourceOperations.Read))
                return session;
            throw new DeviceAuthorizationException("User is not authorized to see see this connection");
        }

        private IQueryable<DeviceConnection> queryDeviceConnectionSessionById(long session_id) {
            return _db_context.DeviceConnections
                .Where(s => s.Id == session_id)
                .Include(s => s.Device);
        }

        public async Task<DeviceConnection> getDeviceConnectionByTime(Device device, DateTimeOffset time) {
            var device_connection = getActiveDeviceConnection(device);
            if (device_connection != null && device_connection.StartTime <= time)
                return device_connection;

            device_connection = await _db_context.DeviceConnections
                .Where(dc =>
                    device.Id == dc.DeviceId
                    && time >= dc.StartTime
                    && time <= dc.EndTime
                )
                .SingleOrDefaultAsync();

            if (device_connection == null) {
                throw new DeviceConnectionException("No device connection matching provided time");
            }

            return device_connection;
        }

        public async Task deleteDeviceFlightSessions(Device device) {
            if (getRPCSessionOrDefault(device) != null)
                throw new DeviceConnectionException("Cannot delete flight session that is ongoing");
            var sessions = _db_context.DeviceConnections
                .Where(dc => dc.DeviceId == device.Id);
            _db_context.DeviceConnections.RemoveRange(sessions);
            await _db_context.SaveChangesAsync();
        }

        public async Task deleteFlightSessions(SubscriptionAPI.OrganizationName organization_name) {
            if (getActiveSubscriptionConnections().Any())
                throw new DeviceConnectionException("Cannot delete subscription flight sessions when connections are ongoing");

            var sessions = _db_context.DeviceConnections
                .Where(c => c.SubscriptionOrganizationName == organization_name.Value);
            _db_context.DeviceConnections.RemoveRange(sessions);
            await _db_context.SaveChangesAsync();
        }

        public async Task recordDeviceTelemetry(IDroneTelemetry telemetry_data) {
            await _db_context.AddAsync(telemetry_data);
            await _db_context.SaveChangesAsync();
        }
    }
}