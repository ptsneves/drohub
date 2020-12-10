package com.drohub.Fragments;

import android.content.Context;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.TextView;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.DroHubHelper;
import com.drohub.IInfoDisplay;
import com.drohub.R;
import com.drohub.SnackBarInfoDisplay;
import com.drohub.api.GetMediaSubscriptionInfoHelper;
import org.json.JSONObject;

import java.net.URISyntaxException;


public class MediaInfoCardFragment extends DeviceFragment {
    private final int _INVALID_VALUE = -1;
    private int _device_photos_count;
    private int _device_video_count;

    private int _local_photos_count;
    private int _local_video_count;
    private int _local_storage_usage_gb;

    private int _remote_photos_count;
    private int _remote_video_count;
    private int _remote_storage_usage_gb;

    public MediaInfoCardFragment() {
        super(R.layout.fragment_media_info_card);
        _device_photos_count = _INVALID_VALUE;
        _device_video_count = _INVALID_VALUE;
        _local_photos_count = _INVALID_VALUE;
        _local_video_count = _INVALID_VALUE;
        _remote_photos_count = _INVALID_VALUE;
        _remote_video_count = _INVALID_VALUE;
    }

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
    }

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container,
                             Bundle savedInstanceState) {

        super.onCreateView(inflater, container, savedInstanceState);

        final View root_view = getActivity().getWindow().getDecorView().findViewById(android.R.id.content);
        final IInfoDisplay error_display = new SnackBarInfoDisplay(root_view, 5000);

        String sub_info_url;
        try {
            Context context = getContext();
            if (context == null)
                throw new RuntimeException();
            sub_info_url = DroHubHelper.getURL(context, R.string.get_subscription_media_info_url);
        } catch (URISyntaxException e) {
            throw new RuntimeException();
        }


        final GetMediaSubscriptionInfoHelper media_info_helper = new GetMediaSubscriptionInfoHelper(
                error_display,
                this::onRemoteData,
                _user_email,
                _user_auth_token,
                sub_info_url
        );
        _view.setOnClickListener(v -> {
            error_display.addTemporarily("Media Info Refreshed.", 2000);
            media_info_helper.get();
        });

        showData();

        return _view;
    }

    private void showData() {
        TextView device_media_info_text_view = getFragmentViewById(R.id.device_media_info);
        TextView local_media_info_text_view = getFragmentViewById(R.id.local_media_info);
        TextView remote_media_info_text_view = getFragmentViewById(R.id.remote_media_info);

        if (_device_photos_count == _INVALID_VALUE || _device_video_count == -1)
            device_media_info_text_view.setText("No info available");
        else {
            device_media_info_text_view.setText(String.format("%d Pictures and %d Videos",
                    _device_photos_count, _device_video_count));
        }

        if (_local_photos_count == _INVALID_VALUE || _local_storage_usage_gb == _INVALID_VALUE
                || _local_video_count == _INVALID_VALUE) {

            local_media_info_text_view.setText("No info available");
        }
        else{
            local_media_info_text_view.setText(String.format("%d Pictures and %d Videos",
                    _local_photos_count, _local_video_count));
        }

        if (_remote_photos_count == _INVALID_VALUE || _remote_storage_usage_gb == _INVALID_VALUE
                ||_remote_video_count == _INVALID_VALUE) {
            remote_media_info_text_view.setText("No info available");
        }
        else {
            remote_media_info_text_view.setText(String.format("%d Pictures and %d Videos",
                    _remote_photos_count, _remote_video_count));
        }
    }

    public void onRemoteData(JSONObject response) {
        JSONObject result = response.optJSONObject("result");
        if (result == null)
            return;

        _remote_photos_count = result.optInt("image_count", _INVALID_VALUE);
        _remote_video_count = result.optInt("video_count", _INVALID_VALUE);
        _remote_storage_usage_gb = result.optInt("storage_in_gb", _INVALID_VALUE);
        showData();
    }

    @Override
    public void onMediaStore(IPeripheral.IMediaStoreProvider media_store) {
        _device_video_count = media_store.getVideos().size();
        _device_photos_count = media_store.getPhotos().size();
    }
}

