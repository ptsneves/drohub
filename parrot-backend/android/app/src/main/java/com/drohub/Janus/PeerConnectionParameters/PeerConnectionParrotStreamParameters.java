package com.drohub.Janus.PeerConnectionParameters;

import com.parrot.drone.groundsdk.device.peripheral.StreamServer;

public class PeerConnectionParrotStreamParameters extends PeerConnectionParameters {
    public StreamServer LiveVideoStreamServer;

    public PeerConnectionParrotStreamParameters(PeerConnectionParameters p, StreamServer live_video_stream_server) {
        super(p);
        LiveVideoStreamServer = live_video_stream_server;
    }
}
