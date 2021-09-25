package com.drohub.api;

import android.graphics.Bitmap;
import android.widget.ImageView;
import androidx.annotation.Nullable;
import com.android.volley.Response;
import com.android.volley.toolbox.ImageRequest;

import java.util.Map;

public class DroHubImageRequest extends ImageRequest {
    private String _user_email;
    private String _user_auth_token;
    public DroHubImageRequest(String user_email, String user_auth_token, String url,
                                   Response.Listener<Bitmap> listener, @Nullable Response.ErrorListener error_listener,
                                   CustomVolleyRetryPolicy.IRetryListener retry_listener) {
        super(url, listener,0,0, ImageView.ScaleType.CENTER, Bitmap.Config.ARGB_8888, error_listener);
        _user_email = user_email;
        _user_auth_token = user_auth_token;
        super.setRetryPolicy(
                new CustomVolleyRetryPolicy(5000, retry_listener));
    }

    @Override
    public Map<String, String> getHeaders() {
        return APIHelper.getHeaders(_user_email, _user_auth_token);
    }
}