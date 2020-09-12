package com.drohub.ParrotHelpers.Peripherals;

import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.MainCamera;
import com.parrot.drone.groundsdk.device.peripheral.camera.Camera;
import com.parrot.drone.groundsdk.device.peripheral.camera.CameraPhoto;
import com.parrot.drone.groundsdk.device.peripheral.camera.CameraZoom;
import com.parrot.drone.groundsdk.value.EnumSetting;

public class ParrotMainCamera implements IParrotPeripheral {
    public interface ZoomLevelListener {
        void onChange(double new_zoom_level);
    }

    final private ParrotMainCameraPriv _priv;

    public ParrotMainCamera(Drone drone) {
        _priv = new ParrotMainCameraPriv(drone);
    }

    public CameraZoom getZoom() {
        return _priv.getZoom();
    }

    private long last_zoom_set = System.currentTimeMillis();


    public boolean triggerPhotoPicture(boolean start) {
        if (_priv.getCameraMode().getValue() != Camera.Mode.PHOTO)
            _priv.getCameraMode().setValue(Camera.Mode.PHOTO);

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
        if (_priv.getCameraMode().getValue() != Camera.Mode.RECORDING)
            _priv.getCameraMode().setValue(Camera.Mode.RECORDING);

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
        if (!_priv.getZoom().isAvailable())
            return ZoomResult.BAD;
        if (System.currentTimeMillis() - last_zoom_set < 250)
            return ZoomResult.TOO_FAST;

        _priv.getZoom().control(CameraZoom.ControlMode.LEVEL, zoom_level);
        last_zoom_set = System.currentTimeMillis();
        return ZoomResult.GOOD;
    }
    
    public void setPeripheralListener(ParrotPeripheralManager.PeripheralListener l) {
        _priv.setPeripheralListener(l);
    }


    private class ParrotMainCameraPriv extends ParrotPeripheralPrivBase<MainCamera> {
        private CameraZoom _camera_zoom;

        private ZoomLevelListener _zoom_listener;
        private EnumSetting<Camera.Mode> _camera_mode;
        private double _last_recorded_zoom_level;
        
        ParrotMainCameraPriv(Drone drone) {
            super(drone, MainCamera.class);
        }


        CameraZoom getZoom() {
            return _camera_zoom;
        }

        EnumSetting<Camera.Mode> getCameraMode() {
            if (_camera_mode == null)
                throw new IllegalArgumentException("Camera mode not available");
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
