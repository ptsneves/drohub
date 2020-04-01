package com.drohub.Janus.PeerConnectionParameters;

import android.app.Activity;
import android.content.Intent;
import org.webrtc.SurfaceViewRenderer;

public class PeerConnectionScreenShareParameters extends PeerConnectionParameters {
    public class InvalidScreenPermissions extends Exception {
    }
    private Intent _permission_data;
    private boolean _is_permission_data_set;

    private int _permission_result_code;
    private boolean _is_permission_result_code_set;

    public PeerConnectionScreenShareParameters(SurfaceViewRenderer local_view,
                                               SurfaceViewRenderer remote_view,
                                               String turn_user_name,
                                               String turn_credential,
                                               String[] ice_servers,
                                               String janus_web_socket_uri,
                                               Activity activity,
                                               int videoFps,
                                               String videoCodec,
                                               int videoStartBitrate,
                                               int audioStartBitrate, String audioCodec,
                                               boolean noAudioProcessing) {

        super(local_view, remote_view, turn_user_name, turn_credential, ice_servers, janus_web_socket_uri, activity,
                activity.getResources().getDisplayMetrics().widthPixels,
                activity.getResources().getDisplayMetrics().heightPixels, videoFps, videoCodec,
                videoStartBitrate, VideoCapturerType.SCREEN_SHARE, audioStartBitrate, audioCodec,
                noAudioProcessing);

        _is_permission_data_set = false;
        _is_permission_result_code_set = false;
    }

    private void validatePermissionsRead() throws InvalidScreenPermissions {
        if (!(_is_permission_data_set && _is_permission_result_code_set))
            throw new InvalidScreenPermissions();
    }

    public Intent getPermissionData() throws InvalidScreenPermissions {
            validatePermissionsRead();
            return _permission_data;
    }

    public void setPermissionData(Intent permission_data) {
        _permission_data = permission_data;
        _is_permission_data_set = true;
    }

    public int getPermissionResultCode() throws InvalidScreenPermissions {
        validatePermissionsRead();
        return _permission_result_code;
    }

    public void setPermissionResultCode(int permission_result_code) {
        _permission_result_code = permission_result_code;
        _is_permission_result_code_set = true;
    }
}
