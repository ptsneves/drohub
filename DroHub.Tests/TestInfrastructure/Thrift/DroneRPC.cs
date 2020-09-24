using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using DroHub.Tests.TestInfrastructure;
using DroHub.Areas.DHub.Models;

public class DroneRPC : Drone.IAsync, IDisposable
{
       public static Dictionary<Type, IDroneTelemetry> generateTelemetry(string device_serial, long timestamp) {
            return new Dictionary<Type, IDroneTelemetry> {
                [typeof(DronePosition)] = new DronePosition { Longitude = 0.0f, Latitude = 0.1f, Altitude = 10f,
                    Serial = device_serial, Timestamp = timestamp },
                [typeof(DroneReply)] = new DroneReply { Result = true, Serial = device_serial, Timestamp = timestamp },
                [typeof(DroneRadioSignal)] = new DroneRadioSignal { SignalQuality = 2, Rssi = -23.0f, Serial = device_serial,
                Timestamp = timestamp },
                [typeof(DroneFlyingState)] = new DroneFlyingState { State = FlyingState.LANDED, Serial = device_serial,
                    Timestamp = timestamp },
                [typeof(DroneBatteryLevel)] = new DroneBatteryLevel { BatteryLevelPercent = 100, Serial = device_serial,
                    Timestamp = timestamp },
                [typeof(DroneLiveVideoStateResult)] = new DroneLiveVideoStateResult { State = DroneLiveVideoState.LIVE,
                    Serial = device_serial, Timestamp = timestamp },
                [typeof(CameraState)] = new CameraState { Mode = CameraMode.VIDEO, ZoomLevel = 1.2f, MinZoom = 1.0f,
                    MaxZoom = 2.0f, Serial = device_serial, Timestamp = timestamp},
                [typeof(GimbalState)] = new GimbalState {
                    Pitch = 1.2f,
                    Roll = 1.3f,
                    Yaw = 1.4f,
                    MaxPitch = 180f,
                    MaxRoll = 180f,
                    MaxYaw = 90f,
                    MinPitch = 0,
                    MinRoll = -90f,
                    MinYaw = -180f,
                    CalibrationState = GimbalCalibrationState.CALIBRATED,
                    IsPitchStabilized = true,
                    IsRollStastabilized = true,
                    IsYawStabilized = false,
                    Serial = device_serial,
                    Timestamp = timestamp
                }
            };
        }

    private bool disposed = false;
    private Dictionary<Type, BlockingCollection<IDroneTelemetry>> collections;
    private bool _inifinte;
    private bool _alive_flag;

    public DroneRPC(TelemetryMock tmock, bool infinite) {
        collections = new Dictionary<Type, BlockingCollection<IDroneTelemetry>>();
        foreach (var item in tmock.TelemetryItems) {
            var new_collection = new BlockingCollection<IDroneTelemetry>();
            new_collection.Add(item.Value.Telemetry);
            collections[item.Key] = new_collection;
        }

        _inifinte = infinite;
        _alive_flag = true;
    }

    public async Task MonitorConnection(TimeSpan check_interval, CancellationToken tkn) {
        do {
            _alive_flag = false;
            await Task.Delay(check_interval, tkn);
        } while (_alive_flag);
    }

    private async Task<T> GetTelemetryItem<T>() where T : IDroneTelemetry
    {
        var type = typeof(T);
        T temp =(T)collections[type].Take();
        if (_inifinte) {
            collections[type].Add(temp);
            await Task.Delay(1000);
        }

        _alive_flag = true;
        return temp;
    }

    public void Dispose() {
        Dispose(true);
        // GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing) {
        if (disposed)
            return;

        if (disposing)
        {
            foreach(var collection in collections) {
                collection.Value.Dispose();
            }
        }
        disposed = true;
    }

    Task<DroneBatteryLevel> Drone.IAsync.getBatteryLevelAsync(CancellationToken cancellationToken)
    {
        return GetTelemetryItem<DroneBatteryLevel>();
    }

    Task<DroneFileList> Drone.IAsync.getFileListAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
        // return Task.FromResult<IDroneTelemetry>(GetTelemetryItem<DroneFileList>().Take().Telemetry);
    }

    Task<DroneFlyingState> Drone.IAsync.getFlyingStateAsync(CancellationToken cancellationToken)
    {
        return GetTelemetryItem<DroneFlyingState>();
    }

    Task<DronePosition> Drone.IAsync.getPositionAsync(CancellationToken cancellationToken)
    {
        return GetTelemetryItem<DronePosition>();
    }

    Task<DroneRadioSignal> Drone.IAsync.getRadioSignalAsync(CancellationToken cancellationToken)
    {
        return GetTelemetryItem<DroneRadioSignal>();
    }

    Task<DroneLiveVideoStateResult> Drone.IAsync.getLiveVideoStateAsync(DroneSendLiveVideoRequest request, CancellationToken cancellationToken)
    {
        return GetTelemetryItem<DroneLiveVideoStateResult>();
    }

    Task<DroneReply> Drone.IAsync.pingServiceAsync(CancellationToken cancellationToken)
    {
        return GetTelemetryItem<DroneReply>();
    }

    public Task<GimbalState> getGimbalStateAsync(CancellationToken cancellationToken = default(CancellationToken)) {
        return GetTelemetryItem<GimbalState>();
    }

    public Task<CameraState> getCameraStateAsync(CancellationToken cancellationToken = default(CancellationToken)) {
        return GetTelemetryItem<CameraState>();
    }

    public Task<DroneReply> setGimbalAttitudeAsync(double pitch, double roll, double yaw,
        CancellationToken cancellationToken = default(CancellationToken)) {
        return Task.FromResult(new DroneReply {
            Result = true,
        });
    }

    public Task<DroneLiveVideoStateResult> sendLiveVideoToAsync(DroneSendLiveVideoRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<DroneReply> doTakeoffAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<DroneReply> doLandingAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<DroneReply> doReturnToHomeAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<DroneReply> moveToPositionAsync(DroneRequestPosition request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<DroneReply> takePictureAsync(DroneTakePictureRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<DroneReply> recordVideoAsync(DroneRecordVideoRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<DroneReply> setCameraZoomAsync(double zoom_level, CancellationToken cancellationToken = default(CancellationToken)) {
        return Task.FromResult(new DroneReply {
            Result = true,
        });
    }
}