package com.drohub.Models;

import androidx.annotation.NonNull;

public class DroHubDevice {
    public interface Provider {
        DroHubDevice getDevice();
    }

    public enum ConnectionState {
        CONNECTED,
        CONNECTING,
        DISCONNECTED,
        DISCONNECTING
    };

    public DroHubDevice(String model,
                        String serial,
                        String name,
                        String description,
                        String provider_name,
                        ConnectionState connection_state,
                        boolean is_registered) {
        this.model = model;
        this.serial = serial;
        this.name = name;
        this.description = description;
        this.provider_name = provider_name;
        this.connection_state = connection_state;
        this.is_registered = is_registered;
    }

    @NonNull
    final public String model;

    @NonNull
    final public String serial;

    @NonNull
    final public String name;

    @NonNull
    final public String description;

    @NonNull
    final public String provider_name;

    @NonNull
    final public ConnectionState connection_state;

    final public boolean is_registered;
}