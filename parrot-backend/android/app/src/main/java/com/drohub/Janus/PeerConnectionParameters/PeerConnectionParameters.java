package com.drohub.Janus.PeerConnectionParameters;

import android.app.Activity;

import com.drohub.CopterHudActivity;
import com.drohub.Devices.Peripherals.Parrot.ParrotStreamServer;
import org.webrtc.PeerConnection;
import org.webrtc.SurfaceViewRenderer;

import java.security.InvalidParameterException;

public class PeerConnectionParameters {
  public enum VideoCapturerType {
    CAMERA_FRONT,
    SCREEN_SHARE,
    GROUNDSDK_VIDEO_SHARE,
    UNDEFINED
  }
  public final PeerConnection.IceServer[] iceServers;
  public final int videoWidth;
  public final int videoHeight;
  public final int videoFps;
  public final String janusWebSocketURL;
  public final String videoCodec;
  public final int audioStartBitrate;
  public final int videoStartBitrate;
  public final String audioCodec;
  public final Activity activity;
  public final VideoCapturerType capturerType;
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
          int videoWidth,
          int videoHeight,
          int videoFps,
          String videoCodec,
          int videoStartBitrate,
          VideoCapturerType capturerType,
          int audioStartBitrate,
          String audioCodec,
          float keepAliveFrequency,
          long keepAliveTimeout) {
    this.keepAliveFrequency = keepAliveFrequency;
    this.keepAliveTimeout = keepAliveTimeout;

    // If video resolution is not specified, default to HD.
    if (videoWidth == 0 || videoHeight == 0)
      throw new InvalidParameterException("Video width or height cannot be 0");

    // If fps is not specified, default to 30.
    if (videoFps == 0)
      throw new InvalidParameterException("Video FPS cannot be 0");

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
    this.activity = activity;
    this.videoWidth = videoWidth;
    this.videoHeight = videoHeight;
    this.videoFps = videoFps;
    this.videoCodec = videoCodec;
    this.videoStartBitrate = videoStartBitrate;
    this.audioStartBitrate = audioStartBitrate;
    this.audioCodec = audioCodec;
    this.capturerType = capturerType;
  }

  public PeerConnectionParameters(PeerConnectionParameters p) {
    iceServers = p.iceServers;
    videoWidth = p.videoWidth;
    videoHeight = p.videoHeight;
    videoFps = p.videoFps;
    janusWebSocketURL = p.janusWebSocketURL;
    videoCodec = p.videoCodec;
    audioStartBitrate = p.audioStartBitrate;
    videoStartBitrate = p.videoStartBitrate;
    audioCodec = p.audioCodec;
    activity = p.activity;
    capturerType = p.capturerType;
    localView = p.localView;
    remoteView = p.remoteView;
    keepAliveFrequency = p.keepAliveFrequency;
    keepAliveTimeout = p.keepAliveTimeout;
  }
}
