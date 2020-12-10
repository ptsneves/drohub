package com.drohub.Devices.RC;

import com.drohub.Models.DroHubDevice;

public interface IRCObserver {
    void onNewRC(DroHubDevice rc);
}