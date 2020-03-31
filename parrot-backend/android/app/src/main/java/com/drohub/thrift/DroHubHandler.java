package com.drohub.thrift;

import android.content.Context;
import android.content.Intent;
import android.location.Location;
import android.media.projection.MediaProjectionManager;
import android.util.DisplayMetrics;
import android.util.Log;

import com.drohub.Janus.PeerConnectionParameters.PeerConnectionParameters;
import com.drohub.Janus.PeerConnectionParameters.PeerConnectionScreenShareParameters;
import org.apache.thrift.*;
import org.webrtc.EglBase;
import org.webrtc.PeerConnection;


import java.util.Arrays;
import java.util.List;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.ArrayBlockingQueue;

import com.drohub.GroundSdkActivityBase;
import com.drohub.Janus.PeerConnectionClient;
import com.drohub.R;
import com.drohub.thift.gen.*;
import com.parrot.drone.groundsdk.device.instrument.BatteryInfo;
import com.parrot.drone.groundsdk.device.instrument.FlyingIndicators;
import com.parrot.drone.groundsdk.device.instrument.Gps;
import com.parrot.drone.groundsdk.device.instrument.Radio;
import com.parrot.drone.groundsdk.device.peripheral.MainCamera;
import com.parrot.drone.groundsdk.device.peripheral.MediaStore;
import com.parrot.drone.groundsdk.device.peripheral.media.MediaItem;
import com.parrot.drone.groundsdk.device.pilotingitf.ManualCopterPilotingItf;


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
    private static final int _CAPTURE_PERMISSION_REQUEST_CODE = 1;
    
    private String _serial_number;
    final private TelemetryContainer<DronePosition> _drone_position;
    final private TelemetryContainer<DroneBatteryLevel> _drone_battery_level;
    final private TelemetryContainer<DroneFlyingState> _drone_flying_state;
    final private TelemetryContainer<DroneRadioSignal> _drone_radio_signal;

    private GroundSdkActivityBase _activity;
    private PeerConnectionClient _peerConnectionClient;
    private DroneLiveVideoState _video_state;
    private MediaStore _drone_media_store;
    PeerConnectionParameters _peer_connection_parameters;
    private long _room_id;
    private com.parrot.drone.groundsdk.device.Drone _drone_handle;

    public DroHubHandler(String serial, PeerConnectionParameters connection_parameters,
                         GroundSdkActivityBase activity) {
        _serial_number = serial;
        _drone_position = new TelemetryContainer<>();
        _drone_battery_level = new TelemetryContainer<>();
        _drone_flying_state = new TelemetryContainer<>();
        _drone_radio_signal = new TelemetryContainer<>();
        _room_id = 0;

        _activity = activity;
        _peer_connection_parameters = connection_parameters;
        _video_state = DroneLiveVideoState.INVALID_CONDITION;
        _drone_handle = _activity.getDroneHandle().getDrone(_serial_number);
        if (_drone_handle == null)
            throw new RuntimeException("Could not retrieve drone handle");

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

        _drone_media_store = _drone_handle.getPeripheral(MediaStore.class, media_store -> {

        }).get();

    }

    void handleCapturePermissionCallback(int requestCode, int resultCode, Intent data) {
        if (requestCode == _CAPTURE_PERMISSION_REQUEST_CODE && _video_state == DroneLiveVideoState.STOPPED) {
            ((PeerConnectionScreenShareParameters)_peer_connection_parameters).setPermissionData(data);
            ((PeerConnectionScreenShareParameters)_peer_connection_parameters).setPermissionResultCode(resultCode);
            initVideo();
        }
    }

    private void initVideo() {
        try {
            EglBase rootEglBase = EglBase.create();
            _peerConnectionClient = new PeerConnectionClient(_room_id, _serial_number,
                    _activity, rootEglBase.getEglBaseContext(),
                    _peer_connection_parameters,  null, null);
            _video_state = DroneLiveVideoState.LIVE;
        }
        catch (Exception e) {
            Log.e(TAG, Arrays.toString(e.getStackTrace()));
            _video_state = DroneLiveVideoState.DIED;
            _activity.alertBox(e.getMessage());
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

    private DroneLiveVideoStateResult setupScreenSharing() {
        MediaProjectionManager mediaProjectionManager =
                (MediaProjectionManager) _activity.getApplication().getSystemService(
                        Context.MEDIA_PROJECTION_SERVICE);

        if (mediaProjectionManager == null) {
            _video_state = DroneLiveVideoState.INVALID_CONDITION;
            return new DroneLiveVideoStateResult(_video_state, _serial_number,
                    System.currentTimeMillis());
        }
        _activity.startActivityForResult(
                mediaProjectionManager.createScreenCaptureIntent(), _CAPTURE_PERMISSION_REQUEST_CODE);

        //TODO: We need to notify that if he does not accept the permissions he will not be
        //able to broadcast the video...THis requirement may change if we stop using screen capture.
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
        if (_peer_connection_parameters.capturerType == PeerConnectionParameters.VideoCapturerType.SCREEN_SHARE)
            return setupScreenSharing();

        throw new org.apache.thrift.TApplicationException(org.apache.thrift.TApplicationException.MISSING_RESULT, "sendLiveVideoTo failed: No recognized video capturer");
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
    public DroneReply moveToPosition(DroneRequestPosition request) {
        return null;
    }

    private static FileResourceType convertToFileResourceType(MediaItem.Type type) throws TException {
        switch (type) {
            case PHOTO:
                return FileResourceType.IMAGE;
            case VIDEO:
                return FileResourceType.VIDEO;
        }
        throw new TException("Cannot convert to File Resource Type");
    }

    @Override
    public DroneFileList getFileList() throws TException {
        Log.e(TAG, "Start fle list");
        List<MediaItem> media_items = _drone_media_store.browse(obj -> {}).get();

        if (media_items == null)
            throw new TException("An error occurred retrieving the file list");
        Log.e(TAG, "No null");
        DroneFileList list_to_send = new DroneFileList();

        for (MediaItem item : media_items) {
            FileEntry file = new FileEntry(item.getName(),
                    convertToFileResourceType(item.getType()));

            list_to_send.file_entries.add(file);

        }
        list_to_send.setFile_entriesIsSet(true);
        list_to_send.setSerial(_serial_number);
        list_to_send.setTimestamp(System.currentTimeMillis());
        return list_to_send;
    }

    @Override
    public DroneReply recordVideo(DroneRecordVideoRequest request) throws TException {
        MainCamera camera_handle = _drone_handle.getPeripheral(MainCamera.class, camera -> {}).get();
        if (camera_handle == null)
            throw new TException("An error occurred retrieving the file list");

        boolean action_result = false;
        switch (request.action_type) {
            case START:
                camera_handle.startRecording();
                switch (camera_handle.recordingState().get()) {
                    case STARTED:
                    case STARTING:
                        action_result = true;
                        break;
                }
                break;
            case STOP:
                camera_handle.stopRecording();
                switch (camera_handle.recordingState().get()) {
                    case STOPPED:
                    case STOPPING:
                        action_result = true;
                }
        }
        return new DroneReply(action_result, _serial_number, System.currentTimeMillis());
    }

    @Override
    public DroneReply takePicture(DroneTakePictureRequest request) throws TException {
        MainCamera camera_handle = _drone_handle.getPeripheral(MainCamera.class, camera -> {}).get();
        if (camera_handle == null)
            throw new TException("An error occurred retrieving the file list");

        boolean action_result = false;
        switch (request.action_type) {
            case START:
                camera_handle.startPhotoCapture();
                switch (camera_handle.recordingState().get()) {
                    case STARTED:
                    case STARTING:
                        action_result = true;
                        break;
                }
                break;
            case STOP:
                camera_handle.stopPhotoCapture();
                switch (camera_handle.photoState().get()) {
                    case STOPPED:
                    case STOPPING:
                        action_result = true;
                }
        }
        return new DroneReply(action_result, _serial_number, System.currentTimeMillis());
    }
}