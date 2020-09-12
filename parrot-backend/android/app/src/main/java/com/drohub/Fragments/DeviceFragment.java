package com.drohub.Fragments;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import com.drohub.DroHubHelper;
import com.drohub.IInfoDisplay;
import com.drohub.Models.DroHubDevice;
import com.drohub.Devices.ParrotDroneObserver;
import com.drohub.Devices.ParrotRCObserver;
import com.drohub.R;
import com.drohub.SnackBarInfoDisplay;

import java.net.URISyntaxException;

public class DeviceFragment extends BaseFragment {
    protected IInfoDisplay _error_display;
    protected DroHubDevice _connected_rc;
    protected DroHubDevice _connected_drone;

    protected ParrotRCObserver _parrot_rc_helper;
    protected ParrotDroneObserver _parrot_drone_helper;

    public DeviceFragment(int fragment_id) {
        super(fragment_id);
        _connected_drone = null;
        _connected_rc = null;
    }

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container,
                             Bundle savedInstanceState) {
        _view = super.onCreateView(inflater, container, savedInstanceState);

        String query_device_info_url;
        try {
            query_device_info_url = DroHubHelper.getURL(getContext(), R.string.query_device_info_url);
        } catch (URISyntaxException e) {
            throw new RuntimeException();
        }

        View root_view = getActivity().getWindow().getDecorView().findViewById(android.R.id.content);

        _error_display = new SnackBarInfoDisplay(root_view, 5000);
        _parrot_rc_helper = new ParrotRCObserver(this.getActivity(), this::onNewRC);
        _parrot_drone_helper = new ParrotDroneObserver(
                _error_display,
                query_device_info_url,
                _user_email,
                _user_auth_token,
                this.getActivity(),
                this::onNewDrone);
        return _view;
    }

    protected boolean isRCAndDronePairConnected() {
        return _connected_rc !=null && _connected_drone != null;
    }

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
    }

    protected void onNewRC(DroHubDevice rc) {
        if (rc.connection_state == DroHubDevice.ConnectionState.CONNECTED)
            _connected_rc = rc;
    }

    protected void onNewDrone(DroHubDevice drone) {
        if (drone.connection_state == DroHubDevice.ConnectionState.CONNECTED)
            _connected_drone = drone;
    }
}
