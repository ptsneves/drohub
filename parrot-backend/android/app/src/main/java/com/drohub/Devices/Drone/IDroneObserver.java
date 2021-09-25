package com.drohub.Devices.Drone;

import com.drohub.Models.DroHubDevice;

public interface IDroneObserver {
    void onNewDrone(DroHubDevice drone);
    void onError(Exception e);
}
