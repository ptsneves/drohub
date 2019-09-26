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
using Microsoft.Extensions.Options;

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
        private readonly IServiceProvider _services;
        private readonly DeviceMicroServiceOptions _device_options;
        public TelemetryListener(IServiceProvider services, ILogger<TelemetryListener> logger, IHubContext<TelemetryHub> hub,
            IOptionsMonitor<DeviceMicroServiceOptions> device_options) {

            _device_options = device_options.CurrentValue;
            _logger = logger;
            _logger.LogDebug( $"Started TelemetryListener{_device_options.Address}:{_device_options.Port}");

            _channel = new Channel($"{_device_options.Address}:{_device_options.Port}", ChannelCredentials.Insecure);
            _client = new Drone.DroneClient(_channel);
            _hub = hub;
            _services = services;
        }
        protected async Task RecordPosition(DronePosition position) {
            using (var scope = _services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DroHubContext>();
                context.Add(position);
                await context.SaveChangesAsync();
            }
        }

        protected async Task GatherPosition(CancellationToken stopping_token) {
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
                                _logger.LogDebug("received position {position}", position);
                                await _hub.Clients.All.SendAsync("position", JsonConvert.SerializeObject(position) );
                                await RecordPosition(position);
                            }
                            else {
                                _logger.LogDebug(LoggingEvents.PositionTelemetry, "Nothing received.Waiting");
                                await Task.Delay(1000);
                            }
                        }
                    }
                }
                catch (RpcException e)
                {
                    _logger.LogWarning(LoggingEvents.PositionTelemetry, e.ToString() + "\nWaiting 1 second before retrying");
                    await Task.Delay(1000);
                    _logger.LogDebug(LoggingEvents.PositionTelemetry, "Calling again");
                }
            }
        }

        protected async Task GatherRadioSignal(CancellationToken stopping_token) {
            while (!stopping_token.IsCancellationRequested)
            {
                try
                {
                    using (var call = _client.getRadioSignal(new DroneRequest { }, cancellationToken: stopping_token))
                    {
                        while (!stopping_token.IsCancellationRequested)
                        {
                            if (await call.ResponseStream.MoveNext(stopping_token)) {
                                DroneRadioSignal radio_signal = call.ResponseStream.Current;
                                _logger.LogDebug("received radio_signal {radio_signal}", radio_signal);
                                await _hub.Clients.All.SendAsync("radio_signal", JsonConvert.SerializeObject(radio_signal) );
                            }
                            else {
                                _logger.LogDebug(LoggingEvents.RadioSignalTelemetry, "Nothing received. Waiting");
                                await Task.Delay(1000);
                            }
                        }
                    }
                }
                catch (RpcException e)
                {
                    _logger.LogWarning(LoggingEvents.RadioSignalTelemetry, e.ToString() + "\nWaiting 1 second before retrying");
                    await Task.Delay(1000);
                    _logger.LogDebug(LoggingEvents.RadioSignalTelemetry, "Calling again");
                }
            }
        }

        protected async Task GatherBatteryLevel(CancellationToken stopping_token) {
            while (!stopping_token.IsCancellationRequested)
            {
                try
                {
                    using (var call = _client.getBatteryLevel(new DroneRequest { }, cancellationToken: stopping_token))
                    {
                        while (!stopping_token.IsCancellationRequested)
                        {
                            if (await call.ResponseStream.MoveNext(stopping_token)) {
                                DroneBatteryLevel battery_level = call.ResponseStream.Current;
                                _logger.LogDebug("received battery_level {battery_level}", battery_level);
                                await _hub.Clients.All.SendAsync("battery_level", JsonConvert.SerializeObject(battery_level) );
                            }
                            else {
                                _logger.LogDebug(LoggingEvents.BatteryLevelTelemetry, "Nothing received.Waiting");
                                await Task.Delay(1000);
                            }
                        }
                    }
                }
                catch (RpcException e)
                {
                    _logger.LogWarning(LoggingEvents.BatteryLevelTelemetry, e.ToString() + "\nWaiting 1 second before retrying");
                    await Task.Delay(1000);
                    _logger.LogDebug(LoggingEvents.BatteryLevelTelemetry, "Calling again");
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stopping_token) {
            stopping_token.Register(() =>
                    _logger.LogWarning(LoggingEvents.Telemetry, "TelemetryListener tasks are stopping."));

            Task.Run(() => GatherPosition(stopping_token));
            Task.Run(() => GatherBatteryLevel(stopping_token));
            Task.Run(() => GatherRadioSignal(stopping_token));
        }
    }
}