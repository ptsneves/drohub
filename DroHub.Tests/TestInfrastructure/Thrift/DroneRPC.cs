using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
public class DroneRPC : Drone.IAsync, IDisposable
{
    public BlockingCollection<DroneReply> LandingReply {get; private set; }
    public BlockingCollection<DroneReply> ReturnToHomeReply {get; private set; }
    public BlockingCollection<DroneReply> TakeOffReply {get; private set; }
    public BlockingCollection<DronePosition> PositionReply { get; private set; }
    public BlockingCollection<DroneBatteryLevel> BatteryLevelReply {get; private set; }
    public BlockingCollection<DroneFileList> FileListReply {get; private set; }
    public BlockingCollection<DroneFlyingState> FlyingStateReply {get; private set; }
    public BlockingCollection<DroneRadioSignal> RadioSignalReply {get; private set; }
    public BlockingCollection<DroneLiveVideoStateResult> VideoStateResultReply {get; private set; }
    public BlockingCollection<DroneReply> MoveToPositionReply {get; private set; }
    public BlockingCollection<DroneReply> PingServiceReply {get; private set; }
    public BlockingCollection<DroneLiveVideoStateResult> SendVideoStateReply {get ; private set; }
    private bool disposed = false;

    public DroneRPC() {
        LandingReply = new BlockingCollection<DroneReply>();
        ReturnToHomeReply = new BlockingCollection<DroneReply>();
        TakeOffReply = new BlockingCollection<DroneReply>();
        PositionReply = new BlockingCollection<DronePosition>();
        BatteryLevelReply = new BlockingCollection<DroneBatteryLevel>();
        FileListReply = new BlockingCollection<DroneFileList>();
        FlyingStateReply = new BlockingCollection<DroneFlyingState>();
        RadioSignalReply = new BlockingCollection<DroneRadioSignal>();
        VideoStateResultReply = new BlockingCollection<DroneLiveVideoStateResult>();
        MoveToPositionReply = new BlockingCollection<DroneReply>();
        PingServiceReply = new BlockingCollection<DroneReply>();
        SendVideoStateReply = new BlockingCollection<DroneLiveVideoStateResult>();
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
            LandingReply?.Dispose();
            ReturnToHomeReply?.Dispose();
            TakeOffReply?.Dispose();
            PositionReply?.Dispose();
            BatteryLevelReply?.Dispose();
            FileListReply?.Dispose();
            FlyingStateReply?.Dispose();
            RadioSignalReply?.Dispose();
            VideoStateResultReply?.Dispose();
            MoveToPositionReply?.Dispose();
            PingServiceReply?.Dispose();
            SendVideoStateReply?.Dispose();
        }
        disposed = true;
    }
    Task<DroneReply> Drone.IAsync.doLandingAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<DroneReply>(LandingReply.Take());
    }

    Task<DroneReply> Drone.IAsync.doReturnToHomeAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<DroneReply>(ReturnToHomeReply.Take());
    }

    Task<DroneReply> Drone.IAsync.doTakeoffAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<DroneReply>(TakeOffReply.Take());
    }

    Task<DroneBatteryLevel> Drone.IAsync.getBatteryLevelAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<DroneBatteryLevel>(BatteryLevelReply.Take());
    }

    Task<DroneFileList> Drone.IAsync.getFileListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<DroneFileList>(FileListReply.Take());
    }

    Task<DroneFlyingState> Drone.IAsync.getFlyingStateAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<DroneFlyingState>(FlyingStateReply.Take());
    }

    Task<DronePosition> Drone.IAsync.getPositionAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<DronePosition>(PositionReply.Take());
    }

    Task<DroneRadioSignal> Drone.IAsync.getRadioSignalAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<DroneRadioSignal>(RadioSignalReply.Take());
    }

    Task<DroneLiveVideoStateResult> Drone.IAsync.getLiveVideoStateAsync(DroneSendLiveVideoRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult<DroneLiveVideoStateResult>(VideoStateResultReply.Take());
    }

    Task<DroneReply> Drone.IAsync.moveToPositionAsync(DroneRequestPosition request, CancellationToken cancellationToken)
    {
        return Task.FromResult<DroneReply>(MoveToPositionReply.Take());
    }

    Task<DroneReply> Drone.IAsync.pingServiceAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<DroneReply>(PingServiceReply.Take());
    }

    Task<DroneLiveVideoStateResult> Drone.IAsync.sendLiveVideoToAsync(DroneSendLiveVideoRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult<DroneLiveVideoStateResult>(SendVideoStateReply.Take());
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