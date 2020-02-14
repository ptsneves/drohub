using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using DroHub.Tests.TestInfrastructure;
using DroHub.Areas.DHub.Models;

public class DroneRPC : Drone.IAsync, IDisposable
{
    private bool disposed = false;
    private Dictionary<Type, BlockingCollection<IDroneTelemetry>> collections;

    public DroneRPC(TelemetryMock tmock) {
        collections = new Dictionary<Type, BlockingCollection<IDroneTelemetry>>();
        foreach (var item in tmock.TelemetryItems) {
            var new_collection = new BlockingCollection<IDroneTelemetry>();
            new_collection.Add(item.Value.Telemetry);
            collections[item.Key] = new_collection;
        }
    }

    private T GetTelemetryItem<T>() where T : IDroneTelemetry
    {
        var type = typeof(T);
        return (T)collections[type].Take();
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
        var r = GetTelemetryItem<DroneBatteryLevel>();
        return Task.FromResult<DroneBatteryLevel>(r);
    }

    Task<DroneFileList> Drone.IAsync.getFileListAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
        // return Task.FromResult<IDroneTelemetry>(GetTelemetryItem<DroneFileList>().Take().Telemetry);
    }

    Task<DroneFlyingState> Drone.IAsync.getFlyingStateAsync(CancellationToken cancellationToken)
    {
        var r = GetTelemetryItem<DroneFlyingState>();
        return Task.FromResult<DroneFlyingState>(r);
    }

    Task<DronePosition> Drone.IAsync.getPositionAsync(CancellationToken cancellationToken)
    {
        var r = GetTelemetryItem<DronePosition>();
        return Task.FromResult<DronePosition>(r);
    }

    Task<DroneRadioSignal> Drone.IAsync.getRadioSignalAsync(CancellationToken cancellationToken)
    {
        var r = GetTelemetryItem<DroneRadioSignal>();
        return Task.FromResult<DroneRadioSignal>(r);
    }

    Task<DroneLiveVideoStateResult> Drone.IAsync.getLiveVideoStateAsync(DroneSendLiveVideoRequest request, CancellationToken cancellationToken)
    {
        var r = GetTelemetryItem<DroneLiveVideoStateResult>();
        return Task.FromResult<DroneLiveVideoStateResult>(r);
    }

    Task<DroneReply> Drone.IAsync.pingServiceAsync(CancellationToken cancellationToken)
    {
        var r = GetTelemetryItem<DroneReply>();
        return Task.FromResult<DroneReply>(r);
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
}