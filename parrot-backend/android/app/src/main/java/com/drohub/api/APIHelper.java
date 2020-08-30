package com.drohub.api;

import android.content.Intent;
import android.graphics.Color;
import android.view.View;
import androidx.annotation.Nullable;
import com.android.volley.Request;
import com.android.volley.Response;
import com.android.volley.VolleyError;
import com.drohub.*;
import org.json.JSONObject;

import static com.drohub.DroHubHelper.EXTRA_USER_AUTH_TOKEN;
import static com.drohub.DroHubHelper.EXTRA_USER_EMAIL;

public class APIHelper {

    protected final String _user_email;
    protected final String _user_auth_token;
    protected final View _snackbar_view;
    protected final VolleyHelper _volley;

    public APIHelper(View snackbar_view, String user_email, String user_auth_token) {
        _snackbar_view = snackbar_view;
        _user_auth_token = user_auth_token;
        _user_email = user_email;
        if ( _user_email == null || _user_auth_token == null)
            DroHubHelper.setStatusText(_snackbar_view, "User or token not set???");
        _volley = new VolleyHelper();
    }

    private void execute(DroHubObjectRequest request) {
        request.setShouldCache(false);
        _volley.getRequestQueue().add(request);
    }

    public void abortOnRetry(VolleyError retry_error, int retry_count) throws VolleyError {
        throw retry_error;
    }

    public void get(String url,
                    Response.Listener<JSONObject> listener, @Nullable Response.ErrorListener error_listener,
                    @Nullable CustomVolleyRetryPolicy.IRetryListener retry_listener) {
        if (retry_listener == null)
            retry_listener = this::abortOnRetry;

        execute(new DroHubObjectRequest(
                _user_email,
                _user_auth_token,
                Request.Method.GET,
                url,
                null,
                listener,
                error_listener,
                retry_listener)
        );
    }

    public void post(String url,
                    JSONObject json_request,
                    Response.Listener<JSONObject> listener, @Nullable Response.ErrorListener error_listener,
                    @Nullable CustomVolleyRetryPolicy.IRetryListener retry_listener) {

        if (retry_listener == null)
            retry_listener = this::abortOnRetry;

        execute(new DroHubObjectRequest(
                _user_email,
                _user_auth_token,
                Request.Method.POST,
                url,
                json_request,
                listener,
                error_listener,
                retry_listener)
        );
    }

}
