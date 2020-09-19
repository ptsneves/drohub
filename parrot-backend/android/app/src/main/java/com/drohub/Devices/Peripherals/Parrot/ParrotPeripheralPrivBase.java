package com.drohub.Devices.Peripherals.Parrot;

import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.Peripheral;

public abstract class ParrotPeripheralPrivBase<C extends Peripheral> implements ParrotPeripheralManager.PeripheralListener<C>, AutoCloseable {
    final Drone _drone;
    final Class<C> _peripheral_class;
    private ParrotPeripheralManager<C> _handle;

    protected ParrotPeripheralPrivBase(Drone drone, Class<C> peripheral_class) {
        _drone = drone;
        _peripheral_class = peripheral_class;
    }

    protected void start() throws IllegalStateException {
        if (_handle == null)
            _handle = new ParrotPeripheralManager<C>(_drone, _peripheral_class, this);
        else
            throw new IllegalStateException("Cannot call start more than once");
    }

    protected ParrotPeripheralManager.PeripheralListener<C> _peripheral_listener;

    protected void setPeripheralListener(ParrotPeripheralManager.PeripheralListener l) {
        if (_handle != null)
            throw new IllegalStateException("Cannot set peripheral listener after starting");
        _peripheral_listener = l;
    }

    public C get() throws IllegalAccessException {
        if (_handle == null)
            throw new IllegalAccessException("Cannot access non started Peripheral");
        return _handle.get();
    }

    @Override
    public void close() {
        if (_handle != null)
            _handle.close();
    }
}
