package com.drohub.ParrotHelpers.Peripherals;

import androidx.annotation.NonNull;
import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.StreamServer;

public class ParrotStreamServer implements IParrotPeripheral {
    ParrotStreamServerPriv _priv;
    public ParrotStreamServer(Drone drone) {
        _priv = new ParrotStreamServerPriv(drone);
    }

    @Override
    public void setPeripheralListener(ParrotPeripheralManager.PeripheralListener l) {
        _priv.setPeripheralListener(l);
    }

    private class ParrotStreamServerPriv extends ParrotPeripheralPrivBase<StreamServer> {
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
