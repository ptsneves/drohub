package com.drohub.Fragments;

import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.TextView;
import com.drohub.*;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.Models.FileEntry;
import com.drohub.api.GetSubscriptionMediaInfoHelper;

import java.net.URISyntaxException;
import java.util.List;

import static com.drohub.DroHubHelper.*;

public class MediaInfoCardFragment extends DeviceFragment {
    private final int _INVALID_VALUE = -1;
    private long _device_photos_count;
    private long _device_video_count;

//    private int _local_photos_count;
//    private int _local_video_count;

    private long _remote_photos_count;
    private long _remote_video_count;

    public MediaInfoCardFragment() {
        super(R.layout.fragment_media_info_card);
        _device_photos_count = _INVALID_VALUE;
        _device_video_count = _INVALID_VALUE;
//        _local_photos_count = _INVALID_VALUE;
//        _local_video_count = _INVALID_VALUE;
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


        final GetSubscriptionMediaInfoHelper media_info_helper = new GetSubscriptionMediaInfoHelper(
                error_display,
                error_message -> error_display.addTemporarily(error_message, 2000),
                _user_email,
                _user_auth_token,
                sub_info_url,
                media_store -> media_store.setNewMediaListener(this::onNewRemoteMedia)
        );
        _view.setOnClickListener(v -> {
            Intent intent = new Intent(this.getActivity(), GalleryActivity.class);
            intent.putExtra(EXTRA_USER_EMAIL, _user_email);
            intent.putExtra(EXTRA_USER_AUTH_TOKEN, _user_auth_token);
            startActivity(intent);
        });

        media_info_helper.get();
        showData();

        return _view;
    }

    private synchronized void showData() {
        TextView device_media_info_text_view = getFragmentViewById(R.id.device_media_info);
//        TextView local_media_info_text_view = getFragmentViewById(R.id.local_media_info);
        TextView remote_media_info_text_view = getFragmentViewById(R.id.remote_media_info);

        if (_device_photos_count == _INVALID_VALUE || _device_video_count == _INVALID_VALUE)
            device_media_info_text_view.setText("No info available");
        else {
            device_media_info_text_view.setText(String.format("%d Pictures and %d Videos",
                    _device_photos_count, _device_video_count));
        }

//        if (_local_photos_count == _INVALID_VALUE || _local_storage_usage_gb == _INVALID_VALUE
//                || _local_video_count == _INVALID_VALUE) {
//
//            local_media_info_text_view.setText("No info available");
//        }
//        else{
//            local_media_info_text_view.setText(String.format("%d Pictures and %d Videos",
//                    _local_photos_count, _local_video_count));
//        }

        if (_remote_photos_count == _INVALID_VALUE || _remote_video_count == _INVALID_VALUE) {
            remote_media_info_text_view.setText("No info available");
        }
        else {
            remote_media_info_text_view.setText(String.format("%d Pictures and %d Videos",
                    _remote_photos_count, _remote_video_count));
        }
    }

    private void onNewRemoteMedia(List<FileEntry> media_list) {
        _remote_photos_count = media_list.stream()
                .filter(media -> media.resource_type == FileEntry.FileResourceType.IMAGE)
                .count();
        _remote_video_count = media_list.stream()
                .filter(media -> media.resource_type == FileEntry.FileResourceType.VIDEO)
                .count();
        showData();
    }

    private void onNewDroneMedia(List<FileEntry> media_list) {
        _device_photos_count = media_list.stream()
            .filter(media -> media.resource_type == FileEntry.FileResourceType.IMAGE)
            .count();
        _device_video_count = media_list.stream()
                .filter(media -> media.resource_type == FileEntry.FileResourceType.VIDEO)
                .count();
        showData();
    }


    @Override
    public void onNewMediaStore(IPeripheral.IMediaStoreProvider media_store) {
        super.onNewMediaStore(media_store);
        media_store.setNewMediaListener(this::onNewDroneMedia);
    }
}

