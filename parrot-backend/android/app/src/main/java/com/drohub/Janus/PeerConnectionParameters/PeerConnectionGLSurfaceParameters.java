package com.drohub.Janus.PeerConnectionParameters;

import android.app.Activity;
import com.drohub.hud.LiveVideoRenderer;
import com.parrot.drone.groundsdk.device.peripheral.StreamServer;
import org.webrtc.SurfaceViewRenderer;

public class PeerConnectionGLSurfaceParameters extends PeerConnectionParameters {
    public StreamServer LiveVideoStreamServer;
    public PeerConnectionGLSurfaceParameters(SurfaceViewRenderer local_view,
                                             SurfaceViewRenderer remote_view,
                                             String turn_user_name,
                                             String turn_credential,
                                             String[] ice_servers,
                                             String janus_web_socket_uri,
                                             Activity activity, int videoFps,
                                             String videoCodec, int video_width, int video_height, int videoStartBitrate,
                                             int audioStartBitrate, String audioCodec,
                                             StreamServer live_video_stream_server,
                                             boolean noAudioProcessing) {
        super(local_view, remote_view, turn_user_name, turn_credential, ice_servers, janus_web_socket_uri, activity,
                video_width,
                video_height, videoFps, videoCodec,
                videoStartBitrate, VideoCapturerType.GROUNDSDK_VIDEO_SHARE, audioStartBitrate,
                audioCodec, noAudioProcessing);
        LiveVideoStreamServer = live_video_stream_server;
    }
}
