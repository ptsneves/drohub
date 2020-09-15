package com.drohub.Devices.Peripherals.Parrot;

import androidx.annotation.NonNull;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.thift.gen.CameraMode;
import com.drohub.thift.gen.CameraState;
import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.MainCamera;
import com.parrot.drone.groundsdk.device.peripheral.camera.Camera;
import com.parrot.drone.groundsdk.device.peripheral.camera.CameraPhoto;
import com.parrot.drone.groundsdk.device.peripheral.camera.CameraZoom;
import com.parrot.drone.groundsdk.value.EnumSetting;
import org.jetbrains.annotations.NotNull;

public class ParrotMainCamera implements IPeripheral<ParrotMainCamera> {
    public interface ZoomLevelListener {
        void onChange(double new_zoom_level);
    }

    public interface CameraStateListener {
        void onChange(CameraState camera_state);
    }

    final private ParrotMainCameraPriv _priv;
    final private String _serial;

    private long last_zoom_set = System.currentTimeMillis();
    private CameraStateListener _camera_state_listener = null;


    public ParrotMainCamera(Drone drone) {
        _serial = drone.getUid();
        _priv = new ParrotMainCameraPriv(drone);
        _priv.setPeripheralListener(new ParrotPeripheralManager.PeripheralListener<MainCamera>() {
            @Override
            public void onChange(@NonNull @NotNull MainCamera main_camera) {
                if (_camera_state_listener == null)
                    return;

                CameraMode mode = CameraMode.ERROR;
                switch (main_camera.mode().getValue()) {
                    case PHOTO:
                        mode = CameraMode.PICTURE;
                        break;
                    case RECORDING:
                        mode = CameraMode.VIDEO;
                        break;
                }

                _camera_state_listener.onChange(new CameraState(
                        mode,
                        main_camera.zoom().getCurrentLevel(),
                        1.0f,
                        main_camera.zoom().getMaxLossyLevel(),
                        _serial,
                        System.currentTimeMillis()
                ));
            }

            @Override
            public boolean onFirstTimeAvailable(@NonNull @NotNull MainCamera mainCamera) {
                return true;
            }
        });
    }

    public void setCameraStateListener(CameraStateListener l) {
        _camera_state_listener = l;
    }

    public void setZoomLevelListener(ZoomLevelListener l) {
        _priv.setZoomLevelUpdateListener(l);
    }

    @Override
    public void setPeripheralListener(IPeripheralListener<ParrotMainCamera> l) {
        _priv.setPeripheralListener(ParrotPeripheralManager.PeripheralListener.convert(l, this));
    }

    public boolean triggerPhotoPicture(boolean start) {
        try {
            if (_priv.getCameraMode().getValue() != Camera.Mode.PHOTO)
                _priv.getCameraMode().setValue(Camera.Mode.PHOTO);
        }
        catch (ParrotMainCameraPriv.ParrotMainCameraPrivException e) {
            return false;
        }

        try {
            if (start) {
                _priv.get().startPhotoCapture();
                return _priv.get().photoState().get() == CameraPhoto.State.FunctionState.STARTED;
            } else {
                _priv.get().stopPhotoCapture();
                switch (_priv.get().photoState().get()) {
                    case STOPPED:
                    case STOPPING:
                        return true;
                }
            }
        } catch (IllegalAccessException e) {
            return false;
        }
        return false;
    }

    public boolean recordVideo(boolean start) {
        try {
            if (_priv.getCameraMode().getValue() != Camera.Mode.RECORDING)
                _priv.getCameraMode().setValue(Camera.Mode.RECORDING);
        }
        catch (ParrotMainCameraPriv.ParrotMainCameraPrivException e) {
            return false;
        }

        try {
            if (start) {
                _priv.get().startRecording();
                switch (_priv.get().recordingState().get()) {
                    case STARTED:
                    case STARTING:
                        return true;
                }
            } else {
                _priv.get().stopRecording();
                switch (_priv.get().recordingState().get()) {
                    case STOPPED:
                    case STOPPING:
                        return true;
                }
            }
        } catch (IllegalAccessException e) {
            return false;
        }
        return false;
    }

    public enum ZoomResult {
        GOOD,
        BAD,
        TOO_FAST
    }

    public ZoomResult setZoom(double zoom_level) {
        try {
            if (!_priv.getZoom().isAvailable())
                return ZoomResult.BAD;
            if (System.currentTimeMillis() - last_zoom_set < 250)
                return ZoomResult.TOO_FAST;

            double _scale_factor = _priv.getZoom().getCurrentLevel() * zoom_level;
            _scale_factor = Math.max(_scale_factor, 1.0f);// prevent our view from becoming too small //
            _scale_factor = Math.min(_scale_factor, _priv.getZoom().getMaxLossyLevel());// prevent our view from becoming too big //
            _priv.getZoom().control(CameraZoom.ControlMode.LEVEL, _scale_factor);
        }
        catch (ParrotMainCameraPriv.ParrotMainCameraPrivException e) {
            return ZoomResult.BAD;
        }

        last_zoom_set = System.currentTimeMillis();
        return ZoomResult.GOOD;
    }

    private class ParrotMainCameraPriv extends ParrotPeripheralPrivBase<MainCamera> {

        public class ParrotMainCameraPrivException extends Exception {
            protected ParrotMainCameraPrivException(String msg) {
                super(msg);
            }
        }

        private CameraZoom _camera_zoom;

        private ZoomLevelListener _zoom_listener;
        private EnumSetting<Camera.Mode> _camera_mode;
        private double _last_recorded_zoom_level;
        
        ParrotMainCameraPriv(Drone drone) {
            super(drone, MainCamera.class);
        }


        CameraZoom getZoom() throws ParrotMainCameraPrivException {
            if (_camera_zoom == null)
                throw new ParrotMainCameraPrivException("Zoom functionality not available");
            return _camera_zoom;
        }

        EnumSetting<Camera.Mode> getCameraMode() throws ParrotMainCameraPrivException {
            if (_camera_mode == null)
                throw new ParrotMainCameraPrivException("Camera mode not available");
            return _camera_mode;
        }

        void setZoomLevelUpdateListener(ZoomLevelListener l) {
            _zoom_listener = l;
        }

        @Override
        public void onChange(MainCamera mc) {
            CameraZoom zoom = mc.zoom();
            if (zoom == null)
                return;
                
            double cur_zoom_level = zoom.getCurrentLevel();
            if (cur_zoom_level != _last_recorded_zoom_level) {
                _last_recorded_zoom_level = cur_zoom_level;
                if (_zoom_listener != null)
                    _zoom_listener.onChange(cur_zoom_level);
            }
            if (_peripheral_listener != null)
                _peripheral_listener.onChange(mc);
        }

        @Override
        public boolean onFirstTimeAvailable(MainCamera camera) {
            _camera_mode = camera.mode();
            _camera_zoom = camera.zoom();

            if (_camera_zoom == null || _camera_mode == null)
                return false;

            _last_recorded_zoom_level = _camera_zoom.getCurrentLevel();
            if (_peripheral_listener != null)
                return _peripheral_listener.onFirstTimeAvailable(camera);

            return true;
        }

    }
}