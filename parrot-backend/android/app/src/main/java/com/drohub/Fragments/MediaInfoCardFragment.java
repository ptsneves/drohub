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
import com.drohub.Models.FileEntry;
import com.drohub.R;
import com.drohub.SnackBarInfoDisplay;
import com.drohub.api.GetMediaSubscriptionInfoHelper;
import org.json.JSONArray;
import org.json.JSONObject;

import java.net.URISyntaxException;
import java.util.Iterator;
import java.util.List;


public class MediaInfoCardFragment extends DeviceFragment {
    private final int _INVALID_VALUE = -1;
    private int _device_photos_count;
    private int _device_video_count;

//    private int _local_photos_count;
//    private int _local_video_count;

    private int _remote_photos_count;
    private int _remote_video_count;

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


        final GetMediaSubscriptionInfoHelper media_info_helper = new GetMediaSubscriptionInfoHelper(
                error_display,
                this::onRemoteData,
                _user_email,
                _user_auth_token,
                sub_info_url
        );
        _view.setOnClickListener(v -> {
            error_display.addTemporarily("Media Info Refreshed.", 2000);
            showData();
            media_info_helper.get();
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

    private void onRemoteData(JSONObject response) {
        int remote_photos = 0;
        int remote_videos = 0;

        JSONObject result = response.optJSONObject("result");
        if (result == null) {
            _error_display.addTemporarily("Unexpected gallery data", 2000);
            return;
        }

        Iterator<String> timestamp_keys = result.keys();
        while (timestamp_keys.hasNext()) {
            String timestamp_key = timestamp_keys.next();
            JSONObject device_files = result.optJSONObject(timestamp_key);
            Iterator<String> device_serial_keys = device_files.keys();

            while (device_serial_keys.hasNext()){
                String device_serial = device_serial_keys.next();
                JSONArray file_info_model_list = device_files.optJSONArray(device_serial);
                if (file_info_model_list instanceof JSONArray) {
                    int size = file_info_model_list.length();
                    for (int i = 0; i < size; i++) {
                        JSONObject file_info_model = file_info_model_list.optJSONObject(i);
                        if (!(file_info_model instanceof JSONObject))
                            continue;

                        JSONObject media_info_model = file_info_model.optJSONObject("media_object");
                        if (!(media_info_model instanceof JSONObject))
                            continue;

                        String media_path = media_info_model.optString("MediaPath");
                        if (!(media_path instanceof String))
                            continue;

                        if (!media_path.isEmpty())
                            System.out.println("sda");

                        if (media_path.endsWith(".jpeg"))
                            remote_photos++;
                        else if (media_path.endsWith(".webm") || media_path.endsWith(".mp4"))
                            remote_videos++;
                    }
                }
            }

        }
        _remote_photos_count = remote_photos;
        _remote_video_count = remote_videos;
        showData();
    }

    private void onNewPhotos(List<FileEntry> photos) {
        _device_photos_count = photos.size();
        showData();
    }

    private void onNewVideos(List<FileEntry> videos) {
        _device_video_count = videos.size();
        showData();
    }

    @Override
    public void onNewMediaStore(IPeripheral.IMediaStoreProvider media_store) {
        super.onNewMediaStore(media_store);
        media_store.setNewPhotosListener(this::onNewPhotos);
        media_store.setNewVideosListener(this::onNewVideos);
    }
}

