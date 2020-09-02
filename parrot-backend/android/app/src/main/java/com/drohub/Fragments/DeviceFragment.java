package com.drohub.Fragments;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import androidx.fragment.app.Fragment;
import com.drohub.DroHubHelper;
import com.drohub.IInfoDisplay;
import com.drohub.Models.DroHubDevice;
import com.drohub.ParrotHelpers.ParrotDroneObserver;
import com.drohub.ParrotHelpers.ParrotRCObserver;
import com.drohub.R;
import com.drohub.SnackBarInfoDisplay;

import java.net.URISyntaxException;

public class DeviceFragment extends Fragment {
    private final int _fragment_id;
    protected IInfoDisplay _error_display;
    protected DroHubDevice _connected_rc;
    protected DroHubDevice _connected_drone;

    protected View _view;
    protected String _user_auth_token;
    protected String _user_email;
    protected ParrotRCObserver _parrot_rc_helper;
    protected ParrotDroneObserver _parrot_drone_helper;

    public DeviceFragment(int fragment_id) {
        _fragment_id = fragment_id;
        _connected_drone = null;
        _connected_rc = null;
    }

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container,
                             Bundle savedInstanceState) {
        if (! (getActivity() instanceof DroHubHelper.CredentialGetters))
            throw new IllegalArgumentException();

        String query_device_info_url;
        try {
            query_device_info_url = DroHubHelper.getURL(getContext(), R.string.query_device_info_url);
        } catch (URISyntaxException e) {
            throw new RuntimeException();
        }

        View root_view = getActivity().getWindow().getDecorView().findViewById(android.R.id.content);

        _user_auth_token = ((DroHubHelper.CredentialGetters) getActivity()).getAuthToken();
        _user_email = ((DroHubHelper.CredentialGetters) getActivity()).getUserEmail();

        _view = inflater.inflate(_fragment_id, container, false);
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

    protected  <T> T getFragmentViewById(int id) {
        if (_view == null)
            return null;

        return (T)_view.findViewById(id);
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
