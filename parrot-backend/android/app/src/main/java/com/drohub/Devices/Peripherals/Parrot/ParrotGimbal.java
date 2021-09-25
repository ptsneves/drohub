package com.drohub.Devices.Peripherals.Parrot;

import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.thift.gen.GimbalCalibrationState;
import com.drohub.thift.gen.GimbalState;
import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.Gimbal;
import com.parrot.drone.groundsdk.value.DoubleRange;

public class ParrotGimbal implements IPeripheral<ParrotGimbal> {
    public interface AttitudeListener {
        void onChange(double degrees);
    }

    public interface GimbalStateListener {
        void onChange(GimbalState state);
    }

    final private ParrotGimbalPriv _priv;

    public ParrotGimbal(Drone drone) {
        _priv = new ParrotGimbalPriv(drone);
    }

    public boolean setAttitude(double pitch_degrees, double roll_degrees, double yaw_degrees) {
        try {
            if (!_priv._controls_pitch && pitch_degrees != 0.0f)
                return false;
            if (!_priv._controls_roll && roll_degrees != 0.0f)
                return false;
            if (!_priv._controls_yaw && yaw_degrees != 0.0f)
                return false;

            _priv.get().control(Gimbal.ControlMode.POSITION, yaw_degrees, pitch_degrees, roll_degrees);
            return true;
        }
        catch (IllegalAccessException e) {
            return false;
        }
    }

    public boolean setAttitudeRelative(double pitch_degrees, double roll_degrees, double yaw_degrees) {
        return setAttitude(_priv._pitch + pitch_degrees,
                _priv._roll + roll_degrees,
                _priv._yaw + yaw_degrees);
    }

    public void setGimbalStateListener(GimbalStateListener l) {
        _priv._state_listener = l;
    }

    @Override
    public void setPeripheralListener(IPeripheralListener<ParrotGimbal> l) {
        _priv.setPeripheralListener(ParrotPeripheralManager.PeripheralListener.convert(l, this));
    }

    @Override
    public void start() {
        _priv.start();
    }

    public void setPitchListener(AttitudeListener listener) {
        _priv._pitch_listener = listener;
    }

    public void setRollListener(AttitudeListener listener) {
        _priv._roll_listener = listener;
    }

    public void setYawListener(AttitudeListener listener) {
        _priv._yaw_listener = listener;
    }

    public boolean supportsYaw() {
        return _priv._controls_yaw;
    }

    public boolean supportsRoll() {
        return _priv._controls_roll;
    }

    public boolean supportsPitch() {
        return _priv._controls_pitch;
    }

    private static class ParrotGimbalPriv extends ParrotPeripheralPrivBase<Gimbal> {
        private String _serial;

        private boolean _controls_pitch;
        private boolean _controls_roll;
        private boolean _controls_yaw;
        private AttitudeListener _pitch_listener;
        private AttitudeListener _roll_listener;
        private AttitudeListener _yaw_listener;
        private GimbalStateListener _state_listener;
        
        private double _pitch;
        private double _roll;
        private double _yaw;

        private ParrotGimbalPriv(Drone drone) {
            super(drone, Gimbal.class);
            _serial = drone.getUid();
            _controls_pitch = false;
            _controls_roll = false;
            _controls_yaw = false;
            _pitch = 0.0f;
            _roll = 0.0f;
            _yaw = 0.0f;
        }

        private GimbalCalibrationState getCalibrationState(Gimbal gimbal) {
            GimbalCalibrationState state;

            if (!gimbal.currentErrors().isEmpty())
                return GimbalCalibrationState.ERROR;

            state = gimbal.isCalibrated() ? GimbalCalibrationState.CALIBRATED : GimbalCalibrationState.UNCALIBRATED;
            switch (gimbal.getCalibrationProcessState()) {
                case CALIBRATING:
                    state = GimbalCalibrationState.CALIBRATING;
                    break;
                case SUCCESS:
                case NONE:
                case FAILURE:
                case CANCELED:
                    break;
            }
            return state;
        }

        private class NoRange implements DoubleRange {
            @Override
            public double getLower() {
                return 0;
            }

            @Override
            public double getUpper() {
                return 0;
            }
        }

        @Override
        public void onChange(Gimbal gimbal) {
            if (_controls_pitch && gimbal.getAttitude(Gimbal.Axis.PITCH) != _pitch) {
                _pitch = gimbal.getAttitude(Gimbal.Axis.PITCH);
                if(_pitch_listener != null)
                    _pitch_listener.onChange(_pitch);
            }

            if (_controls_roll && gimbal.getAttitude(Gimbal.Axis.ROLL) != _roll) {
                _roll = gimbal.getAttitude(Gimbal.Axis.ROLL);
                if (_roll_listener != null)
                    _roll_listener.onChange(_roll);
            }

            if (_controls_yaw && gimbal.getAttitude(Gimbal.Axis.YAW) != _yaw) {
                _yaw = gimbal.getAttitude(Gimbal.Axis.ROLL);
                if (_yaw_listener != null)
                    _yaw_listener.onChange(_yaw);
            }

            if (_state_listener != null) {
                DoubleRange roll_bounds = _controls_roll ? gimbal.getAttitudeBounds(Gimbal.Axis.ROLL): new NoRange();
                DoubleRange yaw_bounds = _controls_yaw ? gimbal.getAttitudeBounds(Gimbal.Axis.YAW) : new NoRange();
                DoubleRange pitch_bounds = _controls_pitch ? gimbal.getAttitudeBounds(Gimbal.Axis.PITCH): new NoRange();

                _state_listener.onChange(new GimbalState(
                        getCalibrationState(gimbal),
                        _roll,
                        _pitch,
                        _yaw,
                        roll_bounds.getLower(),
                        roll_bounds.getUpper(),
                        yaw_bounds.getLower(),
                        yaw_bounds.getUpper(),
                        pitch_bounds.getLower(),
                        pitch_bounds.getUpper(),
                        _controls_roll && gimbal.getStabilization(Gimbal.Axis.ROLL).isEnabled(),
                        _controls_yaw && gimbal.getStabilization(Gimbal.Axis.YAW).isEnabled(),
                        _controls_pitch && gimbal.getStabilization(Gimbal.Axis.PITCH).isEnabled(),
                        _serial,
                        System.currentTimeMillis()
                ));
            }

            if (_peripheral_listener != null)
                _peripheral_listener.onChange(gimbal);

        }

        @Override
        public boolean onFirstTimeAvailable(Gimbal gimbal) {
            _controls_pitch = gimbal.getSupportedAxes().contains(Gimbal.Axis.PITCH);
            _controls_roll = gimbal.getSupportedAxes().contains(Gimbal.Axis.ROLL);
            _controls_yaw = gimbal.getSupportedAxes().contains(Gimbal.Axis.YAW);
            if (_peripheral_listener != null)
                return _peripheral_listener.onFirstTimeAvailable(gimbal);
            return true;
        }
    }
}
