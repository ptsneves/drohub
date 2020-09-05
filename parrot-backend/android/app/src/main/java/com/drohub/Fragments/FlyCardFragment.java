package com.drohub.Fragments;

import android.content.Intent;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.TextView;
import com.drohub.*;
import com.drohub.Models.DroHubDevice;

import static android.app.Activity.RESULT_OK;
import static com.drohub.DroHubHelper.*;

public class FlyCardFragment extends DeviceFragment {
    private TextView text_view;

    public FlyCardFragment() {
        super(R.layout.fragment_fly_card);
    }

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
        super.onCreateView(inflater, container, savedInstanceState);
        text_view = getFragmentViewById(R.id.header);
        _view.setOnClickListener(v ->
                _error_display.addTemporarily("Please connect to an RC and DRONE.", 3000)
        );
        return _view;
    }

    private void startFlight() {
        text_view.setText("Starting flight mode. Please await");
        _view.setOnClickListener(v -> {});
        Intent intent = new Intent(this.getActivity(), CopterHudActivity.class);
        DroHubHelper.addThriftDataToIntent(intent, _user_email, _user_auth_token, _connected_drone.serial,
                _connected_rc.serial);

        intent.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
        getActivity().startActivity(intent);
        getActivity().finish();
    }

    private void registerDevice() {
        Intent intent = new Intent(this.getActivity(), CreateDeviceActivity.class);
        intent.putExtra(DRONE_UID, _connected_drone.serial);
        intent.putExtra(EXTRA_USER_EMAIL, _user_email);
        intent.putExtra(EXTRA_USER_AUTH_TOKEN, _user_auth_token);
        intent.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
        startActivityForResult(intent, CREATE_DEVICE_INTENT_RESULT);
    }

    @Override
    public void onActivityResult(int request_code, int result_code, Intent data) {
        super.onActivityResult(request_code,result_code, data);
        if(request_code == CREATE_DEVICE_INTENT_RESULT && result_code == RESULT_OK) {
            _connected_drone = new DroHubDevice(
                    _connected_drone.model,
                    _connected_drone.serial,
                    _connected_drone.name,
                    _connected_drone.description,
                    _connected_drone.provider_name,
                    _connected_drone.connection_state,
                    true);
            setFlyAbilityInfo();
        }
    }

    private void setFlyAbilityInfo() {
        if(text_view == null)
            return;

        if (isRCAndDronePairConnected()) {
            if (_connected_drone.is_registered) {
                text_view.setText("Ready to fly");
                _view.setOnClickListener(v -> startFlight());
            }
            else {
                text_view.setText("Drone not registered. Click to register");
                _view.setOnClickListener(v -> registerDevice());
            }
        }
        else {
            text_view.setText("Awaiting Drone Connection...");
            _view.setOnClickListener(v ->
                    _error_display.addTemporarily("Please connect to a DRONE.", 3000)
            );
        }
    }

    @Override
    public void onNewRC(DroHubDevice rc) {
        super.onNewRC(rc);
        setFlyAbilityInfo();
    }

    @Override
    public void onNewDrone(DroHubDevice drone) {
        super.onNewDrone(drone);
        setFlyAbilityInfo();
    }
}