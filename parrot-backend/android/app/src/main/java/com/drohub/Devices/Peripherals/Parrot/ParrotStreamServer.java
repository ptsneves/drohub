package com.drohub.Devices.Peripherals.Parrot;

import androidx.annotation.NonNull;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.Janus.PeerConnectionParameters.PeerConnectionParameters;
import com.drohub.Janus.PeerConnectionParameters.PeerConnectionParrotStreamParameters;
import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.StreamServer;

public class ParrotStreamServer implements IPeripheral<ParrotStreamServer> {

    ParrotStreamServerPriv _priv;
    public ParrotStreamServer(Drone drone) {
        _priv = new ParrotStreamServerPriv(drone);
    }

    @Override
    public void setPeripheralListener(IPeripheralListener<ParrotStreamServer> l) {
        _priv.setPeripheralListener(ParrotPeripheralManager.PeripheralListener.convert(l, this));
    }

    public PeerConnectionParrotStreamParameters getConnectionParameters(PeerConnectionParameters p) throws IllegalAccessException {
        return new PeerConnectionParrotStreamParameters(p, _priv.get());
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
