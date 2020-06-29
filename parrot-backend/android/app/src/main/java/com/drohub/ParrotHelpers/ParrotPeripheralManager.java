package com.drohub.ParrotHelpers;

import androidx.annotation.NonNull;
import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.Peripheral;

public class ParrotPeripheralManager<C extends Peripheral> {
    public interface PeripheralListener<C> {
        void onChange(@NonNull C c); //Will be called after first time available
        boolean onFirstTimeAvailable(@NonNull C c);
    }

    private boolean _valid_handle;
    private C _peripheral_handle;
    private PeripheralListener _peripheral_listener;

    void setPeripheralListener(PeripheralListener on_change_listener) {
        _peripheral_listener = on_change_listener;
    }

    public ParrotPeripheralManager(Drone drone, Class<C> peripheral_class, PeripheralListener on_change_listener) {
        setPeripheralListener(on_change_listener);
        _valid_handle = false;
        _peripheral_handle = drone.getPeripheral(peripheral_class, peripheral -> {
            if (peripheral == null) {
                _valid_handle = false;
                return;
            }

            if (!_valid_handle && _peripheral_listener != null) {
                _valid_handle = _peripheral_listener.onFirstTimeAvailable(peripheral);
                if (!_valid_handle)
                    return;
            }
            else
                _valid_handle = true;


            _peripheral_handle = peripheral;
            if (_peripheral_listener != null)
                _peripheral_listener.onChange(peripheral);
        }).get();
    }

    public C get() throws IllegalAccessException {
        if (_valid_handle)
            return _peripheral_handle;
        throw new IllegalAccessException("Handle is not valid");
    }

    public C get(long time_to_wait) throws IllegalAccessException {
        if (_valid_handle)
            return _peripheral_handle;

        long end_time = System.currentTimeMillis() + time_to_wait;
        do {
            try {
                Thread.sleep(300);
            } catch (InterruptedException e) {
                throw new IllegalAccessException("Sleep was interrupted. Giving up trying to get peripheral");
            }
            if (_valid_handle)
                return _peripheral_handle;
        } while (end_time > System.currentTimeMillis());

        throw new IllegalAccessException("Timeout waiting for peripheral handle");
    }
}
