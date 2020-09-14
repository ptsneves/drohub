package com.drohub.Devices.Peripherals.Parrot;

import androidx.annotation.NonNull;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.parrot.drone.groundsdk.Ref;
import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.Peripheral;

class ParrotPeripheralManager<C extends Peripheral> implements AutoCloseable {
    interface PeripheralListener<C> {
        void onChange(@NonNull C c); //Will be called after first time available
        boolean onFirstTimeAvailable(@NonNull C c);

        static <C,O> ParrotPeripheralManager.PeripheralListener<C> convert(IPeripheral.IPeripheralListener<O> l, O instance){
            return new ParrotPeripheralManager.PeripheralListener<C>() {
                @Override
                public void onChange(C peripheral) {
                    l.onChange(instance);
                }

                @Override
                public boolean onFirstTimeAvailable(C peripheral) {
                    return l.onFirstTimeAvailable(instance);
                }
            };
        }
    }

    private boolean _valid_handle;
    final private Ref<C> _peripheral_handle;

    public ParrotPeripheralManager(final Drone drone, final Class<C> peripheral_class, final PeripheralListener<C> peripheral_listener) {
        _valid_handle = false;
        _peripheral_handle = drone.getPeripheral(peripheral_class, peripheral -> {
            if (peripheral == null)
                return;
            if (peripheral_listener != null) {
                if (!_valid_handle) {
                    _valid_handle = peripheral_listener.onFirstTimeAvailable(peripheral);
                    if (!_valid_handle)
                        return;
                }
                else
                    _valid_handle = true;


                peripheral_listener.onChange(peripheral);
            }
            else
                _valid_handle = true;
        });
    }

    @Override
    public void close() {
        if (_peripheral_handle != null)
            _peripheral_handle.close();
    }

    public C get() throws IllegalAccessException {
        if (_valid_handle)
            return _peripheral_handle.get();
        throw new IllegalAccessException("Handle is not valid");
    }

    public C get(long time_to_wait) throws IllegalAccessException {
        if (_valid_handle)
            return _peripheral_handle.get();

        long end_time = System.currentTimeMillis() + time_to_wait;
        do {
            try {
                Thread.sleep(300);
            } catch (InterruptedException e) {
                throw new IllegalAccessException("Sleep was interrupted. Giving up trying to get peripheral");
            }
            if (_valid_handle)
                return _peripheral_handle.get();
        } while (end_time > System.currentTimeMillis());

        throw new IllegalAccessException("Timeout waiting for peripheral handle");
    }
}
