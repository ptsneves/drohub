package com.drohub.thrift;

import android.content.Context;
import android.content.Intent;
import android.media.projection.MediaProjectionManager;
import android.util.Log;

import org.apache.thrift.*;
import org.webrtc.EglBase;

import java.math.BigInteger;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.ArrayBlockingQueue;

import com.drohub.GroundSdkActivityBase;
import com.drohub.Janus.PeerConnectionClient;
import com.drohub.Janus.PeerConnectionParameters.PeerConnectionScreenShareParameters;
import com.drohub.thift.gen.*;
import java.util.Date;

public class DroHubHandler implements Drone.Iface {
    public class TelemetryContainer<TelemetryType>  {
        private static final int QUEUE_CAPACITY = 4;
        private BlockingQueue<TelemetryType> _q;
        TelemetryContainer() {
            _q = new ArrayBlockingQueue<>(QUEUE_CAPACITY);
        }
        public TelemetryType pop() throws TException {
            try {
                System.out.println("Popping 1" + this.getClass().getName());
                return _q.take();
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
    final public TelemetryContainer<DroneFileList> _drone_file_list;


    private GroundSdkActivityBase _activity;
    private String _janus_websocket_uri;
    private PeerConnectionClient _peerConnectionClient;
    private DroneLiveVideoState _video_state;
    private long _room_id;

    public DroHubHandler(String serial, String janus_websocket_uri, GroundSdkActivityBase activity) {
        _serial_number = serial;
        _drone_position = new TelemetryContainer<>();
        _drone_battery_level = new TelemetryContainer<>();
        _drone_flying_state = new TelemetryContainer<>();
        _drone_radio_signal = new TelemetryContainer<>();
        _drone_file_list = new TelemetryContainer<>();
        _room_id = 0;

        _activity = activity;
        _janus_websocket_uri = janus_websocket_uri;
        _video_state = DroneLiveVideoState.INVALID_CONDITION;
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
            PeerConnectionScreenShareParameters peerConnectionParameters = new PeerConnectionScreenShareParameters(
                    _janus_websocket_uri, _activity, 2340, 1080, 30,
                    "H264",
                    120, "opus", false,
                    permission_data,
                    permission_code);

            _peerConnectionClient = new PeerConnectionClient(_room_id, _serial_number,
                    _activity, rootEglBase.getEglBaseContext(),
                    peerConnectionParameters,  null, null);

        }
        catch (Exception e) {
            Log.e(TAG, e.getStackTrace().toString());
            _activity.alertBox(e.getMessage());
        }
    }

    @Override
    public DroneReply pingService() throws TException {
        Date date = new Date();
        return new DroneReply(true, _serial_number, date.getTime());
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

        return new DroneLiveVideoStateResult(DroneLiveVideoState.STOPPED, _serial_number,
                (new Date()).getTime());
    }

    @Override
    public DroneReply doTakeoff() throws TException {
        return null;
    }

    @Override
    public DroneReply doLanding() throws TException {
        return null;
    }

    @Override
    public DroneReply doReturnToHome() throws TException {
        return null;
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


    @Override
    public DroneFileList getFileList() throws TException {
        return _drone_file_list.pop();
    }

    @Override
    public DroneReply recordVideo(DroneRecordVideoRequest request) throws TException {
        return null;
    }

    @Override
    public DroneReply takePicture(DroneTakePictureRequest request) throws TException {
        return null;
    }
}