package com.drohub.Devices.Peripherals;

import androidx.annotation.NonNull;


public interface IPeripheral<C> {
    public void setPeripheralListener(IPeripheralListener<C> l);
    public interface IPeripheralListener<C> {
        void onChange(@NonNull C c);
        boolean onFirstTimeAvailable(@NonNull C c);
    }
}
