using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DroHub.Areas.DHub;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.DHub.SignalRHubs;
using DroHub.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DroHub.Helpers.Thrift
{
    public class DroneMicroServiceManager : IThriftTasks
    {
        private readonly ILogger<DroneMicroServiceManager> _logger;
        private readonly IHubContext<TelemetryHub> _hub;
        private readonly JanusService _janus_service;
        private readonly IServiceProvider _services;
        private CancellationTokenSource _cancellation_token_source;
        private bool _alive_flag;
        public static TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan SubscriptionUpdateInterval = TimeSpan.FromMinutes(5);
        public DroneMicroServiceManager(ILogger<DroneMicroServiceManager> logger,
            IHubContext<TelemetryHub> hub,
            JanusService janus_service,
            IServiceProvider services) {
            _alive_flag = false;
            _logger = logger;
            _logger.LogDebug("Constructed DroneMicroService");
            _hub = hub;
            _janus_service = janus_service;
            _cancellation_token_source = null;
            _services = services;
        }

        public async ValueTask<bool> doesItPassPreconditions(string device_serial) {
            return await getSubscriptionTimeLeft(device_serial) >= TimeSpan.Zero;
        }

        public ValueTask<List<Task>> getTasks(ThriftMessageHandler handler, CancellationTokenSource token_source)
        {
            _cancellation_token_source = token_source;
            var result = new List<Task>{
                Task.Run(async () => await pingConnection(handler)),
                Task.Run(async () => await GatherPosition(handler)),
                Task.Run(async () => await GatherRadioSignal(handler)),
                Task.Run(async () => await GatherFlyingState(handler)),
                Task.Run(async () => await GatherBatteryLevel(handler)),
                Task.Run(async () => await GatherVideoSource(handler)),
                Task.Run(async () => await MonitorSubscriptionTime(handler)),
                Task.Run(async () => await MonitorConnectionAlive())
            };
            return new ValueTask<List<Task>>(result);
        }

        private async Task<TimeSpan> DecrementAndGetSubscriptionTimeLeft(string serial_number, TimeSpan consumed_time_span) {
            using var scope = _services.CreateScope();
            var db_context = scope.ServiceProvider.GetRequiredService<DroHubContext>();
            var subscription = await DeviceHelper.getDeviceSubscription(serial_number, db_context);
            if (subscription == null)
                throw new InvalidProgramException("Could not find subscription ");
            bool save_failed;
            do {
                save_failed = false;
                subscription.AllowedFlightTime =- consumed_time_span;
                subscription.AllowedFlightTime =
                    subscription.AllowedFlightTime < TimeSpan.Zero ? TimeSpan.Zero : subscription.AllowedFlightTime;

                try {
                    await db_context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException e) {
                    save_failed = true;
                    await e.Entries.Single().ReloadAsync();
                }

                //Only do it in the end so we have a chance to write the value!
                if (save_failed && _cancellation_token_source.Token.IsCancellationRequested)
                    throw new OperationCanceledException();

            } while (save_failed);
            return subscription.AllowedFlightTime;
        }

        private async Task<TimeSpan> getSubscriptionTimeLeft(string serial_number) {
            using var scope = _services.CreateScope();
            var db_context = scope.ServiceProvider.GetRequiredService<DroHubContext>();
            var subscription = await DeviceHelper.getDeviceSubscription(serial_number, db_context);

            return subscription.AllowedFlightTime;
        }

        private async Task MonitorSubscriptionTime(ThriftMessageHandler handler) {
            var time_left = await getSubscriptionTimeLeft(handler.SerialNumber);
            while(time_left > TimeSpan.Zero) {
                var delay = time_left >= SubscriptionUpdateInterval ? SubscriptionUpdateInterval : time_left;
                var delay_start = DateTime.Now;
                try {
                    await Task.Delay(delay, _cancellation_token_source.Token);
                }
                finally {
                    var elapsed_time = DateTime.Now - delay_start;
                    time_left = await DecrementAndGetSubscriptionTimeLeft(handler.SerialNumber, elapsed_time);
                }
            }
            _logger.LogWarning("Subscription expired. Cancelling!");
            _cancellation_token_source.Cancel();
        }

        private async Task MonitorConnectionAlive() {
            do {
                _alive_flag = false;
                await Task.Delay(ConnectionTimeout, _cancellation_token_source.Token);
            } while (_alive_flag);
            _logger.LogInformation("No data received for more than {timeout} seconds", ConnectionTimeout.Seconds);
            _cancellation_token_source.Cancel();
        }

        private static async Task RecordTelemetry(IDroneTelemetry telemetry_data, DroHubContext context) {
            await context.AddAsync(telemetry_data);
            await context.SaveChangesAsync();
        }

        private async Task BroadcastToSignalR(string t_name, IDroneTelemetry telemetry, DroHubContext context) {
            var user_list = await DeviceHelper.getDeviceUsers(telemetry.Serial, context)
                .Select(u => u.Id)
                .ToListAsync();

            await _hub.Clients.Users(user_list).SendAsync(t_name, JsonConvert.SerializeObject(telemetry));
        }

        protected delegate Task<T> DeviceActionDelegate<T>(Drone.Client client,
            CancellationToken token);

        protected async Task doDroneActionForEver<T>(ThriftMessageHandler handler,
                    DeviceActionDelegate<T> del, TimeSpan? delay_between_action = null) where T : IDroneTelemetry
        {
            string t_name = typeof(T).FullName;
            var token = _cancellation_token_source.Token;
            while (!token.IsCancellationRequested)
            {
                await doDroneAction(handler, del, token);
                if (delay_between_action.HasValue)
                    await Task.Delay(delay_between_action.Value, token);
            }
            _logger.LogInformation($"Stopping {t_name} service because we received a cancellation request");
        }
        protected async Task doDroneAction<T>(ThriftMessageHandler handler,
            DeviceActionDelegate<T> del, CancellationToken token) where T : IDroneTelemetry
        {
            var t_name = typeof(T).FullName;
            _logger.LogDebug($"Starting Service {t_name} {handler.SerialNumber}");
            try {
                using var client = handler.getClient<Drone.Client>(_logger);
                var telemetry = await del(client.Client, token);
                _alive_flag = true;
                using var scope = _services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DroHubContext>();
                var device =
                    await context.Devices.FirstOrDefaultAsync(d => d.SerialNumber == telemetry.Serial, token);
                if (device == null || token.IsCancellationRequested) {
                    _logger.LogDebug("Not saving received telemetry for unregistered device {serial}",
                        telemetry.Serial);
                    return;
                }

                _logger.LogWarning($"received {t_name} {telemetry}", telemetry);
                await BroadcastToSignalR(t_name, telemetry, context);
                await RecordTelemetry(telemetry, context);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                _logger.LogWarning($"{t_name} service failed with {e}");
                await Task.Delay(5000, token);
            }
        }

        private async Task pingConnection(ThriftMessageHandler handler) {
            static async Task<DroneReply> Del(Drone.Client client, CancellationToken token) {
                var result = await client.pingServiceAsync(token);
                result.ActionName = "ping service";
                return result;
            }

            await doDroneActionForEver(handler, Del, TimeSpan.FromSeconds(5));
        }

        private async Task GatherPosition(ThriftMessageHandler handler) {
            static Task<DronePosition> Del(Drone.Client client, CancellationToken token)
                => client.getPositionAsync(token);

            await doDroneActionForEver(handler, Del);
        }

        private async Task GatherRadioSignal(ThriftMessageHandler handler) {
            static Task<DroneRadioSignal> Del(Drone.Client client, CancellationToken token) =>
                client.getRadioSignalAsync(token);

            await doDroneActionForEver(handler, Del);
        }

        private async Task GatherFlyingState(ThriftMessageHandler handler) {
            static Task<DroneFlyingState> Del(Drone.Client client, CancellationToken token) =>
                client.getFlyingStateAsync(token);

            await doDroneActionForEver(handler, Del);
        }

        private async Task GatherBatteryLevel(ThriftMessageHandler handler) {
            static Task<DroneBatteryLevel> Del(Drone.Client client, CancellationToken token) {
                return client.getBatteryLevelAsync(token);
            }

            await doDroneActionForEver(handler, Del);
        }

        private async Task GatherVideoSource(ThriftMessageHandler handler)
        {
            DroneSendLiveVideoRequest send_video_request;
            Device device;
            using (var scope = _services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DroHubContext>();
                device = await context.Devices.FirstOrDefaultAsync(d => d.SerialNumber == handler.SerialNumber);
                if (device == null)
                {
                    throw new ApplicationException("Cannot create video mountpoint for unregistered device {}");
                }
                try
                {
                    var video_room = await createVideoRoomForDevice(device);
                    send_video_request = new DroneSendLiveVideoRequest
                    {
                        // RoomSecret = video_room.Secret,
                        RoomId = video_room.Id
                    };
                }
                catch(Exception e) {
                    _logger.LogError(e.ToString());
                    await destroyMountPointForDevice(device);
                    throw;
                }
            }

            async Task<DroneLiveVideoStateResult> VideoStatePoller(Drone.Client client, CancellationToken token) {
                var result = await client.getLiveVideoStateAsync(send_video_request, token);
                if (result.State != DroneLiveVideoState.LIVE && result.State != DroneLiveVideoState.STOPPED)
                    return await client.sendLiveVideoToAsync(send_video_request, token);
                return result;
            }

            try {
                await doDroneActionForEver(handler, VideoStatePoller, TimeSpan.FromSeconds(5));
            }
            finally {
                await destroyMountPointForDevice(device);
            }
        }

        private async Task<JanusService.VideoRoomEndPoint> createVideoRoomForDevice(Device device)
        {
            var session = await _janus_service.createSession();
            var handle = await _janus_service.createStreamerPluginHandle(session);
            var mountpoint = await _janus_service.createVideoRoom(session, handle, device.Id, device.SerialNumber,
                    "mysecret", 10, JanusService.VideoCodecType.H264); // we need to force this for iOS
            return mountpoint;
        }

        private async Task destroyMountPointForDevice(Device device)
        {
            var session = await _janus_service.createSession();
            var handle = await _janus_service.createStreamerPluginHandle(session);
            await _janus_service.destroyVideoRoom(session, handle, device.Id);
        }


        //     protected async Task GatherFileList(CancellationToken stopping_token) {
        //         // while (!stopping_token.IsCancellationRequested)
        //         // {
        //         //     try
        //         //     {
        //         //         using (var call = _client.getFileListStream(new DroneRequest { }, cancellationToken: stopping_token))
        //         //         {
        //         //             while (await call.ResponseStream.MoveNext(stopping_token)) {
        //         //                 DroneFileList file_list = call.ResponseStream.Current;
        //         //                 _logger.LogDebug("received file_list {file_list}", file_list);
        //         //                 await _hub.Clients.All.SendAsync("file_list", JsonConvert.SerializeObject(file_list));
        //         //             }
        //         //         }
        //         //     }
        //         //     catch (RpcException e)
        //         //     {
        //         //         _logger.LogWarning(LoggingEvents.FileListTelemetry, e.ToString() + "\nWaiting 1 second before retrying");
        //         //         await Task.Delay(1000);
        //         //         _logger.LogDebug(LoggingEvents.FileListTelemetry, "Calling again");
        //         //     }
        //         // }
        //     }
    }
}