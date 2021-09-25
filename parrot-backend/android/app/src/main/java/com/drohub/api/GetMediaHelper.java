package com.drohub.api;

import android.graphics.Bitmap;
import android.net.Uri;
import com.android.volley.Response;
import com.android.volley.VolleyError;
import com.drohub.Models.FileEntry;

public class GetMediaHelper extends APIHelper {
    public interface Listener {
        void onError(Exception e);
    }

    final private String _get_media_url;
    final private String _get_preview_url;
    final private Listener _listener;

    public GetMediaHelper(
            Listener listener,
            String user_email,
            String user_auth_token,
            String get_media_url,
            String get_preview_url) {

        super(user_email, user_auth_token);
        _get_media_url = get_media_url;
        _get_preview_url = get_preview_url;
        _listener = listener;
    }

    public void getMedia(FileEntry file, Response.Listener<Bitmap> listener) {
        Uri uri = Uri.parse(_get_media_url)
                .buildUpon()
                .appendQueryParameter("media_id", file.resource_id)
                .build();
        super.get(uri.toString(), listener, this::onError);
    }

    public void getPreview(FileEntry file, Response.Listener<Bitmap> listener) {
        Uri uri = Uri.parse(_get_preview_url)
                .buildUpon()
                .appendQueryParameter("media_id", file.resource_id)
                .build();
        super.get(uri.toString(), listener, this::onError);
    }

    private void onError(VolleyError error) {
        _listener.onError(error);
    }
}
