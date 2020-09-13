package com.drohub.Janus.PeerConnectionParameters;

import android.app.Activity;
import com.parrot.drone.groundsdk.device.peripheral.StreamServer;
import org.webrtc.SurfaceViewRenderer;

public class PeerConnectionParrotStreamParameters extends PeerConnectionParameters {
    public StreamServer LiveVideoStreamServer;
    public PeerConnectionParrotStreamParameters(SurfaceViewRenderer local_view,
                                                SurfaceViewRenderer remote_view,
                                                String turn_user_name,
                                                String turn_credential,
                                                String[] ice_servers,
                                                String janus_web_socket_uri,
                                                Activity activity, int videoFps,
                                                String videoCodec, int video_width, int video_height, int videoStartBitrate,
                                                int audioStartBitrate, String audioCodec,
                                                StreamServer live_video_stream_server,
                                                float keepAliveFrequency, long keepAliveTimeout
                                             ) {
        super(local_view, remote_view, turn_user_name, turn_credential, ice_servers, janus_web_socket_uri, activity,
                video_width,
                video_height, videoFps, videoCodec,
                videoStartBitrate, VideoCapturerType.GROUNDSDK_VIDEO_SHARE, audioStartBitrate,
                audioCodec, keepAliveFrequency, keepAliveTimeout);
         LiveVideoStreamServer = live_video_stream_server;
     }

    public PeerConnectionParrotStreamParameters(PeerConnectionParameters p, StreamServer live_video_stream_server) {
        super(p);
        LiveVideoStreamServer = live_video_stream_server;
    }
}
