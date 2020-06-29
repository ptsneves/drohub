package com.drohub.ParrotHelpers;

public abstract class ParrotPeripheralPrivBase<C> implements ParrotPeripheralManager.PeripheralListener<C>  {
    protected ParrotPeripheralManager.PeripheralListener<C> _peripheral_listener;

    protected void setPeripheralListener(ParrotPeripheralManager.PeripheralListener l) {
        _peripheral_listener = l;
    }

}
