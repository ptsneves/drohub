package com.drohub.api;

import androidx.annotation.Nullable;
import com.android.volley.Request;
import com.android.volley.Response;
import com.android.volley.VolleyError;
import com.drohub.*;
import org.json.JSONObject;

public class APIHelper {
    protected final IInfoDisplay _display;
    protected final String _user_email;
    protected final String _user_auth_token;
    protected final VolleyHelper _volley;

    public APIHelper(IInfoDisplay display, String user_email, String user_auth_token) {
        _display = display;
        _user_auth_token = user_auth_token;
        _user_email = user_email;
        if ( _user_email == null || _user_auth_token == null)
            _display.addTemporarily( "User or token not set???", 5000);
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
