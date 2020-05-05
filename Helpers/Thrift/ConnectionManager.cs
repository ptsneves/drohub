using System.Collections.Concurrent;
using System;
using System.Linq;
using DroHub.Areas.DHub.API;
using Microsoft.Extensions.Logging;


namespace DroHub.Helpers.Thrift
{
    public class ConnectionManager
    {
        private struct Connection {
            internal ThriftMessageHandler handler;
            internal DateTime connection_start;
        }
        private readonly ConcurrentDictionary<DeviceAPI.DeviceSerial, Connection> _connections;
        private readonly ILogger<ConnectionManager> _logger;
        public ConnectionManager(ILogger<ConnectionManager> logger) {
            _logger = logger;
            _connections = new ConcurrentDictionary<DeviceAPI.DeviceSerial, Connection>();
        }

        public ThriftMessageHandler GetRPCSessionBySerial(DeviceAPI.DeviceSerial serial) {
            return _connections.FirstOrDefault(p => p.Key == serial).Value.handler;
        }

        public DateTime? GetConnectionStartTimeOrDefault(DeviceAPI.DeviceSerial serial) {
            if (_connections.ContainsKey(serial))
                return _connections[serial].connection_start;
            return null;
        }

        public void AddSocket(ThriftMessageHandler handler) {
            //TODO: Make tests for multiple fake connections with the same serial
            var connection = new Connection() {
                handler = handler,
                connection_start = DateTime.Now
            };
            if (!_connections.TryAdd(handler.SerialNumber, connection))  {
                throw new InvalidOperationException("For some reason cannot add a new Handler");
            }
        }

        public void RemoveSocket(DeviceAPI.DeviceSerial serial) {
            if (!_connections.TryRemove(serial, out _))
                _logger.LogError("Could not remove a handler, and this should not be possible");
        }
    }
}