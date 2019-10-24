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
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using DroHub.Areas.DHub.SignalRHubs;
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

        private readonly JanusService _janus_service;
        private Dictionary<int, List<Task>> _tasks_by_device_id;
        private CancellationTokenSource _cancellation_token_source;
        private Dictionary<int, bool> _heartbeats_active_by_id;

        public DeviceMicroService(IServiceProvider services,
            ILogger<DeviceMicroService> logger,
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
            _janus_service = janus_service;
            _tasks_by_device_id = new Dictionary<int, List<Task>>();
            _heartbeats_active_by_id = new Dictionary<int, bool>();
            _cancellation_token_source = null;
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
                        while  (await call.ResponseStream.MoveNext(stopping_token)) {
                                DronePosition position = call.ResponseStream.Current;
                                _logger.LogDebug("received position {position}", position);
                                await _hub.Clients.All.SendAsync("position", JsonConvert.SerializeObject(position));
                                await RecordTelemetry(position);
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
                        while (await call.ResponseStream.MoveNext(stopping_token)) {
                            DroneRadioSignal radio_signal = call.ResponseStream.Current;
                            _logger.LogDebug("received radio_signal {radio_signal}", radio_signal);
                            await _hub.Clients.All.SendAsync("radio_signal", JsonConvert.SerializeObject(radio_signal));
                            await RecordTelemetry(radio_signal);
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
                        while (await call.ResponseStream.MoveNext(stopping_token)) {
                            DroneFlyingState flying_state = call.ResponseStream.Current;
                            _logger.LogDebug("received flying_state {flying_state}", flying_state);
                            await _hub.Clients.All.SendAsync("flying_state", JsonConvert.SerializeObject(flying_state));
                            await RecordTelemetry(flying_state);
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
                        while (await call.ResponseStream.MoveNext(stopping_token)) {
                            DroneBatteryLevel battery_level = call.ResponseStream.Current;
                            _logger.LogDebug("received battery_level {battery_level}", battery_level);
                            await _hub.Clients.All.SendAsync("battery_level", JsonConvert.SerializeObject(battery_level));
                            await RecordTelemetry(battery_level);
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
                        while (await call.ResponseStream.MoveNext(stopping_token)) {
                            DroneFileList file_list = call.ResponseStream.Current;
                            _logger.LogDebug("received file_list {file_list}", file_list);
                            await _hub.Clients.All.SendAsync("file_list", JsonConvert.SerializeObject(file_list));
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

                        while (await call.ResponseStream.MoveNext(stopping_token))
                        {
                            DroneVideoState video_state = call.ResponseStream.Current;
                            _logger.LogDebug(LoggingEvents.GatherVideoTelemetry, "received video_state {video_state}", video_state);
                        }
                    }
                }
                catch (RpcException e)
                {
                    _logger.LogWarning(LoggingEvents.FileListTelemetry, e.ToString() + "\nWaiting 1 second before retrying");
                    await Task.Delay(1000);
                }
                catch (JanusService.JanusServiceException e)
                {
                    _logger.LogWarning(LoggingEvents.FileListTelemetry, e.ToString() + "\nWaiting 1 second before retrying");
                    await Task.Delay(1000);
                }
                await destroyMountPointForDevice(device);
            }
        }

        private delegate T DroneActionDelegate<T>(Drone.DroneClient my_client);
        private  Task<T> DoDeviceActionAsync<T>(Device device, DroneActionDelegate<T> action, string action_name_for_logging) {
            if (_cancellation_token_source == null)
                throw new InvalidOperationException("Cannot make any action when we have not yet initialized the service async token");

            var task = Task.Run(() =>
            {
                _logger.LogDebug( "Trying to do an action");
                int logging_action = 0;
                T reply = action(_client);

                _logger.LogWarning(logging_action, "{action} attemped on {device}. Result was {result}",
                    action, device.Name, reply, JsonConvert.SerializeObject(device));
                return reply;
            });
            _tasks_by_device_id[device.Id].Add(task); //crash the action if we do not have such a device id, as this means we do not have telemetry
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
            // foreach (var file_entry in file_list.FileEntries) {

            //     var file_post_information = new FilePostInformation{
            //         ResourceId = file_entry.ResourceId,
            //     };
            //     file_post_information.PostAddresses.Add(new FilePostInformation.Types.PostAddress
            //     {
            //         PostUrl = "1da",
            //         HttpHeaders = "sasdsd"
            //     });

            //     DroneActionDelegate<Grpc.Core.AsyncServerStreamingCall<FilePostProgress>> post_file_delegate = (_client => { return _client.postFileTo(file_post_information); });
            //     var postInformation = await DoDeviceAction<Grpc.Core.AsyncServerStreamingCall<FilePostProgress>>(id, post_file_delegate, "PostFileTo");
            // }
        }


        private void joinTasks(List<Task> tasks)
        {
            if (tasks.Any())
            {
                Task tasks_result = Task.WhenAll(tasks.ToArray());
                tasks_result.Wait();
                if (tasks_result.Status == TaskStatus.RanToCompletion)
                    _logger.LogInformation("Task set closed correctly.");
                else if (tasks_result.Status == TaskStatus.Faulted)
                    _logger.LogWarning("Some tasks failed");
            }
        }

        protected List<Task> spawnTelemetryTasks(CancellationToken stopping_token, Device device) {
            var telemetry_tasks = new List<Task>();
            telemetry_tasks.Add(Task.Run(() => GatherVideoSource(stopping_token, device)));
            telemetry_tasks.Add(Task.Run(() => GatherPosition(stopping_token)));
            telemetry_tasks.Add(Task.Run(() => GatherBatteryLevel(stopping_token)));
            telemetry_tasks.Add(Task.Run(() => GatherRadioSignal(stopping_token)));
            telemetry_tasks.Add(Task.Run(() => GatherFlyingState(stopping_token)));
            return telemetry_tasks;
        }

        public void spawnHeartBeatMonitor(Device device) {
            if (_cancellation_token_source == null)
                throw new InvalidOperationException("Cannot make any action when we have not yet initialized the service async token");
            spawnHeartBeatMonitor(_cancellation_token_source.Token, device);
        }
        protected void spawnHeartBeatMonitor(CancellationToken stopping_token, Device device) {
            var task = spawnHeartBeatMonitorTask(stopping_token, device);
            if (_tasks_by_device_id.ContainsKey(device.Id))
                _tasks_by_device_id[device.Id].Add(task);
            else
                _tasks_by_device_id.Add(device.Id, new List<Task> { task });
        }

        protected async Task spawnHeartBeatMonitorTask(CancellationToken stopping_token, Device device) {
            lock (_heartbeats_active_by_id)
            {
                if (_heartbeats_active_by_id.ContainsKey(device.Id) && _heartbeats_active_by_id[device.Id])
                {
                    return;
                }
                _heartbeats_active_by_id.Add(device.Id, true);
            }
            while (!stopping_token.IsCancellationRequested)
            {
                var spawned_cancel_src = CancellationTokenSource.CreateLinkedTokenSource(stopping_token);
                var tasks = new List<Task>();
                try
                {
                    using (var call = _client.pingService(new DroneRequest { }, cancellationToken: spawned_cancel_src.Token))
                    {
                        _logger.LogDebug($"Called pingService function for {device.Id} aka {device.Name}");

                        while (await call.ResponseStream.MoveNext(spawned_cancel_src.Token) &&
                            call.ResponseStream.Current.Serial == device.SerialNumber &&
                            call.ResponseStream.Current.Message)
                        {
                            if (!tasks.Any())
                                tasks.AddRange(spawnTelemetryTasks(spawned_cancel_src.Token, device));
                        }
                    }
                }
                catch (RpcException e)
                {
                    _logger.LogDebug(e.Message);
                }
                catch(Exception e) {
                    _logger.LogDebug(e.Message);
                }
                _logger.LogDebug($"No valid state of drone {device.Id} aka {device.Name}");
                spawned_cancel_src.Cancel();
                lock(_heartbeats_active_by_id) {
                    _heartbeats_active_by_id[device.Id] = false;
                }
                joinTasks(tasks);
                await Task.Delay(5000);
            }
        }
        protected async Task spawnHeartBeatMonitors(CancellationToken stopping_token)
        {
            using (var scope = _services.CreateScope())
            {
                //check if the device exists before we add it to the db
                var context = scope.ServiceProvider.GetRequiredService<DroHubContext>();
                var devices = await context.Devices.ToListAsync();
                foreach (var device in devices)
                {
                    spawnHeartBeatMonitor(stopping_token, device);
                }
            }
            joinTasks(_tasks_by_device_id.Values.SelectMany(x => x).ToList());
        }
        protected override async Task ExecuteAsync(CancellationToken stopping_token) {
            _cancellation_token_source = CancellationTokenSource.CreateLinkedTokenSource(stopping_token);
            do
            {
                try
                {
                    await spawnHeartBeatMonitors(stopping_token);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e.Message);
                }
                await Task.Delay(5000);
            } while (!stopping_token.IsCancellationRequested);
        }
    }
}