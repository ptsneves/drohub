package com.drohub.Devices.Peripherals.DroHubWeb;

import android.graphics.Bitmap;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.Models.FileEntry;
import com.drohub.api.GetMediaHelper;

import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

public class DroHubGalleryMediaStore implements IPeripheral.IMediaStoreProvider {
    private final String _get_media_url;
    private final String _get_preview_url;
    private final String _user_email;
    private final String _user_auth_token;

    private List<FileEntry> _media = new ArrayList<>();
    private IPeripheral.OnNewMediaListener _media_listener;

    public DroHubGalleryMediaStore(String get_media_url, String get_preview_url, String user_email, String user_auth_token) {
        _get_media_url = get_media_url;
        _get_preview_url = get_preview_url;
        _user_email = user_email;
        _user_auth_token = user_auth_token;
    }

    public void setNewMedia(List<FileEntry> new_media) {
        _media = new_media;
        if (_media_listener != null)
            _media_listener.onChange(_media);
    }

    @Override
    public void setNewMediaListener(IPeripheral.OnNewMediaListener listener) {
        _media_listener = listener;
    }

    @Override
    public List<FileEntry> getAllMedia() {
        return _media;
    }

    @Override
    public void getThumbnail(FileEntry file, InputStreamListener listener) {
            GetMediaHelper helper = new GetMediaHelper(e -> listener.onError(file, e), _user_email, _user_auth_token,
                    _get_media_url, _get_preview_url);

            helper.getPreview(file, response -> {
                try {
                    listener.onAvailable(file, IPeripheral.IMediaStoreProvider
                            .convertToInputStream(response, Bitmap.CompressFormat.JPEG, 80));
                }
                catch (IllegalAccessException | IOException e) {
                    listener.onError(file, e);
                }
            });
    }

    @Override
    public void getMedia(FileEntry file, InputStreamListener listener) throws IllegalAccessException {
        throw new IllegalAccessException("Not implemented");
    }
}
