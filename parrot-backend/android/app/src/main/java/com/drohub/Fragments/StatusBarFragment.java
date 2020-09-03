package com.drohub.Fragments;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ImageView;
import android.widget.TextView;
import com.drohub.Models.DroHubDevice;
import com.drohub.R;

public class StatusBarFragment extends DeviceFragment {
    public StatusBarFragment() {
        super(R.layout.fragment_status_bar);
    }

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
        View ret = super.onCreateView(inflater, container, savedInstanceState);
        TextView text_view = getFragmentViewById(R.id.user_email);

        text_view.setText(_user_email);
        return ret;
    }

    @Override
    public void onNewRC(DroHubDevice rc) {
        super.onNewRC(rc);
        ImageView rc_icon = getFragmentViewById(R.id.rc_status);
        if (rc_icon == null)
            return;

        if (rc.connection_state == DroHubDevice.ConnectionState.CONNECTED)
            rc_icon.setImageResource(R.drawable.ic_video_info_controlsignal_connected);
        else
            rc_icon.setImageResource(R.drawable.ic_video_info_controlsignal_disconnected);

        _error_display.addTemporarily(String.format("%s is %s", rc.model, rc.connection_state.name()), 5000);
    }

    public void onNewDrone(DroHubDevice drone) {
        super.onNewDrone(drone);
        ImageView drone_icon= getFragmentViewById(R.id.drone_status);
        if (drone_icon == null)
            return;

        if (drone.connection_state == DroHubDevice.ConnectionState.CONNECTED)
            drone_icon.setImageResource(R.drawable.ic_device_id_drone_connected);
        else
            drone_icon.setImageResource(R.drawable.ic_device_id_drone_disconnected);

        _error_display.addTemporarily(String.format("%s is %s", drone.model, drone.connection_state.name()), 5000);
    }
}
