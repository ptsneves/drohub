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
        private readonly ConcurrentDictionary<string, ThriftMessageHandler> _handlers;
        private readonly ILogger<ConnectionManager> _logger;
        public ConnectionManager(ILogger<ConnectionManager> logger)
        {
            _logger = logger;
            _handlers = new ConcurrentDictionary<string, ThriftMessageHandler>();
        }

        public ThriftMessageHandler GetRPCSessionBySerial(string serial)
        {
            return _handlers.FirstOrDefault(p => p.Key == serial).Value;
        }

        public ConcurrentDictionary<string, ThriftMessageHandler> GetAll()
        {
            return _handlers;
        }

        public string GetId(ThriftMessageHandler handler)
        {
            return _handlers.FirstOrDefault(p => p.Value.SerialNumber == handler.SerialNumber).Key;
        }

        public string AddSocket(ThriftMessageHandler handler)
        {
            //TODO: Make tests for multiple fake connections with the same serial
            _logger.LogDebug("Creating Handler");
            if (!_handlers.TryAdd(handler.SerialNumber, handler))
            {
                RemoveSocket(handler.SerialNumber);
                if (!_handlers.TryAdd(handler.SerialNumber, handler))
                {
                    throw new InvalidOperationException("For some reason cannot add a new Handler");
                }
            }
            _logger.LogDebug("Added connection for "+ handler.SerialNumber);
            return handler.SerialNumber;
        }

        public void RemoveSocket(string serial)
        {
            if (_handlers.TryRemove(serial, out _))
            {
                _logger.LogDebug("Removed Socket from database");
            }
            else
                _logger.LogWarning("Could not remove a handler, and this should not be possible");
        }
    }
}