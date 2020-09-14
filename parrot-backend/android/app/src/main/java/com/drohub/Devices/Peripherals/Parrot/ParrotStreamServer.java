package com.drohub.Devices.Peripherals.Parrot;

import androidx.annotation.NonNull;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.Janus.PeerConnectionParameters;
import com.drohub.hud.GroundSDKVideoCapturer;
import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.StreamServer;
import org.jetbrains.annotations.NotNull;
import org.webrtc.SurfaceTextureHelper;

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

    private ParrotPeripheralManager.PeripheralListener<StreamServer> getParrotPeripheralListener() {
        final ParrotStreamServer instance = this;
        return new ParrotPeripheralManager.PeripheralListener<StreamServer>() {
            @Override
            public void onChange(@NonNull @NotNull StreamServer streamServer) {
                if (_peripheral_l != null)
                    _peripheral_l.onChange(streamServer);
            }

            @Override
            public boolean onFirstTimeAvailable(@NonNull @NotNull StreamServer streamServer) {
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

    public void setCapturerListener(
            int video_width,
            int video_height,
            int video_fps,
            IVideoCapturerListener<ParrotStreamServer> listener) {

        _capturer_l = new CapturerListenerPriv(listener, video_width, video_height, video_fps);
        final ParrotStreamServer this_instance = this;
        if (_peripheral_l == null) {
            _priv.setPeripheralListener(new ParrotPeripheralManager.PeripheralListener<StreamServer>() {
                @Override
                public void onChange(@NonNull StreamServer streamServer) {
                }

                @Override
                public boolean onFirstTimeAvailable(@NonNull StreamServer streamServer) {
                    _capturer_l.listener.onCapturerAvailable(
                            this_instance,
                            _capturer_l.generateCapturer(streamServer));
                    return true;

                }
            });
        }
        else
            _priv.setPeripheralListener(_peripheral_l);
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

            return ((egl_context, observer) -> {
                SurfaceTextureHelper surfaceTextureHelper = SurfaceTextureHelper.create("VideoCapturerThread",
                        egl_context);

                GroundSDKVideoCapturer capturer = new GroundSDKVideoCapturer(
                        stream_server,
                        egl_context,
                        video_width,
                        video_height
                );

                capturer.initialize(surfaceTextureHelper, null, observer);
                capturer.startCapture(video_width, video_height, video_fps);
                return capturer;
            });
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
