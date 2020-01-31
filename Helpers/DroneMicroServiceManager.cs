using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DroHub.Areas.DHub.Models;
using Newtonsoft.Json;
using DroHub.Data;
using System.Collections.Generic;
using DroHub.Areas.DHub.SignalRHubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace DroHub.Helpers.Thrift
{
    public class DroneMicroServiceManager : IThriftTasks
    {
        private readonly ILogger<DroneMicroServiceManager> _logger;
        private readonly IHubContext<TelemetryHub> _hub;
        private readonly JanusService _janus_service;
        private readonly IServiceProvider _services;
        private CancellationTokenSource _cancellation_token_source;
        public DroneMicroServiceManager(ILogger<DroneMicroServiceManager> logger,
            IHubContext<TelemetryHub> hub,
            JanusService janus_service,
            IServiceProvider services)
        {
            _logger = logger;
            _logger.LogDebug("Constructed DroneMicroService");
            _hub = hub;
            _janus_service = janus_service;
            _cancellation_token_source = null;
            _services = services;
        }
        public async ValueTask<List<Task>> getTasks(ThriftMessageHandler handler, CancellationToken token)
        {
            using (var scope = _services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DroHubContext>();
                //check if the device exists before we add it to the db
                var device = await context.Devices.FirstOrDefaultAsync(d => d.SerialNumber == handler.SerialNumber);
                if (device == null)
                {
                    _logger.LogInformation("Received data from an unregistered device. Closing the connections");
                    return new List<Task> { };
                }
            }

            _cancellation_token_source = CancellationTokenSource.CreateLinkedTokenSource(token);
            var result = new List<Task>{
                Task.Run(async () => await pingConnection(handler)),
                Task.Run(async () => await GatherPosition(handler)),
                Task.Run(async () => await GatherRadioSignal(handler)),
                Task.Run(async () => await GatherFlyingState(handler)),
                Task.Run(async () => await GatherBatteryLevel(handler)),
                Task.Run(async () => await GatherVideoSource(handler))
            };
            return result;
        }

        private async Task RecordTelemetry(IDroneTelemetry telemetry_data, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;

            using (var scope = _services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DroHubContext>();
                //check if the device exists before we add it to the db
                var device = await context.Devices.FirstOrDefaultAsync(d => d.SerialNumber == telemetry_data.Serial, token);
                if (device == null || token.IsCancellationRequested)
                {
                    _logger.LogDebug("Not saving received telemetry for unregistered device {telemetry_data}", telemetry_data);
                    return;
                }
                context.Add(telemetry_data);
                await context.SaveChangesAsync(token);
            }
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
                await doDroneAction<T>(handler, del, token);
                if (delay_between_action.HasValue)
                    await Task.Delay(delay_between_action.Value, token);
            }
            _logger.LogInformation($"Stopping {t_name} service because we received a cancellation request");
        }
        protected async Task doDroneAction<T>(ThriftMessageHandler handler,
            DeviceActionDelegate<T> del, CancellationToken token) where T : IDroneTelemetry
        {
            string t_name = typeof(T).FullName;
            _logger.LogDebug($"Starting Service {t_name}");
            try
            {

                using (var client = handler.getClient<Drone.Client>(_logger))
                {
                    T telemetry = await del(client.Client, token);
                    if (!token.IsCancellationRequested)
                    {
                        _logger.LogDebug($"received {t_name} {telemetry}", telemetry);
                        await _hub.Clients.All.SendAsync(t_name, JsonConvert.SerializeObject(telemetry), token);
                        await RecordTelemetry(telemetry, token);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning($"{t_name} service failed with {e.ToString()}");
                await Task.Delay(5000, token);
            }
        }
        private async Task pingConnection(ThriftMessageHandler handler)
        {
            DeviceActionDelegate<DroneReply> del = (async (client, token) =>
            {
                var result = await client.pingServiceAsync(token);
                result.ActionName = "ping service";
                return result;
            });
            await doDroneActionForEver<DroneReply>(handler, del, TimeSpan.FromSeconds(5));
        }

        private async Task GatherPosition(ThriftMessageHandler handler) {
            DeviceActionDelegate<DronePosition> del = ((client, token) =>
            {
                return client.getPositionAsync(token);
            });
            await doDroneActionForEver<DronePosition>(handler, del);
        }
        protected async Task GatherRadioSignal(ThriftMessageHandler handler)
        {
            DeviceActionDelegate<DroneRadioSignal> del = ((client, token) => {
                return client.getRadioSignalAsync(token);
            });
            await doDroneActionForEver<DroneRadioSignal>(handler, del);
        }

        protected async Task GatherFlyingState(ThriftMessageHandler handler)
        {
            DeviceActionDelegate<DroneFlyingState> del = ((client, token) =>
            {
                return client.getFlyingStateAsync(token);
            });
            await doDroneActionForEver<DroneFlyingState>(handler, del);
        }
        protected async Task GatherBatteryLevel(ThriftMessageHandler handler)
        {
            DeviceActionDelegate<DroneBatteryLevel> del = ((client, token) =>
            {
                return client.getBatteryLevelAsync(token);
            });
            await doDroneActionForEver<DroneBatteryLevel>(handler, del);
        }

        protected async Task GatherVideoSource(ThriftMessageHandler handler)
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
                    _logger.LogDebug("Saved edit information on device {}", device);
                }
                catch(Exception e) {
                    await destroyMountPointForDevice(device);
                    throw e;
                }
            }

            DeviceActionDelegate<DroneLiveVideoStateResult> video_state_poller = (async (client, token) =>
            {
                var result = await client.getLiveVideoStateAsync(send_video_request, token);
                if (result.State != DroneLiveVideoState.LIVE && result.State != DroneLiveVideoState.STOPPED)
                    return await client.sendLiveVideoToAsync(send_video_request, token);
                return result;
            });

            try
            {
                await doDroneActionForEver<DroneLiveVideoStateResult>(handler, video_state_poller, TimeSpan.FromSeconds(5));
            }
            finally
            {
                await destroyMountPointForDevice(device);
            }
        }

        private async Task<JanusService.VideoRoomEndPoint> createVideoRoomForDevice(Device device)
        {
            var session = await _janus_service.createSession();
            var handle = await _janus_service.createStreamerPluginHandle(session);
            var mountpoint = await _janus_service.createVideoRoom(session, handle, device.Id, device.SerialNumber,
                    "mysecret", 10);
            var date_now = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds().ToString();
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