package com.drohub.ParrotHelpers;

import android.app.Activity;
import android.view.View;
import androidx.annotation.NonNull;
import com.drohub.IInfoDisplay;
import com.drohub.InfoDisplayBase;
import com.drohub.Models.DroHubDevice;
import com.drohub.api.QueryDeviceInfoHelper;
import com.parrot.drone.groundsdk.GroundSdk;
import com.parrot.drone.groundsdk.ManagedGroundSdk;
import com.parrot.drone.groundsdk.device.DeviceState;
import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.facility.AutoConnection;
import org.json.JSONException;
import org.json.JSONObject;

public class ParrotDroneObserver {
    public interface Listener {
        void onNewDrone(DroHubDevice drone);
    }

    private final String PROVIDER_NAME = "ParrotDrone";

    private GroundSdk _parrot_sdk;
    private Listener _listener;

    public ParrotDroneObserver(IInfoDisplay display,
                               String query_device_info_url,
                               String user_mail,
                               String user_auth_token,
                               Activity activity, @NonNull Listener listener) {
        _listener = listener;
        _parrot_sdk = ManagedGroundSdk.obtainSession(activity);

        if (_parrot_sdk == null) {
            throw new NullPointerException("Could not obtain ground sdk session");
        }

        _parrot_sdk.getFacility(AutoConnection.class, auto_connection -> {
            if (auto_connection == null) {
                return;
            }

            if (auto_connection.getStatus() != AutoConnection.Status.STARTED) {
                auto_connection.start();
            }

            Drone drone = auto_connection.getDrone();

            if (drone == null)
                return;

            processParrotDrone(display,
                    query_device_info_url,
                    user_mail,
                    user_auth_token,
                    drone);
        });
    }

    synchronized private void processParrotDrone(IInfoDisplay display,
                                                 String query_device_info_url,
                                                 String user_mail,
                                                 String user_auth_token,
                                                 Drone drone) {

        QueryDeviceInfoHelper device_info_helper = new QueryDeviceInfoHelper(
                display, new QueryDeviceInfoHelper.Listener() {
                    @Override
                    public void onDeviceInfoResponse(JSONObject response) {
                        try {
                            String name = "No name";
                            if (response.has("result")) {
                                JSONObject jo = response.getJSONObject("result");
                                name = jo.getString("name");
                            }
                            DroHubDevice device = new DroHubDevice(
                                    drone.getModel().toString(),
                                    drone.getUid(),
                                    name,
                                    "No Info for description",
                                    PROVIDER_NAME,
                                    getConnectionState(drone.getState().getConnectionState()),
                                    true
                            );
                            _listener.onNewDrone(device);
                        } catch (JSONException e) {
                            e.printStackTrace();
                        }
                    }

                    @Override
                    public void onDeviceNotRegistered() {
                        DroHubDevice device = new DroHubDevice(
                                drone.getModel().toString(),
                                drone.getUid(),
                                drone.getName(),
                                "No description available for unregistered device",
                                PROVIDER_NAME,
                                getConnectionState(drone.getState().getConnectionState()),
                                false
                        );
                        _listener.onNewDrone(device);
                    }
                },
                query_device_info_url,
                user_mail,
                user_auth_token,
                drone.getUid()
        );
        device_info_helper.validateDeviceRegistered();
    }

    public static DroHubDevice.ConnectionState getConnectionState(DeviceState.ConnectionState state) {
        switch (state) {
            case DISCONNECTED:
                return DroHubDevice.ConnectionState.DISCONNECTED;
            case CONNECTING:
                return DroHubDevice.ConnectionState.CONNECTING;
            case CONNECTED:
                return DroHubDevice.ConnectionState.CONNECTED;
            case DISCONNECTING:
                return DroHubDevice.ConnectionState.DISCONNECTING;
        }
        return DroHubDevice.ConnectionState.DISCONNECTED;
    }
}
