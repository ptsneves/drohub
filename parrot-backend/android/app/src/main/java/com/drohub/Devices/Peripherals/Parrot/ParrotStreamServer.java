package com.drohub.Devices.Peripherals.Parrot;

import androidx.annotation.NonNull;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.Janus.PeerConnectionParameters;
import com.drohub.hud.GroundSDKVideoCapturer;
import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.StreamServer;
import com.parrot.drone.groundsdk.device.peripheral.stream.CameraLive;
import com.parrot.drone.groundsdk.internal.stream.GlRenderSink;
import com.parrot.drone.groundsdk.stream.Stream;
import org.webrtc.CapturerObserver;
import org.webrtc.EglBase;
import org.webrtc.SurfaceTextureHelper;
import org.webrtc.VideoCapturer;

public class ParrotStreamServer implements IPeripheral<ParrotStreamServer>, IPeripheral.ICapturerProvider<ParrotStreamServer> {
    ParrotStreamServerPriv _priv;
    private CapturerListenerPriv _capturer_l;
    ParrotPeripheralManager.PeripheralListener<StreamServer> _peripheral_l;
    boolean _listener_first_time_success;

    public ParrotStreamServer(Drone drone) {
        _priv = new ParrotStreamServerPriv(drone);
        _priv.setPeripheralListener(getParrotPeripheralListener());
        _listener_first_time_success = false;
    }

    @Override
    public void setPeripheralListener(IPeripheralListener<ParrotStreamServer> l) {
        _peripheral_l = ParrotPeripheralManager.PeripheralListener.convert(l, this);
        _listener_first_time_success = false;
    }

    @Override
    public void start() {
        _priv.start();
    }

    private ParrotPeripheralManager.PeripheralListener<StreamServer> getParrotPeripheralListener() {
        final ParrotStreamServer instance = this;
        return new ParrotPeripheralManager.PeripheralListener<StreamServer>() {
            @Override
            public void onChange(StreamServer streamServer) {
                if (_peripheral_l != null)
                    _peripheral_l.onChange(streamServer);
            }

            @Override
            public boolean onFirstTimeAvailable(@NonNull StreamServer streamServer) {
                if (_peripheral_l != null && !_listener_first_time_success) {
                    if (!_peripheral_l.onFirstTimeAvailable(streamServer))
                        return false;
                }
                _listener_first_time_success = true;

                if (_capturer_l != null) {
                    return _capturer_l.listener.onCapturerAvailable(instance, _capturer_l.generateCapturer(streamServer));
                }
                return true;
            }
        };
    }

    synchronized public void setCapturerListener(
            int video_width,
            int video_height,
            int video_fps,
            IVideoCapturerListener<ParrotStreamServer> listener) {

        _capturer_l = new CapturerListenerPriv(listener, video_width, video_height, video_fps);
    }

    private static class CapturerListenerPriv {
        private final int video_width;
        private final int video_height;
        private final int video_fps;
        private final IVideoCapturerListener<ParrotStreamServer> listener;

        private CapturerListenerPriv(
                IVideoCapturerListener<ParrotStreamServer> listener,
                int video_width,
                int video_height,
                int video_fps) {

            this.listener = listener;
            this.video_width = video_width;
            this.video_height = video_height;
            this.video_fps = video_fps;
        }


        private PeerConnectionParameters.CapturerGenerator generateCapturer(StreamServer stream_server) {
            return (egl_context, observer) -> getCapturer(stream_server, egl_context, observer);
        }

        private VideoCapturer getCapturer(StreamServer stream_server, EglBase.Context egl_context, CapturerObserver observer) {
            SurfaceTextureHelper surfaceTextureHelper = SurfaceTextureHelper.create("VideoCapturerThread",
                    egl_context);

            GroundSDKVideoCapturer capturer = new GroundSDKVideoCapturer(
                    new GroundSDKVideoCapturer.IParrotStreamServerControl() {
                        private Stream.Sink _sink;
                        private CameraLive _camera_live;
                        @Override
                        public boolean startStream(GlRenderSink.Callback render_sink_cb) {
                            if (_sink != null)
                                return false;
                            stream_server.enableStreaming(true);

                            if (_camera_live != null)
                                return false;

                            _camera_live = stream_server.live(cam_live -> {
                                if (cam_live == null)
                                    System.out.println("WHAAA??");
                            }).get();
                            if (_camera_live == null)
                                return false;

                            _sink = _camera_live.openSink(GlRenderSink.config(render_sink_cb));
                            return _camera_live.play();
                        }

                        @Override
                        public boolean stopStream() {
                            if (_camera_live == null || _camera_live.playState() != CameraLive.PlayState.PLAYING)
                                return false;
                            _camera_live.stop();
                            _sink.close();
                            if(stream_server.streamingEnabled())
                                stream_server.enableStreaming(false);
                            return true;
                        }
                    },
                    egl_context,
                    video_width,
                    video_height
            );

            capturer.initialize(surfaceTextureHelper, null, observer);
            capturer.startCapture(video_width, video_height, video_fps);
            return capturer;
        }
    }

    private static class ParrotStreamServerPriv extends ParrotPeripheralPrivBase<StreamServer> {
        private ParrotStreamServerPriv(Drone drone) {
            super(drone, StreamServer.class);
        }

        @Override
        public void onChange(@NonNull StreamServer stream_server) {
            if (_peripheral_listener != null)
                _peripheral_listener.onChange(stream_server);
        }

        @Override
        public boolean onFirstTimeAvailable(@NonNull StreamServer stream_server) {
            if (_peripheral_listener != null)
                return _peripheral_listener.onFirstTimeAvailable(stream_server);
            return true;
        }
    }
}
