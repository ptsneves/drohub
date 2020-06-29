package com.drohub;

import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.Gimbal;
import com.parrot.drone.groundsdk.value.DoubleRange;

public class FlightActions {
    public static boolean isGimbalPitchAvailable(Drone drone) {
        final Gimbal gimbal = drone.getPeripheral(Gimbal.class, g -> {}).get();
        return  !(gimbal == null || !gimbal.getSupportedAxes().contains(Gimbal.Axis.PITCH));
    }

    public static boolean setVerticalGimbalPosition(Drone drone, float adim_dx, float adim_dy) {
        final Gimbal gimbal = drone.getPeripheral(Gimbal.class, g -> {}).get();

        if (!isGimbalPitchAvailable(drone))
            return false;

        DoubleRange adim_range = new DoubleRange() {
            @Override
            public double getLower() {
                return -1.0;
            }

            @Override
            public double getUpper() {
                return 1.0;
            }
        };

        DoubleRange pitch_range = gimbal.getAttitudeBounds(Gimbal.Axis.PITCH);
        double curr_pitch = gimbal.getAttitude(Gimbal.Axis.PITCH);
        double scaled_dy = pitch_range.scaleFrom(adim_dy, adim_range);
        double target_pitch = curr_pitch + scaled_dy;

        gimbal.control(Gimbal.ControlMode.POSITION,  null, target_pitch, null);
        return true;
    }
}