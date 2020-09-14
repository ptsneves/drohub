package com.drohub.Janus;

import android.app.Activity;
import org.webrtc.*;

public class PeerConnectionParameters {
  public interface CapturerGenerator {
    VideoCapturer getCapturer(EglBase.Context egl_context, CapturerObserver observer);
  }
  public final PeerConnection.IceServer[] iceServers;
  public final CapturerGenerator capturer_generator;
  public final String janusWebSocketURL;
  public final String videoCodec;
  public final int audioStartBitrate;
  public final int videoStartBitrate;
  public final String audioCodec;
  public final Activity activity;
  public final SurfaceViewRenderer localView;
  public final SurfaceViewRenderer remoteView;
  public final float keepAliveFrequency;
  public final long keepAliveTimeout;

  public PeerConnectionParameters(
          SurfaceViewRenderer local_view,
          SurfaceViewRenderer remote_view,
          String turn_user_name,
          String turn_credential,
          String[] ice_servers,
          String janus_web_socket_uri,
          Activity activity,
          String videoCodec,
          int videoStartBitrate,
          CapturerGenerator capturer_generator,
          int audioStartBitrate,
          String audioCodec,
          float keepAliveFrequency,
          long keepAliveTimeout) {
    this.keepAliveFrequency = keepAliveFrequency;
    this.keepAliveTimeout = keepAliveTimeout;

    iceServers = new PeerConnection.IceServer[ice_servers.length];
    for (int i = 0; i < iceServers.length; i++) {
      PeerConnection.IceServer.Builder b =  PeerConnection.IceServer.builder(ice_servers[i]);

      if (turn_user_name != null && turn_credential != null)
            b.setUsername(turn_user_name)
              .setPassword(turn_credential);

      iceServers[i] = b.createIceServer();
      System.out.println("Added turn server " + ice_servers[i]);
    }
    localView = local_view;
    remoteView = remote_view;
    janusWebSocketURL = janus_web_socket_uri;
    this.videoCodec = videoCodec;
    this.videoStartBitrate = videoStartBitrate;
    this.audioStartBitrate = audioStartBitrate;
    this.audioCodec = audioCodec;
    this.capturer_generator = capturer_generator;
    this.activity = activity;
  }
}
