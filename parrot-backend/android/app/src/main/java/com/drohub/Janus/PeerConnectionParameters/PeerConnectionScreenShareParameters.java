package com.drohub.Janus.PeerConnectionParameters;

import android.app.Activity;
import android.content.Intent;

import org.webrtc.PeerConnection;

import java.util.List;

public class PeerConnectionScreenShareParameters extends PeerConnectionParameters {
    final public Intent permission_data;
    final public int permission_result_code;

    public PeerConnectionScreenShareParameters(String janus_web_socket_uri,
                                               Activity activity,
                                               List<PeerConnection.IceServer> iceServers,
                                               int videoWidth, int videoHeight, int videoFps,
                                               String videoCodec,
                                               int videoStartBitrate,
                                               int audioStartBitrate, String audioCodec,
                                               boolean noAudioProcessing,
                                               Intent permission_data,
                                               int permission_result_code) {

        super(janus_web_socket_uri, activity, iceServers, videoWidth, videoHeight, videoFps, videoCodec,
                videoStartBitrate, VideoCapturerType.SCREEN_SHARE, audioStartBitrate, audioCodec,
                noAudioProcessing);

        this.permission_data = permission_data;
        this.permission_result_code = permission_result_code;
    }
}
