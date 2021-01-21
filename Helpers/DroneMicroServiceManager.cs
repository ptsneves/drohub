using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.DHub.SignalRHubs;
using DroHub.Helpers.Thrift;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DroHub.Helpers
{
    public class DroneMicroServiceManager : IThriftTasks {
        private readonly IServiceProvider _service_provider;
        private readonly DeadManSwitch _toggle;
        public static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan SubscriptionCheckInterval = TimeSpan.FromSeconds(30);

        public DroneMicroServiceManager(IServiceProvider service_provider) {
            _service_provider = service_provider;
            _toggle = new DeadManSwitch() {IsAlive = false};
        }

        public async Task doTask(CancellationTokenSource token_source) {
            var result = new List<Task> {
                Task.Run(async () => {
                    using var scope = _service_provider.CreateScope();

                    var t = new MonitorSubscriptionTimeService(
                        scope.ServiceProvider.GetService<ILogger<MonitorSubscriptionTimeService>>(),
                        scope.ServiceProvider.GetService<SubscriptionAPI>(),
                        SubscriptionCheckInterval);
                    await runTask(token_source, t);
                }),
                Task.Run(async () => {
                    using var scope = _service_provider.CreateScope();
                    var t = new MonitorConnectionAliveService(
                        scope.ServiceProvider.GetService<ILogger<MonitorConnectionAliveService>>(),
                        ConnectionTimeout);
                    await runTask(token_source, t);
                }),
                Task.Run(async () => {
                    using var scope = _service_provider.CreateScope();
                    using var t = new PingConnectionDeviceService(
                        scope.ServiceProvider.GetService<ILogger<PingConnectionDeviceService>>(),
                        scope.ServiceProvider.GetService<IHubContext<TelemetryHub>>(),
                        scope.ServiceProvider.GetService<DeviceAPI>(),
                        scope.ServiceProvider.GetService<DeviceConnectionAPI>(),
                        _service_provider.GetRequiredService<ThriftMessageHandler>());

                    await runTask(token_source, t);
                }),
                Task.Run(async () => {
                    using var scope = _service_provider.CreateScope();
                    using var t = new GatherDevicePositionService(
                        scope.ServiceProvider.GetService<ILogger<GatherDevicePositionService>>(),
                        scope.ServiceProvider.GetService<IHubContext<TelemetryHub>>(),
                        scope.ServiceProvider.GetService<DeviceAPI>(),
                        scope.ServiceProvider.GetService<DeviceConnectionAPI>(),
                        _service_provider.GetRequiredService<ThriftMessageHandler>());

                    await runTask(token_source, t);
                }),
                Task.Run(async () => {
                    using var scope = _service_provider.CreateScope();
                    using var t = new GatherDeviceRadioSignalService(
                        scope.ServiceProvider.GetService<ILogger<GatherDeviceRadioSignalService>>(),
                        scope.ServiceProvider.GetService<IHubContext<TelemetryHub>>(),
                        scope.ServiceProvider.GetService<DeviceAPI>(),
                        scope.ServiceProvider.GetService<DeviceConnectionAPI>(),
                        _service_provider.GetRequiredService<ThriftMessageHandler>());

                    await runTask(token_source, t);
                }),
                Task.Run(async () => {
                    using var scope = _service_provider.CreateScope();
                    var t = new GatherDeviceFlyingStateService(
                        scope.ServiceProvider.GetService<ILogger<GatherDeviceFlyingStateService>>(),
                        scope.ServiceProvider.GetService<IHubContext<TelemetryHub>>(),
                        scope.ServiceProvider.GetService<DeviceAPI>(),
                        scope.ServiceProvider.GetService<DeviceConnectionAPI>(),
                        _service_provider.GetRequiredService<ThriftMessageHandler>());

                    await runTask(token_source, t);
                }),
                Task.Run(async () => {
                    using var scope = _service_provider.CreateScope();
                    using var t = new GatherDeviceBatteryLevelService(
                        scope.ServiceProvider.GetService<ILogger<GatherDeviceBatteryLevelService>>(),
                        scope.ServiceProvider.GetService<IHubContext<TelemetryHub>>(),
                        scope.ServiceProvider.GetService<DeviceAPI>(),
                        scope.ServiceProvider.GetService<DeviceConnectionAPI>(),
                        _service_provider.GetRequiredService<ThriftMessageHandler>());

                    await runTask(token_source, t);
                }),
                Task.Run(async () => {
                    using var scope = _service_provider.CreateScope();
                    using var t = new GatherDeviceCameraStateService(
                        scope.ServiceProvider.GetService<ILogger<GatherDeviceCameraStateService>>(),
                        scope.ServiceProvider.GetService<IHubContext<TelemetryHub>>(),
                        scope.ServiceProvider.GetService<DeviceAPI>(),
                        scope.ServiceProvider.GetService<DeviceConnectionAPI>(),
                        _service_provider.GetRequiredService<ThriftMessageHandler>());

                    await runTask(token_source, t);
                }),
                Task.Run(async () => {
                    using var scope = _service_provider.CreateScope();
                    using var t = new GatherDeviceGimbalStateService(
                        scope.ServiceProvider.GetService<ILogger<GatherDeviceGimbalStateService>>(),
                        scope.ServiceProvider.GetService<IHubContext<TelemetryHub>>(),
                        scope.ServiceProvider.GetService<DeviceAPI>(),
                        scope.ServiceProvider.GetService<DeviceConnectionAPI>(),
                        _service_provider.GetRequiredService<ThriftMessageHandler>());

                    await runTask(token_source, t);
                }),
                Task.Run(async () => {
                    using var scope = _service_provider.CreateScope();
                    using var t = new GatherDeviceLiveVideoService(
                        scope.ServiceProvider.GetService<ILogger<GatherDeviceLiveVideoService>>(),
                        scope.ServiceProvider.GetService<IHubContext<TelemetryHub>>(),
                        scope.ServiceProvider.GetService<DeviceAPI>(),
                        scope.ServiceProvider.GetService<DeviceConnectionAPI>(),
                        _service_provider.GetRequiredService<ThriftMessageHandler>(),
                        scope.ServiceProvider.GetService<JanusService>(),
                        scope.ServiceProvider.GetService<MediaObjectAndTagAPI>()
                        );

                    await runTask(token_source, t);
                }),

            };
            await Task.WhenAll(result);
        }

        private async Task runTask(CancellationTokenSource cts, IDeviceServiceTask o) {
            try {
                await o.getServiceTask(cts.Token, _toggle);
            }
            catch (OperationCanceledException) {
            }
            finally {
                cts?.Cancel();
            }
        }
    }

    internal interface IDeviceServiceTask {
        public Task getServiceTask(CancellationToken ct, DeadManSwitch toggle);
    }

    internal class DeadManSwitch {
        internal volatile bool IsAlive;
    }

    internal class ServiceBase {
        protected readonly ILogger _logger;

        internal ServiceBase(ILogger logger) {
            _logger = logger;
        }
    }

    internal abstract class DeviceService<TDroneAction> : ServiceBase, IDisposable where TDroneAction : IDroneTelemetry{
        private readonly IHubContext<TelemetryHub> _hub;
        protected readonly TimeSpan _action_repeat_period;
        protected readonly DeviceConnectionAPI _connection_api;
        protected readonly DeviceAPI _device_api;
        protected readonly DeviceAPI.DeviceSerial _authenticated_serial;
        private readonly Drone.Client _client;
        protected readonly DeviceConnection _device_connection;

        protected abstract Task<TDroneAction> doAction(Drone.Client client, CancellationToken token);

        protected DeviceService(ILogger logger,
            IHubContext<TelemetryHub> hub,
            DeviceAPI device_api,
            DeviceConnectionAPI connection_api,
            TimeSpan action_repeat_period,
            ThriftMessageHandler thrift_message_handler) :base(logger) {
            _logger.LogDebug("Constructed DroneMicroService");
            _hub = hub;
            _device_api = device_api;
            _connection_api = connection_api;
            _action_repeat_period = action_repeat_period;
            _authenticated_serial = _device_api.getDeviceSerialNumberFromConnectionClaim();
            _client = thrift_message_handler.getClient<Drone.Client>(_logger).Client;
            _device_connection = _connection_api.getCurrentConnectionOrDefault();
            if (_device_connection == null)
                throw new InvalidProgramException("Unreachable situation");
        }

        private async Task BroadcastToSignalR(string t_name, IDroneTelemetry telemetry) {
            var user_list = (await _device_api
                    .getDeviceUsers(new DeviceAPI.DeviceSerial(telemetry.Serial)))
                .Select(u => u.Id)
                .ToList();

            await _hub.Clients.Users(user_list).SendAsync(t_name, JsonConvert.SerializeObject(telemetry));
        }

        protected async Task doDroneActionForEver(DeadManSwitch toggle, CancellationToken token,
            TimeSpan? delay_between_action = null)  {

            var t_name = typeof(TDroneAction).FullName;
            while (!token.IsCancellationRequested) {
                await doDroneAction(_client, toggle, token);
                if (delay_between_action.HasValue)
                    await Task.Delay(delay_between_action.Value, token);
            }
            _logger.LogDebug($"Stopping {t_name} {_authenticated_serial.Value }service because we received a cancellation request");
        }

        private async Task doDroneAction(Drone.Client client, DeadManSwitch toggle,
        CancellationToken token) {
            var t_name = typeof(TDroneAction).FullName;
            _logger.LogInformation($"Get {t_name} {_authenticated_serial.Value}");
            var telemetry = await doAction(client, token);
            if (telemetry.Serial == null)
                throw new InvalidDataException("Telemetry cannot have null serial");
            if (new DeviceAPI.DeviceSerial(telemetry.Serial) != _authenticated_serial)
                throw new InvalidDataException("Received a different telemetry serial than the one registered.");

            toggle.IsAlive = true;

            _logger.LogInformation($"received {t_name} {_authenticated_serial.Value} {telemetry}", telemetry);
            telemetry.ConnectionId = _device_connection.Id;
            await BroadcastToSignalR(t_name, telemetry);
            await _connection_api.recordDeviceTelemetry(telemetry);
        }

        public virtual Task getServiceTask(CancellationToken ct, DeadManSwitch toggle) {
            _logger.LogDebug($"Starting Service {GetType()} {_authenticated_serial.Value}");
            return doDroneActionForEver(toggle, ct,_action_repeat_period);
        }

        public void Dispose() {
            _client.Dispose();
        }
    }

    internal class MonitorSubscriptionTimeService : ServiceBase, IDeviceServiceTask {
        private readonly SubscriptionAPI _subscription_api;
        private readonly TimeSpan _subscription_update_interval;

        internal MonitorSubscriptionTimeService(ILogger logger,
            SubscriptionAPI subscription_api, TimeSpan subscription_update_interval) :base(logger) {
            _subscription_api = subscription_api;
            _subscription_update_interval = subscription_update_interval;
        }

        public async Task getServiceTask(CancellationToken ct, DeadManSwitch toggle) {
            _logger.LogDebug($"Starting Service {GetType()}");
            var time_left = await _subscription_api.getSubscriptionTimeLeft();
            while(time_left > TimeSpan.Zero) {
                var delay = time_left >= _subscription_update_interval ? _subscription_update_interval : time_left;
                var delay_start = DateTimeOffset.UtcNow;
                try {
                    await Task.Delay(delay, ct);
                }
                finally {
                    var elapsed_time = DateTimeOffset.UtcNow - delay_start;
                    time_left = await _subscription_api.decrementAndGetSubscriptionTimeLeft(elapsed_time, ct);
                }
            }
            _logger.LogWarning("Subscription expired!");
        }
    }

    internal class MonitorConnectionAliveService : ServiceBase, IDeviceServiceTask {
        private readonly TimeSpan _timeout_period;
        internal MonitorConnectionAliveService(ILogger logger, TimeSpan timeout_period): base(logger) {
            _timeout_period = timeout_period;
        }

        public async Task getServiceTask(CancellationToken ct, DeadManSwitch toggle) {
            _logger.LogDebug($"Starting Service {GetType()}");
            do {
                toggle.IsAlive = false;
                await Task.Delay(_timeout_period, ct);
            } while (toggle.IsAlive);
            _logger.LogError("No data received for more than {timeout} seconds", _timeout_period.Seconds);
        }
    }

    internal class PingConnectionDeviceService : DeviceService<DroneReply>, IDeviceServiceTask {

        internal PingConnectionDeviceService(ILogger<PingConnectionDeviceService> logger, IHubContext<TelemetryHub> hub,
            DeviceAPI device_api, DeviceConnectionAPI connection_api, ThriftMessageHandler thrift_message_handler) :
            base(logger, hub, device_api, connection_api, TimeSpan.FromSeconds(5), thrift_message_handler) {
        }

        protected override async Task<DroneReply> doAction(Drone.Client client, CancellationToken token) {
            var result = await client.pingServiceAsync(token);
            result.ActionName = "ping service";
            return result;
        }
    }

    internal class GatherDevicePositionService : DeviceService<DronePosition>, IDeviceServiceTask {
        internal GatherDevicePositionService(ILogger logger, IHubContext<TelemetryHub> hub,
            DeviceAPI device_api, DeviceConnectionAPI connection_api, ThriftMessageHandler thrift_message_handler) :
            base(logger, hub, device_api, connection_api, TimeSpan.FromMilliseconds(1), thrift_message_handler) {
        }

        protected override Task<DronePosition> doAction(Drone.Client client, CancellationToken token) {
            return client.getPositionAsync(token);
        }
    }

    internal class GatherDeviceRadioSignalService : DeviceService<DroneRadioSignal>, IDeviceServiceTask {
        internal GatherDeviceRadioSignalService(ILogger logger,
            IHubContext<TelemetryHub> hub, DeviceAPI device_api, DeviceConnectionAPI connection_api,
            ThriftMessageHandler thrift_message_handler) :
            base(logger, hub, device_api, connection_api,TimeSpan.FromMilliseconds(1), thrift_message_handler) {
        }

        protected override Task<DroneRadioSignal> doAction(Drone.Client client, CancellationToken token) {
            return client.getRadioSignalAsync(token);
        }
    }

    internal class GatherDeviceFlyingStateService : DeviceService<DroneFlyingState>, IDeviceServiceTask {
        internal GatherDeviceFlyingStateService(ILogger logger,
            IHubContext<TelemetryHub> hub, DeviceAPI device_api,
            DeviceConnectionAPI connection_api, ThriftMessageHandler thrift_message_handler) :
            base(logger, hub, device_api, connection_api,TimeSpan.FromMilliseconds(1), thrift_message_handler) {
        }

        protected override Task<DroneFlyingState> doAction(Drone.Client client, CancellationToken token) {
            return client.getFlyingStateAsync(token);
        }
    }

    internal class GatherDeviceBatteryLevelService : DeviceService<DroneBatteryLevel>, IDeviceServiceTask {
        internal GatherDeviceBatteryLevelService(ILogger logger,
            IHubContext<TelemetryHub> hub, DeviceAPI device_api,
            DeviceConnectionAPI connection_api, ThriftMessageHandler thrift_message_handler) :
            base(logger, hub, device_api, connection_api,TimeSpan.FromMilliseconds(1), thrift_message_handler) {
        }

        protected override Task<DroneBatteryLevel> doAction(Drone.Client client, CancellationToken token) {
            return client.getBatteryLevelAsync(token);
        }
    }

    internal class GatherDeviceCameraStateService : DeviceService<CameraState>, IDeviceServiceTask {
        internal GatherDeviceCameraStateService(ILogger logger,
            IHubContext<TelemetryHub> hub, DeviceAPI device_api,
            DeviceConnectionAPI connection_api, ThriftMessageHandler thrift_message_handler) :
            base(logger, hub, device_api, connection_api,TimeSpan.FromMilliseconds(1), thrift_message_handler) {
        }

        protected override Task<CameraState> doAction(Drone.Client client, CancellationToken token) {
            return client.getCameraStateAsync(token);
        }
    }

    internal class GatherDeviceGimbalStateService : DeviceService<GimbalState>, IDeviceServiceTask {
        internal GatherDeviceGimbalStateService(ILogger logger,
            IHubContext<TelemetryHub> hub, DeviceAPI device_api,
            DeviceConnectionAPI connection_api, ThriftMessageHandler thrift_message_handler) :
            base(logger, hub, device_api, connection_api,TimeSpan.FromMilliseconds(1), thrift_message_handler) {
        }

        protected override Task<GimbalState> doAction(Drone.Client client, CancellationToken token) {
            return client.getGimbalStateAsync(token);
        }
    }

    internal class GatherDeviceLiveVideoService : DeviceService<DroneLiveVideoStateResult>, IDeviceServiceTask {
        private readonly JanusService _janus_service;
        private DroneSendLiveVideoRequest _send_video_request;
        private readonly MediaObjectAndTagAPI _media_api;

        internal GatherDeviceLiveVideoService(ILogger logger,
            IHubContext<TelemetryHub> hub, DeviceAPI device_api,
            DeviceConnectionAPI connection_api,
            ThriftMessageHandler thrift_message_handler,
            JanusService janus_service,
            MediaObjectAndTagAPI media_api) :
            base(logger, hub, device_api, connection_api, TimeSpan.FromSeconds(2), thrift_message_handler) {

            _janus_service = janus_service;
            _media_api = media_api;
        }

        private async Task<JanusService.VideoRoomEndPoint> createVideoRoomForDevice(Device device, string media_dir) {
            var session = await _janus_service.createSession();
            var handle = await _janus_service.createStreamerPluginHandle(session);
            return await _janus_service.createVideoRoom(session, handle, device.Id,
                device.SerialNumber, media_dir,
                "mysecret", 10, JanusService
                .VideoCodecType.VP9);
        }

        private async Task destroyMountPointForDevice(Device device)
        {
            var session = await _janus_service.createSession();
            var handle = await _janus_service.createStreamerPluginHandle(session);
            await _janus_service.destroyVideoRoom(session, handle, device.Id);
        }

        public override async Task getServiceTask(CancellationToken ct, DeadManSwitch toggle) {
            _logger.LogDebug($"Starting Service {GetType()} {_authenticated_serial.Value}");
            var device = await _device_api.getDeviceBySerial(_authenticated_serial);
            var media_dir = MediaObjectAndTagAPI.LocalStorageHelper.calculateConnectionDirectory(_device_connection.Id);
            try {
                var video_room = await createVideoRoomForDevice(device, media_dir);
                _send_video_request = new DroneSendLiveVideoRequest {
                    // RoomSecret = video_room.Secret,
                    RoomId = video_room.Id
                };

                await doDroneActionForEver(toggle, ct, TimeSpan.FromSeconds(2));
                _logger.LogInformation($"Finished live video {_authenticated_serial.Value}");
            }
            finally {
                await destroyMountPointForDevice(device);
                try {
                    var r = await MJRPostProcessor.RunConvert(media_dir, true, MediaObjectAndTagAPI.PreviewFileNamePrefix, _logger);
                    if (r.media_type != MJRPostProcessor.ConvertResult.MediaType.VIDEO)
                        throw new InvalidDataException("We do not support non video mjr conversions");
                    const string tag = "live stream";

                    var mo = MediaObjectAndTagAPI.generateMediaObject(r.result_path, r.creation_date_utc,
                        _device_connection.SubscriptionOrganizationName, _device_connection.Id);

                    await _media_api.addMediaObject(mo,new List<string> {tag}, false);

                }
                catch (Exception e) {
                    _logger.LogError(e.Message);
                }
            }
        }

        protected override async Task<DroneLiveVideoStateResult> doAction(Drone.Client client, CancellationToken token) {
            var result = await client.getLiveVideoStateAsync(_send_video_request, token);
            if (result.State != DroneLiveVideoState.LIVE && result.State != DroneLiveVideoState.STOPPED)
                return await client.sendLiveVideoToAsync(_send_video_request, token);
            return result;
        }
    }
}