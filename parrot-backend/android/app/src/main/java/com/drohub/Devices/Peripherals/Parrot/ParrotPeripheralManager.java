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

    final private Ref<C> _peripheral_handle;

    public ParrotPeripheralManager(final Drone drone, final Class<C> peripheral_class, @NonNull final PeripheralListener<C> peripheral_listener) {
        if (peripheral_listener == null)
            throw new RuntimeException("Assertion of non null listener failed");
        _peripheral_handle = drone.getPeripheral(peripheral_class, peripheral -> {
            if (peripheral == null)
                return;

            peripheral_listener.onChange(peripheral);
        });
        C i = _peripheral_handle.get();
        if (i == null)
            throw new RuntimeException("Failed to get the peripheral");
        peripheral_listener.onFirstTimeAvailable(i);
    }

    @Override
    public void close() {
        if (_peripheral_handle != null)
            _peripheral_handle.close();
    }

    public C get(){
        return _peripheral_handle.get();
    }
}
