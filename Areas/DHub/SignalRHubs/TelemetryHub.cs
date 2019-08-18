using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using DroHub.Areas.DHub.Models;
using Newtonsoft.Json;
using DroHub.Data;
using Grpc.Core;
using DroHub.Helpers;

namespace DroHub.Areas.DHub.SignalRHubs
{

    public class TelemetryHub : Hub
    {
        private readonly ILogger<TelemetryHub> _logger;
        public TelemetryHub(DroHubContext context, ILogger<TelemetryHub> logger)
        {
            _logger = logger;
        }
    }

    public class TelemetryListener : BackgroundService
    {
        private readonly ILogger<TelemetryListener> _logger;
        private Channel _channel;
        private Drone.DroneClient _client;
        private readonly IHubContext<TelemetryHub> _hub;

        public TelemetryListener(ILogger<TelemetryListener> logger, IHubContext<TelemetryHub> hub) {
            _logger = logger;
            _logger.LogInformation(LoggingEvents.Telemetry, "Started TelemetryListener");

            _channel = new Channel("127.0.0.1:50051", ChannelCredentials.Insecure);
            _client = new Drone.DroneClient(_channel);
            _hub = hub;
        }

        protected override async Task ExecuteAsync(CancellationToken stopping_token) {
            stopping_token.Register(() =>
                    _logger.LogWarning(LoggingEvents.Telemetry, "TelemetryListener tasks are stopping."));
            while (!stopping_token.IsCancellationRequested)
            {
                try
                {
                    using (var call = _client.getPosition(new DroneRequest { }, cancellationToken: stopping_token))
                    {
                        while (!stopping_token.IsCancellationRequested)
                        {
                            if (await call.ResponseStream.MoveNext(stopping_token)) {
                                DronePosition position = call.ResponseStream.Current;
                                _logger.LogInformation(LoggingEvents.PositionTelemetry, "Received " + position.ToString());
                                await _hub.Clients.All.SendAsync("telemetry", JsonConvert.SerializeObject(position) );
                            }
                            else {
                                _logger.LogInformation(LoggingEvents.PositionTelemetry, "Nothing received.Waiting");
                                await Task.Delay(1000);
                            }
                        }
                    }
                }
                catch (RpcException e)
                {
                    _logger.LogWarning(LoggingEvents.PositionTelemetry, e.ToString() + "\nWaiting 1 second before retrying");
                    await Task.Delay(1000);
                    _logger.LogWarning(LoggingEvents.PositionTelemetry, "Calling again");
                }
            }
        }
    }
}