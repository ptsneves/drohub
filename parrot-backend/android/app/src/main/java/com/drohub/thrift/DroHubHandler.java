package com.drohub.thrift;

import android.content.Context;
import android.content.Intent;
import android.location.Location;
import android.media.projection.MediaProjectionManager;
import android.util.DisplayMetrics;
import android.util.Log;

import org.apache.thrift.*;
import org.webrtc.EglBase;
import org.webrtc.PeerConnection;


import java.util.Arrays;
import java.util.List;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.ArrayBlockingQueue;

import com.drohub.GroundSdkActivityBase;
import com.drohub.Janus.PeerConnectionClient;
import com.drohub.Janus.PeerConnectionParameters.PeerConnectionScreenShareParameters;
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

import java.util.Date;

public class DroHubHandler implements Drone.Iface {
    public class TelemetryContainer<TelemetryType>  {
        private static final int QUEUE_CAPACITY = 1024;
        private BlockingQueue<TelemetryType> _q;
        TelemetryContainer() {
            _q = new ArrayBlockingQueue<>(QUEUE_CAPACITY);
        }
        public TelemetryType pop() throws TException {
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
        public void push(TelemetryType t) throws TException {
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
    final public TelemetryContainer<DronePosition> _drone_position;
    final public TelemetryContainer<DroneBatteryLevel> _drone_battery_level;
    final public TelemetryContainer<DroneFlyingState> _drone_flying_state;
    final public TelemetryContainer<DroneRadioSignal> _drone_radio_signal;


    private GroundSdkActivityBase _activity;
    private String _janus_websocket_uri;
    private PeerConnection.IceServer[] _turn_servers;
    private PeerConnectionClient _peerConnectionClient;
    private DroneLiveVideoState _video_state;
    private MediaStore _drone_media_store;
    private long _room_id;
    private com.parrot.drone.groundsdk.device.Drone _drone_handle;
    private Date _date_handle;

    public DroHubHandler(String serial, String janus_websocket_uri, GroundSdkActivityBase activity) {
        _serial_number = serial;
        _drone_position = new TelemetryContainer<>();
        _drone_battery_level = new TelemetryContainer<>();
        _drone_flying_state = new TelemetryContainer<>();
        _drone_radio_signal = new TelemetryContainer<>();
        _room_id = 0;

        _activity = activity;
        _janus_websocket_uri = janus_websocket_uri;
        _video_state = DroneLiveVideoState.INVALID_CONDITION;
        _drone_handle = _activity.getDroneHandle().getDrone(_serial_number);
        _date_handle = new Date();


        String[] res_turn_urls = _activity.getResources().getStringArray(R.array.turn_servers);
        _turn_servers = new PeerConnection.IceServer[res_turn_urls.length];
        for (int i = 0; i < _turn_servers.length; i++) {
            _turn_servers[i] =  PeerConnection.IceServer
                    .builder(res_turn_urls[i])
                    .setUsername(_activity.getResources().getString(R.string.turn_user_name))
                    .setPassword(_activity.getResources().getString(R.string.turn_credential))
                    .createIceServer();
        }


        _drone_handle.getInstrument(FlyingIndicators.class, flying_indicators -> {
            if (flying_indicators == null)
                return;


            DroneFlyingState state_to_send = null;

            switch (flying_indicators.getState()) {
                case LANDED:
                    switch (flying_indicators.getLandedState()) {
                        case MOTOR_RAMPING:
                            state_to_send = new DroneFlyingState(FlyingState.MOTOR_RAMPING,
                                    _serial_number, _date_handle.getTime());
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
                                    _serial_number, _date_handle.getTime());
                            break;
                        case LANDING:
                            state_to_send = new DroneFlyingState(FlyingState.LANDING,
                                    _serial_number, _date_handle.getTime());
                            break;
                        case FLYING:
                            state_to_send = new DroneFlyingState(FlyingState.FLYING,
                                    _serial_number, _date_handle.getTime());
                            break;
                        case NONE:
                        case WAITING:
                            break;
                    }
                    break;
                case EMERGENCY_LANDING:
                    state_to_send = new DroneFlyingState(FlyingState.EMERGENCY_LANDING,
                            _serial_number, _date_handle.getTime());
                    break;
                case EMERGENCY:
                    state_to_send = new DroneFlyingState(FlyingState.EMERGENCY,
                            _serial_number, _date_handle.getTime());
                    break;
            }

            if (state_to_send != null) {
                try {
                    _drone_flying_state.push(state_to_send);
                } catch (TException e) {
                    ; // TODO: Think about this
                }
            }
        });

        _drone_handle.getInstrument(BatteryInfo .class, battery_info -> {
            try {
                _drone_battery_level.push(
                        new DroneBatteryLevel(
                                battery_info.getBatteryLevel(),
                                _serial_number, _date_handle.getTime()
                        )
                );
            } catch (TException e) {
                ;
            }
        });

        _drone_handle.getInstrument(Gps.class, gps -> {
            if (gps == null)
                return;

            Location location = gps.lastKnownLocation();

            DronePosition position_to_send = new DronePosition(location.getLatitude(),
                    location.getLongitude(),
                    location.getAltitude(),
                    _serial_number,
                    _date_handle.getTime());

            try {
                _drone_position.push(position_to_send);
            } catch (TException e) {
                ; // TODO: Think about this
            }
        });

        _drone_handle.getInstrument(Radio.class, radio_signal -> {
           if (radio_signal == null)
                return;

           DroneRadioSignal radio_signal_to_send = new DroneRadioSignal(
                   _serial_number,
                   _date_handle.getTime());

            radio_signal_to_send.signal_quality = radio_signal.getLinkSignalQuality();
            radio_signal_to_send.rssi = radio_signal.getRssi();
            try {
                _drone_radio_signal.push(radio_signal_to_send);
            } catch (TException e) {
                ; // TODO: Think about this
            }
        });

        _drone_media_store = _drone_handle.getPeripheral(MediaStore.class, media_store -> {
            ;
        }).get();

    }



    public void handleCapturePermissionCallback(int requestCode, int resultCode, Intent data) {
        if (requestCode == _CAPTURE_PERMISSION_REQUEST_CODE && _video_state == DroneLiveVideoState.STOPPED) {
            initVideo(data, resultCode);
            _video_state = DroneLiveVideoState.LIVE;
        }
    }

    private void initVideo(Intent permission_data, int permission_code) {
        try {
            EglBase rootEglBase = EglBase.create();
            DisplayMetrics metrics = _activity.getResources().getDisplayMetrics();
            PeerConnectionScreenShareParameters peerConnectionParameters = new PeerConnectionScreenShareParameters(
                    _janus_websocket_uri, _activity,
                    Arrays.asList(_turn_servers),
                    metrics.widthPixels, metrics.heightPixels, 20,
                    "VP8",
                    120, null, false,
                    permission_data,
                    permission_code);

            _peerConnectionClient = new PeerConnectionClient(_room_id, _serial_number,
                    _activity, rootEglBase.getEglBaseContext(),
                    peerConnectionParameters,  null, null);

        }
        catch (Exception e) {
            Log.e(TAG, e.getStackTrace().toString());
            _video_state = DroneLiveVideoState.DIED;
            _activity.alertBox(e.getMessage());
        }
    }

    @Override
    public DroneReply pingService() throws TException {
        return new DroneReply(true, _serial_number, _date_handle.getTime());
    }

    @Override
    public DroneLiveVideoStateResult getLiveVideoState(DroneSendLiveVideoRequest request) throws TException {
        return new DroneLiveVideoStateResult(_video_state, _serial_number, (new Date()).getTime());
    }

    @Override
    public DroneLiveVideoStateResult sendLiveVideoTo(DroneSendLiveVideoRequest request) throws TException {
        _video_state = DroneLiveVideoState.STOPPED;
        _room_id = request.room_id;
        MediaProjectionManager mediaProjectionManager =
                (MediaProjectionManager) _activity.getApplication().getSystemService(
                        Context.MEDIA_PROJECTION_SERVICE);

        _activity.startActivityForResult(
                mediaProjectionManager.createScreenCaptureIntent(), _CAPTURE_PERMISSION_REQUEST_CODE);
        //TODO: We need to notify that if he does not accept the permissions he will not be
        //able to broadcast the video...THis requirement may change if we stop using screen capture.
        return new DroneLiveVideoStateResult(_video_state, _serial_number,
                (new Date()).getTime());
    }

    @Override
    public DroneReply doTakeoff() {
        _drone_handle.getPilotingItf(ManualCopterPilotingItf.class, pilotingItf -> {
            if (pilotingItf == null) {
                return;
            }
            pilotingItf.takeOff();
        });
        return new DroneReply(true, _serial_number, _date_handle.getTime());
    }

    @Override
    public DroneReply doLanding() {
        _drone_handle.getPilotingItf(ManualCopterPilotingItf.class, pilotingItf -> {
            if (pilotingItf == null) {
                return;
            }
            pilotingItf.land();
        });
        return new DroneReply(true, _serial_number, _date_handle.getTime());
    }

    @Override
    public DroneReply doReturnToHome() throws TException {
        return new DroneReply(false, _serial_number, _date_handle.getTime());
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
    public DroneReply moveToPosition(DroneRequestPosition request) throws TException {
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
        List<MediaItem> media_items = _drone_media_store.browse(obj -> {}).get();

        if (media_items == null)
            throw new TException("An error occurred retrieving the file list");

        DroneFileList list_to_send = new DroneFileList();

        for (MediaItem item : media_items) {
            FileEntry file = new FileEntry(item.getName(),
                    convertToFileResourceType(item.getType()));

            list_to_send.file_entries.add(file);

        }
        list_to_send.serial = _serial_number;
        list_to_send.timestamp = _date_handle.getTime();
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
        return new DroneReply(action_result, _serial_number, _date_handle.getTime());
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
        return new DroneReply(action_result, _serial_number, _date_handle.getTime());
    }
}