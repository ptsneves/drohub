package com.drohub.Janus.PeerConnectionParameters;

import android.app.Activity;

public class PeerConnectionCameraParameters extends PeerConnectionParameters {
    public PeerConnectionCameraParameters(String turn_user_name,
                                          String turn_credential,
                                          String[] ice_servers,
                                          String janus_web_socket_uri,
                                          Activity activity,
                                          int videoWidth, int videoHeight, int videoFps,
                                          String videoCodec, int videoStartBitrate,
                                          int audioStartBitrate, String audioCodec,
                                          boolean noAudioProcessing) {
        super(turn_user_name, turn_credential, ice_servers, janus_web_socket_uri, activity, videoWidth, videoHeight, videoFps, videoCodec,
                videoStartBitrate, VideoCapturerType.CAMERA_FRONT, audioStartBitrate,
                audioCodec, noAudioProcessing);
    }
}
