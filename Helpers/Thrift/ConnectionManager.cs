using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Web;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
namespace DroHub.Helpers.Thrift
{
    public class ConnectionManager
    {
        private struct Connection {
            internal ThriftMessageHandler handler;
            internal DateTime connection_start;
        }
        private readonly ConcurrentDictionary<string, Connection> _connections;
        private readonly ILogger<ConnectionManager> _logger;
        public ConnectionManager(ILogger<ConnectionManager> logger) {
            _logger = logger;
            _connections = new ConcurrentDictionary<string, Connection>();
        }

        public ThriftMessageHandler GetRPCSessionBySerial(string serial) {
            return _connections.FirstOrDefault(p => p.Key == serial).Value.handler;
        }

        public DateTime GetConnectionStartTime(string serial) {
            return _connections
                .FirstOrDefault(p => p.Key == serial)
                .Value.connection_start;
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

        public void RemoveSocket(string serial) {
            if (!_connections.TryRemove(serial, out _))
                _logger.LogError("Could not remove a handler, and this should not be possible");
        }
    }
}