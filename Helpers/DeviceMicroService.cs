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
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using DroHub.Areas.DHub.SignalRHubs;
using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.SignalR;

namespace DroHub.Helpers {
    public class DeviceMicroService : BackgroundService
    {
        private readonly ILogger<DeviceMicroService> _logger;
        private Channel _channel;
        private Drone.DroneClient _client;
        private readonly IHubContext<TelemetryHub> _hub;
        private readonly IServiceProvider _services;
        private readonly DeviceMicroServiceOptions _device_options;

        private readonly List<Task> _telemetry_tasks;
        private readonly List<Task> _action_tasks;
        private readonly CancellationTokenSource _action_cancelation_source;
        private readonly JanusService _janus_service;
        public DeviceMicroService(IServiceProvider services,
            ILogger<DeviceMicroServiceHelper> logger,
            IHubContext<TelemetryHub> hub,
            IOptionsMonitor<DeviceMicroServiceOptions> device_options,
            JanusService janus_service) {

            _device_options = device_options.CurrentValue;
            _logger = logger;
            _logger.LogDebug($"Started DeviceMicroService{_device_options.Address}:{_device_options.Port}");

            _channel = new Channel($"{_device_options.Address}:{_device_options.Port}", ChannelCredentials.Insecure);
            _client = new Drone.DroneClient(_channel);
            _hub = hub;
            _services = services;
            _telemetry_tasks = new List<Task>();
            _action_tasks= new List<Task>();
            _action_cancelation_source = new CancellationTokenSource();
            _janus_service = janus_service;
        }

        protected async Task RecordTelemetry(IDroneTelemetry telemetry_data)
        {
            using (var scope = _services.CreateScope())
            {
                //check if the device exists before we add it to the db
                var context = scope.ServiceProvider.GetRequiredService<DroHubContext>();
                var device = await context.Devices.FirstOrDefaultAsync(d => d.SerialNumber == telemetry_data.Serial);
                if (device == null)
                {
                    _logger.LogDebug("Not saving received telemetry for unregistered device {}", telemetry_data);
                    return;
                }
                context.Add(telemetry_data);
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
                                await _hub.Clients.All.SendAsync("position", JsonConvert.SerializeObject(position));
                                await RecordTelemetry(position);
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
                                await _hub.Clients.All.SendAsync("radio_signal", JsonConvert.SerializeObject(radio_signal));
                                await RecordTelemetry(radio_signal);
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

        protected async Task GatherFlyingState(CancellationToken stopping_token) {
            while (!stopping_token.IsCancellationRequested)
            {
                try
                {
                    using (var call = _client.getFlyingState(new DroneRequest { }, cancellationToken: stopping_token))
                    {
                        while (!stopping_token.IsCancellationRequested)
                        {
                            if (await call.ResponseStream.MoveNext(stopping_token)) {
                                DroneFlyingState flying_state = call.ResponseStream.Current;
                                _logger.LogDebug("received flying_state {flying_state}", flying_state);
                                await _hub.Clients.All.SendAsync("flying_state", JsonConvert.SerializeObject(flying_state));
                                await RecordTelemetry(flying_state);
                            }
                            else {
                                _logger.LogDebug(LoggingEvents.FlyingStateTelemetry, "Nothing received. Waiting");
                                await Task.Delay(1000);
                            }
                        }
                    }
                }
                catch (RpcException e)
                {
                    _logger.LogWarning(LoggingEvents.FlyingStateTelemetry, e.ToString() + "\nWaiting 1 second before retrying");
                    await Task.Delay(1000);
                    _logger.LogDebug(LoggingEvents.FlyingStateTelemetry, "Calling again");
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
                                await _hub.Clients.All.SendAsync("battery_level", JsonConvert.SerializeObject(battery_level));
                                await RecordTelemetry(battery_level);
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

        protected async Task GatherFileList(CancellationToken stopping_token) {
            while (!stopping_token.IsCancellationRequested)
            {
                try
                {
                    using (var call = _client.getFileListStream(new DroneRequest { }, cancellationToken: stopping_token))
                    {
                        while (!stopping_token.IsCancellationRequested)
                        {
                            if (await call.ResponseStream.MoveNext(stopping_token)) {
                                DroneFileList file_list = call.ResponseStream.Current;
                                _logger.LogDebug("received file_list {file_list}", file_list);
                                await _hub.Clients.All.SendAsync("file_list", JsonConvert.SerializeObject(file_list));
                            }
                            else {
                                _logger.LogDebug(LoggingEvents.FileListTelemetry, "Nothing received.Waiting");
                                await Task.Delay(1000);
                            }
                        }
                    }
                }
                catch (RpcException e)
                {
                    _logger.LogWarning(LoggingEvents.FileListTelemetry, e.ToString() + "\nWaiting 1 second before retrying");
                    await Task.Delay(1000);
                    _logger.LogDebug(LoggingEvents.FileListTelemetry, "Calling again");
                }
            }
        }

        protected async Task GatherVideoSources(CancellationToken stopping_token) {
            using (var scope = _services.CreateScope())
            {
                //check if the device exists before we add it to the db
                var context = scope.ServiceProvider.GetRequiredService<DroHubContext>();
                var devices = await context.Devices.ToListAsync();
                foreach (var device in devices) {
                    _telemetry_tasks.Add(Task.Run(() => GatherVideoSource(stopping_token, device)));
                }
            }
        }

        private async Task<JanusService.RTPMountPoint> createMountPointForDevice(Device device)
        {
            var session = await _janus_service.createSession();
            var handle = await _janus_service.createStreamerPluginHandle(session);
            var mountpoint = await _janus_service.createRTPVideoMountPoint(session, handle, device.Id, device.SerialNumber,
                    "mysecret", 100, "VP8/90000", null);
            return mountpoint;
        }

        private async Task destroyMountPointForDevice(Device device)
        {
            var session = await _janus_service.createSession();
            var handle = await _janus_service.createStreamerPluginHandle(session);
            await _janus_service.destroyMountPoint(session, handle, device.Id);
        }

        protected async Task GatherVideoSource(CancellationToken stopping_token, Device device) {
            while (!stopping_token.IsCancellationRequested)
            {
                try
                {
                    var mountpoint = await createMountPointForDevice(device);
                    device.LiveVideoRTPUrl = mountpoint.LiveVideoRTPUrl;
                    device.LiveVideoFMTProfile = mountpoint.LiveVideoFMTProfile;
                    device.LiveVideoPt = mountpoint.LiveVideoPt;
                    device.LiveVideoRTPMap = mountpoint.LiveVideoRTPMap;
                    device.LiveVideoSecret = mountpoint.LiveVideoSecret;


                    var send_video_request = new DroneSendVideoRequest
                    {
                        RtpUrl = mountpoint.LiveVideoRTPUrl,
                        VideoType = (mountpoint.LiveVideoRTPMap == "VP8/90000" ? DroneSendVideoRequest.Types.VideoType.Vp8 :
                            DroneSendVideoRequest.Types.VideoType.H264)
                    };
                    using (var call = _client.sendVideoTo(send_video_request, cancellationToken: stopping_token))
                    {
                        using (var scope = _services.CreateScope())
                        {
                            var context = scope.ServiceProvider.GetRequiredService<DroHubContext>();
                            context.Update(device);
                            await context.SaveChangesAsync();
                            _logger.LogDebug("Saved edit information on device {}", device);
                        }

                        while (!stopping_token.IsCancellationRequested)
                        {
                            if (await call.ResponseStream.MoveNext(stopping_token))
                            {
                                DroneVideoState video_state = call.ResponseStream.Current;
                                _logger.LogDebug("received video_state {video_state}", video_state);
                            }
                            else
                            {
                                // _logger.LogDebug(LoggingEvents.FileListTelemetry, "Nothing received.Waiting");
                                await Task.Delay(5000);
                            }
                        }
                    }
                }
                catch (RpcException e)
                {
                    await destroyMountPointForDevice(device);
                    _logger.LogWarning(LoggingEvents.FileListTelemetry, e.ToString() + "\nWaiting 1 second before retrying");
                    await Task.Delay(1000);
                    _logger.LogDebug(LoggingEvents.FileListTelemetry, "Calling again");
                }
                catch (JanusService.JanusServiceException e)
                {
                    await Task.Delay(1000);
                }
                await destroyMountPointForDevice(device);
            }
        }

        private delegate T DroneActionDelegate<T>(Drone.DroneClient my_client);
        private  Task<T> DoDeviceActionAsync<T>(Device device, DroneActionDelegate<T> action, string action_name_for_logging) {
            var task = Task.Run(() =>
            {
                _logger.LogDebug( "Trying to do an action");
                int logging_action = 0;
                T reply = action(_client);

                _logger.LogWarning(logging_action, "{action} attemped on {device}. Result was {result}",
                    action, device.Name, reply, JsonConvert.SerializeObject(device));
                return reply;
            });
            _telemetry_tasks.Add(task);
            return task;
        }

        public Task<DroneReply> TakeOffAsync(Device device) {
            DroneActionDelegate<DroneReply> takeoff_delegate = (_client =>  { return _client.doTakeoff( new DroneRequest{}); });

            return DoDeviceActionAsync<DroneReply>(device, takeoff_delegate, "Takeoff");
        }

        public Task<DroneReply> LandAsync(Device device) {
            DroneActionDelegate<DroneReply> landing_delegate = (_client =>  { return _client.doLanding( new DroneRequest{}); });
            return DoDeviceActionAsync<DroneReply>(device, landing_delegate, "Landing");
        }

        public Task<DroneReply> MoveToPositionAsync(Device device, float latitude, float longitude, float altitude, double heading)
        {
            var new_position = new DroneRequestPosition
            {
                Latitude = latitude,
                Longitude = longitude,
                Altitude = altitude,
                Heading = heading
            };
            _logger.LogDebug("Called move to position with position {latitude} {longitude} {altitude} {heading}", latitude, longitude, altitude, heading);
            DroneActionDelegate<DroneReply> movetoposition = (_client => { return _client.moveToPosition(new_position); });
            return DoDeviceActionAsync<DroneReply>(device, movetoposition, "Move to Position");
        }

        public Task<DroneFileList> GetFileListAsync(Device device) {
            DroneActionDelegate<DroneFileList> get_file_list_delegate = (_client => { return _client.getFileList(new DroneRequest { }); });
            return DoDeviceActionAsync<DroneFileList>(device, get_file_list_delegate, "GetFileList");
        }


        private async void stopAllActionTasks() {
            _logger.LogWarning(LoggingEvents.Telemetry, "DeviceMicroService action tasks are stopping.");
            _action_cancelation_source.Cancel();
            await joinTasks(_action_tasks);
        }

        private async Task joinTasks(List<Task> tasks) {
            Task tasks_result = Task.WhenAll(tasks.ToArray());
            try
            {
                await tasks_result;
            }
            catch
            {
                if (tasks_result.Status == TaskStatus.RanToCompletion)
                   _logger.LogInformation("Task set closed correctly.");
                else if (tasks_result.Status == TaskStatus.Faulted)
                   _logger.LogWarning("Some tasks failed");
            }
        }
        protected override async Task ExecuteAsync(CancellationToken stopping_token) {
            stopping_token.Register(() => stopAllActionTasks());
            _telemetry_tasks.Add(Task.Run(() => GatherVideoSources(stopping_token)));
            _telemetry_tasks.Add(Task.Run(() => GatherPosition(stopping_token)));
            _telemetry_tasks.Add(Task.Run(() => GatherBatteryLevel(stopping_token)));
            _telemetry_tasks.Add(Task.Run(() => GatherRadioSignal(stopping_token)));
            _telemetry_tasks.Add(Task.Run(() => GatherFlyingState(stopping_token)));
            await joinTasks(_telemetry_tasks);
        }
    }
}