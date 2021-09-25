package com.drohub.Devices.Drone;

import android.app.Activity;
import android.graphics.Bitmap;
import androidx.annotation.NonNull;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.Devices.Peripherals.Parrot.ParrotMediaStore;
import com.drohub.IInfoDisplay;
import com.drohub.Models.DroHubDevice;
import com.drohub.api.QueryDeviceInfoHelper;
import com.parrot.drone.groundsdk.GroundSdk;
import com.parrot.drone.groundsdk.ManagedGroundSdk;
import com.parrot.drone.groundsdk.device.DeviceState;
import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.facility.AutoConnection;
import org.json.JSONException;
import org.json.JSONObject;

public class ParrotDrone implements IDrone{
    private final String PROVIDER_NAME = "ParrotDrone";

    final private GroundSdk _parrot_sdk;
    final private IDroneObserver _drone_observer;
    final private IPeripheral.IMediaStoreProvider.ProviderListener _media_store_provider_listener;

    public ParrotDrone(String query_device_info_url,
                       String user_mail,
                       String user_auth_token,
                       Activity activity,
                       @NonNull IDroneObserver drone_observer,
                       IPeripheral.IMediaStoreProvider.ProviderListener media_store_provider_listener) {
        _drone_observer = drone_observer;
        _media_store_provider_listener = media_store_provider_listener;
        _parrot_sdk = ManagedGroundSdk.obtainSession(activity);

        if (_parrot_sdk == null) {
            drone_observer.onError(new NullPointerException("Could not obtain ground sdk session"));
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

            processParrotDrone(query_device_info_url,
                    user_mail,
                    user_auth_token,
                    drone);
        });
    }

    synchronized private void processParrotDrone(String query_device_info_url,
                                                 String user_mail,
                                                 String user_auth_token,
                                                 Drone drone) {

        QueryDeviceInfoHelper device_info_helper = new QueryDeviceInfoHelper(
                new QueryDeviceInfoHelper.Listener() {
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
                            _drone_observer.onNewDrone(device);
                            if (device.connection_state == DroHubDevice.ConnectionState.CONNECTED) {
                                ParrotMediaStore media_store =
                                        new ParrotMediaStore(drone, 50, Bitmap.CompressFormat.JPEG);
                                _media_store_provider_listener.onNewMediaStore(media_store);
                                media_store.start();
                            }

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
                        _drone_observer.onNewDrone(device);
                        if (device.connection_state == DroHubDevice.ConnectionState.CONNECTED) {
                            ParrotMediaStore media_store =
                                    new ParrotMediaStore(drone, 50, Bitmap.CompressFormat.JPEG);
                            _media_store_provider_listener.onNewMediaStore(media_store);
                            media_store.start();
                        }
                    }

            @Override
            public void onDeviceQueryInfoError(String error) {
                _drone_observer.onError(new Exception(error));
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
