package com.drohub.ParrotHelpers.Peripherals;

import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.Peripheral;

public abstract class ParrotPeripheralPrivBase<C extends Peripheral> implements ParrotPeripheralManager.PeripheralListener<C> {
    final protected ParrotPeripheralManager<C> _handle;
    protected ParrotPeripheralPrivBase(Drone drone, Class<C> peripheral_class) {
        _handle = new ParrotPeripheralManager<C>(drone, peripheral_class, this);
    }
    protected ParrotPeripheralManager.PeripheralListener<C> _peripheral_listener;

    protected void setPeripheralListener(ParrotPeripheralManager.PeripheralListener l) {
        _peripheral_listener = l;
    }

    public C get() throws IllegalAccessException {
        return _handle.get();
    }
}
