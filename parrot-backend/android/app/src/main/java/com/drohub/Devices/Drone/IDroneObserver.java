package com.drohub.Devices.Drone;

import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.Models.DroHubDevice;

public interface IDroneObserver {
    void onNewDrone(DroHubDevice drone);
    void onNewMediaStore(IPeripheral.IMediaStoreProvider media_store);
}
