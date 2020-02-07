package com.drohub.Janus.PeerConnectionParameters;

import android.app.Activity;

import org.webrtc.PeerConnection;

import java.util.List;

public class PeerConnectionCameraParameters extends PeerConnectionParameters {
    public PeerConnectionCameraParameters(String janus_web_socket_uri,
                                          Activity activity,
                                          List<PeerConnection.IceServer> iceServers,
                                          int videoWidth, int videoHeight, int videoFps,
                                          String videoCodec, int videoStartBitrate,
                                          VideoCapturerType capturerType,
                                          int audioStartBitrate, String audioCodec,
                                          boolean noAudioProcessing) {
        super(janus_web_socket_uri, activity, iceServers, videoWidth, videoHeight, videoFps, videoCodec,
                videoStartBitrate, capturerType, audioStartBitrate,
                audioCodec, noAudioProcessing);
    }
}
