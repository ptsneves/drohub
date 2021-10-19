package com.drohub.Fragments.GalleryFragment;

import android.content.Context;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.GridView;
import android.widget.ProgressBar;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.DroHubHelper;
import com.drohub.Fragments.DeviceFragment;
import com.drohub.Models.FileEntry;
import com.drohub.R;
import com.drohub.api.GetSubscriptionMediaInfoHelper;
import com.drohub.api.UploadMediaHelper;

import java.util.ArrayList;
import java.util.List;
import java.util.stream.Collectors;
import java.util.stream.Stream;

public class GalleryFragment extends DeviceFragment implements UploadMediaHelper.Listener {
    private GetSubscriptionMediaInfoHelper _subscription_media_info_helper;
    private ImageAdapter _image_adapter;
    private IPeripheral.IMediaStoreProvider _drohub_media_store;
    private IPeripheral.IMediaStoreProvider _drone_media_store;
    private List<FileEntry> _drohub_media_list;
    private List<FileEntry> _drone_media_list;

    public GalleryFragment() {
        super(R.layout.fragment_gallery);
        _drohub_media_list = new ArrayList<>();
        _drone_media_list = new ArrayList<>();
    }

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
        View ret = super.onCreateView(inflater, container, savedInstanceState);

        _image_adapter = new ImageAdapter(this.getContext(), _error_display);
        GridView _gallery_grid_view = getFragmentViewById(R.id.gridview);
        _gallery_grid_view.setAdapter(_image_adapter);
        Context context = getContext();
        if (context == null)
            _error_display.addTemporarily("Error getting context?", 2000);

        _subscription_media_info_helper = new GetSubscriptionMediaInfoHelper(
                error_message -> _error_display.addTemporarily(error_message, 2000),
                _user_email,
                _user_auth_token,
                DroHubHelper.getURL(getContext(), R.string.get_subscription_media_info_url),
                DroHubHelper.getURL(getContext(), R.string.get_media_url),
                DroHubHelper.getURL(getContext(), R.string.get_preview_url),
                drohub_media_store ->  {
                    _drohub_media_store = drohub_media_store;
                    _drohub_media_store.setNewMediaListener(media -> {
                        _drohub_media_list = drohub_media_store.getAllMedia();
                        _image_adapter.generateMediaList(_drohub_media_list, _drone_media_list);
                        if (_drone_media_store != null) {
                            Stream<FileEntry> files_to_upload = _drone_media_list.stream()
                                    .filter(drone_file -> !_drohub_media_list.contains(drone_file));
                            GalleryUploadMediaHelper gallery_upload_media_helper = new GalleryUploadMediaHelper(
                                    getContext(),
                                    _error_display,
                                    _user_email,
                                    _user_auth_token,
                                    files_to_upload.collect(Collectors.toList()),
                                    this
                                    );

                            gallery_upload_media_helper.upload();
                        }
                    });
                });

        _subscription_media_info_helper.get();
        if (!_connected_drone.is_registered)
            _error_display.add("Device not paired. Will not upload picture");
        return ret;
    }

    @Override
    public void onNewMediaStore(IPeripheral.IMediaStoreProvider media_store) {
        super.onNewMediaStore(_drone_media_store);
        _drone_media_store = media_store;
        _drone_media_store.setNewMediaListener(drone_media -> {
            _drone_media_list = _drone_media_store.getAllMedia();
            _image_adapter.generateMediaList(_drohub_media_list, _drone_media_list);
            _subscription_media_info_helper.get();
        });
    }

    @Override
    public void onSuccess(FileEntry f) {
        _error_display.addTemporarily("File Downloaded", 1000);
        ProgressBar progress_bar = getFragmentViewById(R.id.progress_bar);
        progress_bar.setVisibility(View.GONE);
    }

    @Override
    public void onUploadError(FileEntry f, String error) {
        _error_display.addTemporarily(error, 3000);
        ProgressBar progress_bar = getFragmentViewById(R.id.progress_bar);
        progress_bar.setVisibility(View.GONE);
    }

    @Override
    public boolean onProgress(FileEntry f, int percent) {
        ProgressBar progress_bar = getFragmentViewById(R.id.progress_bar);
        progress_bar.setProgress(percent);
        progress_bar.setVisibility(View.VISIBLE);
        return true;
    }
}
