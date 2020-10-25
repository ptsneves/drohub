package com.drohub.thrift;

import android.location.Location;
import android.util.Log;
import androidx.annotation.NonNull;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.Devices.Peripherals.Parrot.ParrotGimbal;
import com.drohub.GroundSdkHelperActivity;
import com.drohub.IInfoDisplay;
import com.drohub.Janus.PeerConnectionClient;
import com.drohub.Janus.PeerConnectionParameters;
import com.drohub.Devices.Peripherals.Parrot.ParrotMainCamera;
import com.drohub.thift.gen.*;
import com.parrot.drone.groundsdk.device.instrument.BatteryInfo;
import com.parrot.drone.groundsdk.device.instrument.FlyingIndicators;
import com.parrot.drone.groundsdk.device.instrument.Gps;
import com.parrot.drone.groundsdk.device.instrument.Radio;
import com.parrot.drone.groundsdk.device.peripheral.MediaStore;
import com.parrot.drone.groundsdk.device.peripheral.media.MediaItem;
import com.parrot.drone.groundsdk.device.pilotingitf.ManualCopterPilotingItf;
import org.apache.thrift.TApplicationException;
import org.apache.thrift.TException;
import org.jetbrains.annotations.NotNull;

import java.util.Arrays;
import java.util.List;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.BlockingQueue;


public class DroHubHandler implements Drone.Iface {
    public class TelemetryContainer<TelemetryType>  {
        private static final int QUEUE_CAPACITY = 1024;
        private BlockingQueue<TelemetryType> _q;
        TelemetryContainer() {
            _q = new ArrayBlockingQueue<>(QUEUE_CAPACITY);
        }
        TelemetryType pop() throws TException {
            try {
                System.out.println("Popping 1" + this.getClass().getName());
                TelemetryType ret = _q.take();
                _q.clear();
                return ret;
            } catch (InterruptedException e) {
                throw new TException(e.getMessage());
            }
            finally {
                System.out.println("Pop successful");
            }
        }
        void push(TelemetryType t) throws TException {
            try {
                _q.put(t);
            } catch (InterruptedException e) {
                throw new TException(e.getMessage());
            }
        }
    }
    private static final String TAG = "DroHubHandler";

    private String _serial_number;
    final private IInfoDisplay _display;
    final private TelemetryContainer<DronePosition> _drone_position;
    final private TelemetryContainer<DroneBatteryLevel> _drone_battery_level;
    final private TelemetryContainer<DroneFlyingState> _drone_flying_state;
    final private TelemetryContainer<DroneRadioSignal> _drone_radio_signal;
    final private TelemetryContainer<CameraState> _camera_state;
    final private TelemetryContainer<GimbalState> _gimbal_state;
    final private ParrotMainCamera _main_camera;
    final private ParrotGimbal _gimbal;

    private GroundSdkHelperActivity _activity;
    private PeerConnectionClient _peerConnectionClient;
    private DroneLiveVideoState _video_state;

    PeerConnectionParameters _peer_connection_parameters;
    private long _room_id;
    private com.parrot.drone.groundsdk.device.Drone _drone_handle;

    public DroHubHandler(String serial, PeerConnectionParameters connection_parameters,
                         GroundSdkHelperActivity activity,
                         IInfoDisplay display)  {

        _drone_handle = activity.getParrotSDKHandle().getDrone(serial);
        if (_drone_handle == null)
            throw new RuntimeException("Could not retrieve drone handle");


        _serial_number = serial;
        _drone_position = new TelemetryContainer<>();
        _drone_battery_level = new TelemetryContainer<>();
        _drone_flying_state = new TelemetryContainer<>();
        _drone_radio_signal = new TelemetryContainer<>();
        _camera_state = new TelemetryContainer<>();
        _gimbal_state = new TelemetryContainer<>();
        _main_camera = new ParrotMainCamera(_drone_handle);
        _gimbal = new ParrotGimbal(_drone_handle);

        _room_id = 0;

        _activity = activity;
        _peer_connection_parameters = connection_parameters;
        _video_state = DroneLiveVideoState.INVALID_CONDITION;

        _drone_handle.getInstrument(FlyingIndicators.class, flying_indicators -> {
            if (flying_indicators == null)
                return;


            DroneFlyingState state_to_send = null;

            switch (flying_indicators.getState()) {
                case LANDED:
                    switch (flying_indicators.getLandedState()) {
                        case MOTOR_RAMPING:
                            state_to_send = new DroneFlyingState(FlyingState.MOTOR_RAMPING,
                                    _serial_number, System.currentTimeMillis());
                            break;
                        case INITIALIZING:
                        case IDLE:
                        case NONE:
                        case WAITING_USER_ACTION:
                            break;
                    }
                case FLYING:
                    switch (flying_indicators.getFlyingState()) {
                        case TAKING_OFF:
                            state_to_send = new DroneFlyingState(FlyingState.TAKING_OFF,
                                    _serial_number, System.currentTimeMillis());
                            break;
                        case LANDING:
                            state_to_send = new DroneFlyingState(FlyingState.LANDING,
                                    _serial_number, System.currentTimeMillis());
                            break;
                        case FLYING:
                            state_to_send = new DroneFlyingState(FlyingState.FLYING,
                                    _serial_number, System.currentTimeMillis());
                            break;
                        case NONE:
                        case WAITING:
                            break;
                    }
                    break;
                case EMERGENCY_LANDING:
                    state_to_send = new DroneFlyingState(FlyingState.EMERGENCY_LANDING,
                            _serial_number, System.currentTimeMillis());
                    break;
                case EMERGENCY:
                    state_to_send = new DroneFlyingState(FlyingState.EMERGENCY,
                            _serial_number, System.currentTimeMillis());
                    break;
            }

            if (state_to_send != null) {
                try {
                    _drone_flying_state.push(state_to_send);
                } catch (TException e) {
                    // TODO: Think about this
                }
            }
        });

        _drone_handle.getInstrument(BatteryInfo .class, battery_info -> {
            if (battery_info == null)
                return;
            try {
                _drone_battery_level.push(
                        new DroneBatteryLevel(
                                battery_info.getBatteryLevel(),
                                _serial_number, System.currentTimeMillis()
                        )
                );
            } catch (TException e) {
                // TODO: Think about this
            }
        });

        _drone_handle.getInstrument(Gps.class, gps -> {
            if (gps == null)
                return;

            Location location = gps.lastKnownLocation();
            if (location == null)
                return;
            DronePosition position_to_send = new DronePosition(location.getLatitude(),
                    location.getLongitude(),
                    location.getAltitude(),
                    _serial_number,
                    System.currentTimeMillis());

            try {
                _drone_position.push(position_to_send);
            } catch (TException e) {
                // TODO: Think about this
            }
        });

        _drone_handle.getInstrument(Radio.class, radio_signal -> {
           if (radio_signal == null)
                return;

           DroneRadioSignal radio_signal_to_send = new DroneRadioSignal(
                   _serial_number,
                   System.currentTimeMillis());

            radio_signal_to_send.setSignal_quality(radio_signal.getLinkSignalQuality());
            radio_signal_to_send.setRssi(radio_signal.getRssi());

            try {
                _drone_radio_signal.push(radio_signal_to_send);
            } catch (TException e) {
                // TODO: Think about this
            }
        });

        _display = display;

        _main_camera.setCameraStateListener(camera_state -> {
            try {
                _camera_state.push(camera_state);
            } catch (TException e) {
                ;
            }
        });
        _main_camera.start();

        _gimbal.setGimbalStateListener(state -> {
            try {
                _gimbal_state.push(state);
            }
            catch (TException e) {
                ;
            }
        });
        _gimbal.start();
    }

    private void initVideo() {
        try {
            _peerConnectionClient = new PeerConnectionClient(_room_id, _serial_number,
                    _activity,
                    _display,
                    _peer_connection_parameters);
            _video_state = DroneLiveVideoState.LIVE;
        }
        catch (Exception e) {
            Log.e(TAG, Arrays.toString(e.getStackTrace()));
            _video_state = DroneLiveVideoState.DIED;
            _display.add(e.getMessage());
        }
    }

    @Override
    public DroneReply pingService() {
        return new DroneReply(true, _serial_number, System.currentTimeMillis());
    }

    @Override
    public DroneLiveVideoStateResult getLiveVideoState(DroneSendLiveVideoRequest request) {
        System.out.println(_video_state);
        return new DroneLiveVideoStateResult(_video_state, _serial_number,
                System.currentTimeMillis());
    }

    @Override
    public DroneLiveVideoStateResult sendLiveVideoTo(DroneSendLiveVideoRequest request) throws TApplicationException {
        if (_video_state != DroneLiveVideoState.INVALID_CONDITION) {
            return new DroneLiveVideoStateResult(_video_state, _serial_number,
                    System.currentTimeMillis());
        }
        _video_state = DroneLiveVideoState.STOPPED;
        _room_id = request.room_id;
        initVideo();
        return new DroneLiveVideoStateResult(_video_state, _serial_number, System.currentTimeMillis());
    }

    @Override
    public DroneReply doTakeoff() {
        _drone_handle.getPilotingItf(ManualCopterPilotingItf.class, pilotingItf -> {
            if (pilotingItf == null) {
                return;
            }
            pilotingItf.takeOff();
        });
        return new DroneReply(true, _serial_number, System.currentTimeMillis());
    }

    @Override
    public DroneReply doLanding() {
        _drone_handle.getPilotingItf(ManualCopterPilotingItf.class, pilotingItf -> {
            if (pilotingItf == null) {
                return;
            }
            pilotingItf.land();
        });
        return new DroneReply(true, _serial_number, System.currentTimeMillis());
    }

    @Override
    public DroneReply doReturnToHome() {
        return new DroneReply(false, _serial_number, System.currentTimeMillis());
    }


    @Override
    public DronePosition getPosition() throws TException {
        return _drone_position.pop();
    }


    @Override
    public DroneBatteryLevel getBatteryLevel() throws TException {
        return _drone_battery_level.pop();
    }


    @Override
    public DroneFlyingState getFlyingState() throws TException {
        return _drone_flying_state.pop();
    }


    @Override
    public DroneRadioSignal getRadioSignal() throws TException {
        return _drone_radio_signal.pop();
    }

    @Override
    public CameraState getCameraState() throws TException {
        return _camera_state.pop();
    }

    @Override
    public GimbalState getGimbalState() throws TException {
        return _gimbal_state.pop();
    }

    @Override
    public DroneReply moveToPosition(DroneRequestPosition request) {
        return null;
    }

    @Override
    public DroneReply recordVideo(DroneRecordVideoRequest request) {
        boolean r = _main_camera.recordVideo(request.action_type == ActionType.START);
        return new DroneReply(r, _serial_number, System.currentTimeMillis());
    }

    @Override
    public DroneReply setCameraZoom(double zoom_level) {
        ParrotMainCamera.ZoomResult zr = _main_camera.setZoom(zoom_level);
        boolean r = zr == ParrotMainCamera.ZoomResult.GOOD ? true : false;
        return new DroneReply(r, _serial_number, System.currentTimeMillis());
    }

    @Override
    public DroneReply setGimbalAttitude(double pitch, double roll, double yaw) {
        boolean r =  _gimbal.setAttitude(pitch, roll, yaw);
        return new DroneReply(r, _serial_number, System.currentTimeMillis());
    }

    @Override
    public DroneReply takePicture(DroneTakePictureRequest request) {
        boolean r = _main_camera.triggerPhotoPicture(request.action_type == ActionType.START);
        return new DroneReply(r, _serial_number, System.currentTimeMillis());
    }
    
    public boolean setMicrophoneMute(boolean enabled) {
        if (_peerConnectionClient == null)
            return false;

        return _peerConnectionClient.setMicrophoneMute(enabled);
    }

    public void onStop() {
        if (_peerConnectionClient == null)
            return;

        _peerConnectionClient.onStop();
    }
}
