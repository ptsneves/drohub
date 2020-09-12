package com.drohub.Janus;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.media.projection.MediaProjection;
import android.util.Log;

import com.drohub.IInfoDisplay;
import com.drohub.Janus.PeerConnectionParameters.PeerConnectionParrotStreamParameters;
import com.drohub.Janus.PeerConnectionParameters.PeerConnectionParameters;
import com.drohub.Janus.PeerConnectionParameters.PeerConnectionScreenShareParameters;

import org.json.JSONObject;
import org.webrtc.*;

import java.io.InvalidObjectException;
import java.math.BigInteger;
import java.net.URISyntaxException;
import java.util.Arrays;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;


public class PeerConnectionClient implements JanusRTCInterface {
  public static final String VIDEO_TRACK_ID = "ARDAMSv0";
  public static final String AUDIO_TRACK_ID = "ARDAMSa0";
  private static final String TAG = "PCRTCClient";
  private static final String AUDIO_ECHO_CANCELLATION_CONSTRAINT = "googEchoCancellation";
  private static final String AUDIO_AUTO_GAIN_CONTROL_CONSTRAINT = "googAutoGainControl";
  private static final String AUDIO_HIGH_PASS_FILTER_CONSTRAINT = "googHighpassFilter";
  private static final String AUDIO_NOISE_SUPPRESSION_CONSTRAINT = "googNoiseSuppression";

  final private String displayName;
  final private Context context;
  final private PeerConnectionFactory factory;
  final private WebSocketChannel _webSocketChannel;
  final private ConcurrentHashMap<BigInteger, JanusConnection> peerConnectionMap;
  final private SurfaceViewRenderer local_video_sink;
  final private SurfaceViewRenderer remote_video_sink;

  final private EglBase.Context renderEGLContext;
  private boolean videoCapturerStopped;
  private boolean isError;

  private MediaConstraints sdpMediaConstraints;
  public PeerConnectionParameters peerConnectionParameters;
  private MediaStream local_webrtc_stream;
  private VideoCapturer videoCapturer;

  public PeerConnectionClient(long room_id, String displayName, final Context context,
                               IInfoDisplay display,
                               final PeerConnectionParameters peerConnectionParameters) throws InterruptedException, InvalidObjectException, URISyntaxException {
    try {
      peerConnectionMap = new ConcurrentHashMap<>();
      this.peerConnectionParameters = peerConnectionParameters;
      videoCapturerStopped = false;
      isError = false;
      local_webrtc_stream = null;
      videoCapturer = null;
      this.local_video_sink = peerConnectionParameters.localView;
      this.remote_video_sink = peerConnectionParameters.remoteView;
      this.context = context;
      EglBase rootEglBase = EglBase.create();
      this.renderEGLContext =  rootEglBase.getEglBaseContext();
      this.displayName = displayName;

      Log.d(TAG, "Capturing format: " + peerConnectionParameters.videoWidth +
              "x" + peerConnectionParameters.videoHeight + "@" + peerConnectionParameters.videoFps);

      isError = false;

      PeerConnectionFactory.InitializationOptions factory_init_options = PeerConnectionFactory.InitializationOptions
              .builder(context)
              .setInjectableLogger(((s, severity, s1) -> {
                Log.d("internal", s1);
              }), Logging.Severity.LS_INFO)
              .createInitializationOptions();

      PeerConnectionFactory.initialize(factory_init_options);


      factory = PeerConnectionFactory
              .builder()
              .setVideoDecoderFactory(new DefaultVideoDecoderFactory(renderEGLContext))
              .setVideoEncoderFactory(new DefaultVideoEncoderFactory(renderEGLContext, true, true))
              .setAudioEncoderFactoryFactory(new BuiltinAudioEncoderFactoryFactory())
              .setAudioDecoderFactoryFactory(new BuiltinAudioDecoderFactoryFactory())
              .createPeerConnectionFactory();

      _webSocketChannel = WebSocketChannel.createWebSockeChannel(
              room_id,
              displayName,
              peerConnectionParameters.activity,
              this,
              peerConnectionParameters.janusWebSocketURL,
              peerConnectionParameters,
              display);
    }
    catch (Exception e) {
      onStop();
      throw e;
    }
  }

  public boolean setMicrophoneMute(boolean do_mute) {
    return local_webrtc_stream.audioTracks.get(0).setEnabled(!do_mute);
  }

  public boolean isAudioEnabled() {
    return peerConnectionParameters.audioCodec != null;
  }

  public PeerConnection createRemotePeerConnection(BigInteger handleId) {
    return createPeerConnection(handleId, JanusConnection.ConnectionType.REMOTE);
  }


  public void createLocalPeerConnection( final BigInteger handleId) {
    if (peerConnectionParameters == null) {
      Log.e(TAG, "Creating peer connection without initializing factory.");
      return;
    }

    // Create SDP constraints.
    sdpMediaConstraints = new MediaConstraints();
    if (isAudioEnabled()) {
      sdpMediaConstraints.mandatory.add(
              new MediaConstraints.KeyValuePair("OfferToReceiveAudio", "true"));
    }
    else
      sdpMediaConstraints.mandatory.add(
              new MediaConstraints.KeyValuePair("OfferToReceiveAudio", "false"));

    PeerConnection peerConnection = createPeerConnection(handleId, JanusConnection.ConnectionType.LOCAL);

    local_webrtc_stream = factory.createLocalMediaStream("ARDAMS");
    VideoSource videoSource = factory.createVideoSource(false);

    try {
      switch (peerConnectionParameters.capturerType) {
        case CAMERA_FRONT:
          videoCapturer = createCamera2Capturer(videoSource.getCapturerObserver());
          break;
        case SCREEN_SHARE:
          videoCapturer = createScreenCapturer(videoSource.getCapturerObserver(),
                  ((PeerConnectionScreenShareParameters)peerConnectionParameters).getPermissionData(),
                  ((PeerConnectionScreenShareParameters)peerConnectionParameters).getPermissionResultCode());
          break;
        case GROUNDSDK_VIDEO_SHARE:
          videoCapturer = createGroundSDKVideoCapturer(videoSource.getCapturerObserver());
      }
      videoCapturerStopped = false;
    } catch (InvalidObjectException | PeerConnectionScreenShareParameters.InvalidScreenPermissions e) {
      Log.e(TAG, e.getMessage());
      e.printStackTrace();
    }
    local_webrtc_stream.addTrack(createVideoTrack(videoSource));
    if (isAudioEnabled()) {
      local_webrtc_stream.addTrack(createAudioTrack());
    }
    peerConnection.addStream(local_webrtc_stream);
  }

  private PeerConnection createPeerConnection(BigInteger handleId, JanusConnection.ConnectionType type) {
    Log.d(TAG, "Create peer connection.");

    PeerConnection.RTCConfiguration rtcConfig = new PeerConnection.RTCConfiguration(
            Arrays.asList(peerConnectionParameters.iceServers));

    rtcConfig.iceTransportsType = PeerConnection.IceTransportsType.ALL;
    rtcConfig.enableDtlsSrtp = true;


    PeerConnectionObserver pcObserver = new PeerConnectionObserver(remote_video_sink, _webSocketChannel, handleId);

    PeerConnection peerConnection = factory.createPeerConnection(rtcConfig, pcObserver);
    if (peerConnection == null)
      throw new NullPointerException("peer connection is null");

    JanusConnection janusConnection = new JanusConnection();
    janusConnection.handleId = handleId;
    janusConnection.sdpObserver = new SDPObserver(_webSocketChannel, peerConnection, handleId, type);
    janusConnection.peerConnection = peerConnection;
    janusConnection.type = type;

    peerConnectionMap.put(handleId, janusConnection);

    Log.d(TAG, "Peer connection created.");
    return peerConnection;
  }

  public void onStop() {
    Log.d(TAG, "Closing peer connection.");

    if (peerConnectionMap != null) {
      for (Map.Entry<BigInteger, JanusConnection> entry: peerConnectionMap.entrySet()) {
        if (entry.getValue().peerConnection != null) {
          entry.getValue().peerConnection.dispose();
        }
      }
    }

    Log.d(TAG, "Stopping capture.");
    if (videoCapturer != null) {
      try {
        videoCapturer.stopCapture();
      } catch (InterruptedException e) {
        Log.e(TAG, "Failed to stop capture " + e.getMessage());
      }
      videoCapturerStopped = true;
      videoCapturer.dispose();
      videoCapturer = null;
    }

    Log.d(TAG, "Closing peer connection factory.");
    if (factory != null)
      factory.dispose();
    Log.d(TAG, "Closing peer connection done.");
    if (_webSocketChannel != null)
      _webSocketChannel.close();
  }


  public void setRemoteDescription(final BigInteger handleId, final SessionDescription sdp) {
    PeerConnection peerConnection = peerConnectionMap.get(handleId).peerConnection;
    if (peerConnection == null || isError) {
      return;
    }
    SDPObserver sdpObserver = peerConnectionMap.get(handleId).sdpObserver;

    peerConnection.setRemoteDescription(sdpObserver, sdp);
  }

  public void subscriberHandleRemoteJsep(final BigInteger handleId, final SessionDescription sdp) {
      PeerConnection peerConnection = createRemotePeerConnection(handleId);
      SDPObserver sdpObserver = peerConnectionMap.get(handleId).sdpObserver;
      if (peerConnection == null || isError) {
        return;
      }
      JanusConnection connection = peerConnectionMap.get(handleId);
      peerConnection.setRemoteDescription(sdpObserver, sdp);
      Log.d(TAG, "PC create ANSWER");
      peerConnection.createAnswer(connection.sdpObserver, sdpMediaConstraints);
  }

  private AudioTrack createAudioTrack() {
    // Create audio constraints.
    MediaConstraints audioConstraints = new MediaConstraints();
    audioConstraints.mandatory.add(
            new MediaConstraints.KeyValuePair(AUDIO_ECHO_CANCELLATION_CONSTRAINT, "true"));
    audioConstraints.mandatory.add(
            new MediaConstraints.KeyValuePair(AUDIO_AUTO_GAIN_CONTROL_CONSTRAINT, "true"));
    audioConstraints.mandatory.add(
            new MediaConstraints.KeyValuePair(AUDIO_HIGH_PASS_FILTER_CONSTRAINT, "true"));
    audioConstraints.mandatory.add(
            new MediaConstraints.KeyValuePair(AUDIO_NOISE_SUPPRESSION_CONSTRAINT, "true"));
    AudioSource audioSource = factory.createAudioSource(audioConstraints);
    AudioTrack localAudioTrack = factory.createAudioTrack(AUDIO_TRACK_ID, audioSource);
    localAudioTrack.setEnabled(true);
    return localAudioTrack;
  }

  private VideoTrack createVideoTrack(VideoSource videoSource) {
    VideoTrack localVideoTrack = factory.createVideoTrack(VIDEO_TRACK_ID, videoSource);
    localVideoTrack.setEnabled(true);
    if (local_video_sink != null) {
      local_video_sink.init(renderEGLContext, null);
      local_video_sink.setEnableHardwareScaler(true);
      localVideoTrack.addSink(local_video_sink);
    }
    return localVideoTrack;
  }


  // interface JanusRTCInterface
  @Override
  public void onPublisherJoined(final BigInteger handleId) {
    createLocalPeerConnection(handleId);
    JanusConnection connection = peerConnectionMap.get(handleId);
    PeerConnection peerConnection = connection.peerConnection;
    if (peerConnection != null && !isError) {
      Log.d(TAG, "PC Create OFFER");
      peerConnection.createOffer(connection.sdpObserver, sdpMediaConstraints);
    }
  }

  @Override
  public void onPublisherRemoteJsep(final BigInteger handleId, final JSONObject jsep) {
    SessionDescription.Type type = SessionDescription.Type.fromCanonicalForm(jsep.optString("type"));
    String sdp = jsep.optString("sdp");
    SessionDescription sessionDescription = new SessionDescription(type, sdp);
    setRemoteDescription(handleId, sessionDescription);
  }

  @Override
  public void subscriberHandleRemoteJsep(final BigInteger handleId, final JSONObject jsep) {
    SessionDescription.Type type = SessionDescription.Type.fromCanonicalForm(jsep.optString("type"));
    String sdp = jsep.optString("sdp");
    SessionDescription sessionDescription = new SessionDescription(type, sdp);
    subscriberHandleRemoteJsep(handleId, sessionDescription);
  }

  @Override
  public void onLeaving(BigInteger handleId) {

  }

  private VideoCapturer createGroundSDKVideoCapturer(CapturerObserver capturerObserver) {
    SurfaceTextureHelper surfaceTextureHelper = SurfaceTextureHelper.create("VideoCapturerThread", renderEGLContext);
    GLCapturer capturer = new GLCapturer(renderEGLContext,
            ((PeerConnectionParrotStreamParameters)peerConnectionParameters).LiveVideoStreamServer);

    capturer.initialize(surfaceTextureHelper, context, capturerObserver);
    capturer.startCapture(peerConnectionParameters.videoWidth, peerConnectionParameters.videoHeight,
            peerConnectionParameters.videoFps);
    return capturer;
  }

  private VideoCapturer createCamera2Capturer(CapturerObserver capturerObserver) throws InvalidObjectException {
    if (Camera2Enumerator.isSupported(context)) {
      CameraEnumerator enumerator = new Camera2Enumerator(context);
      final String[] deviceNames = enumerator.getDeviceNames();
      for (String device_name : deviceNames) {
        if (enumerator.isFrontFacing(device_name)) {
          Log.d(TAG, "Creating capturer using camera2 API.");
          SurfaceTextureHelper surfaceTextureHelper = SurfaceTextureHelper.create("VideoCapturerThread", renderEGLContext);
          Camera2Capturer camera2Capturer = new Camera2Capturer(context, device_name, null);
          camera2Capturer.initialize(surfaceTextureHelper, context, capturerObserver);
          camera2Capturer.startCapture(peerConnectionParameters.videoWidth, peerConnectionParameters.videoHeight,
                  peerConnectionParameters.videoFps);
          return camera2Capturer;
        }
      }
    }
    throw new InvalidObjectException("Could not find front camera or camera2enumerator is not supported");
  }


  private VideoCapturer createScreenCapturer(CapturerObserver capturerObserver, Intent data, int code) throws InvalidObjectException {
    if (code != Activity.RESULT_OK) {
      throw new InvalidObjectException("No permissions for screen sharing");
    }
    ScreenCapturerAndroid cap = new ScreenCapturerAndroid(data, new MediaProjection.Callback() {
      @Override
      public void onStop() {
        super.onStop();
      }
    });
    SurfaceTextureHelper surfaceTextureHelper = SurfaceTextureHelper.create("VideoCapturerThread", renderEGLContext);
    cap.initialize(surfaceTextureHelper, context, capturerObserver);
    cap.startCapture(peerConnectionParameters.videoWidth, peerConnectionParameters.videoHeight, peerConnectionParameters.videoFps);
    return cap;
  }
}
