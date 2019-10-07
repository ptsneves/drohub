using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DroHub.Areas.DHub.SignalRHubs
{

    public class TelemetryHub : Hub
    {
        private readonly ILogger<TelemetryHub> _logger;
        public TelemetryHub(ILogger<TelemetryHub> logger)
        {
            _logger = logger;
        }
    }


}