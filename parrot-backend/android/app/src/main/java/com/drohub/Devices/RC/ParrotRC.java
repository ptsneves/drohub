package com.drohub.Devices.RC;

import android.app.Activity;
import androidx.annotation.NonNull;
import com.drohub.Devices.Drone.ParrotDrone;
import com.drohub.Models.DroHubDevice;
import com.parrot.drone.groundsdk.GroundSdk;
import com.parrot.drone.groundsdk.ManagedGroundSdk;
import com.parrot.drone.groundsdk.device.RemoteControl;
import com.parrot.drone.groundsdk.facility.AutoConnection;

public class ParrotRC implements IRC {

    private GroundSdk _parrot_sdk;

    public ParrotRC(Activity activity, @NonNull IRCObserver listener) {
        final String PROVIDER_NAME = "ParrotRC";
        assert activity != null;

        _parrot_sdk = ManagedGroundSdk.obtainSession(activity);
        if (_parrot_sdk == null) {
            throw new NullPointerException("Could not obtain ground sdk session");
        }

        _parrot_sdk.getFacility(AutoConnection.class, auto_connection -> {
            if (auto_connection == null)
                return;

            if (auto_connection.getStatus() != AutoConnection.Status.STARTED)
                auto_connection.start();

            RemoteControl rc = auto_connection.getRemoteControl();
            if (rc == null)
                return;

            String model = rc.getModel().toString().replace("_", " ");
            DroHubDevice device = new DroHubDevice(
                    model,
                    rc.getUid(),
                    rc.getName(),
                    "",
                    PROVIDER_NAME,
                    ParrotDrone.getConnectionState(rc.getState().getConnectionState()),
                    false
            );

            listener.onNewRC(device);
        });
    }
}
