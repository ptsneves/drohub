package com.drohub.Janus;

import org.webrtc.PeerConnection;

import java.math.BigInteger;

public class JanusConnection {
    public enum ConnectionType {
        REMOTE,
        LOCAL
    };
    public BigInteger handleId;
    public PeerConnection peerConnection;
    public SDPObserver sdpObserver;
    public ConnectionType type;
}
