package com.drohub;

import androidx.annotation.Nullable;
import com.android.volley.Response;
import com.android.volley.toolbox.JsonObjectRequest;
import org.json.JSONObject;

import java.util.HashMap;
import java.util.Map;

public class DroHubObjectRequest extends JsonObjectRequest {
    private String _user_email;
    private String _user_auth_token;
    public DroHubObjectRequest(String user_email, String user_auth_token, int method, String url, @Nullable JSONObject jsonRequest,
                               Response.Listener<JSONObject> listener, @Nullable Response.ErrorListener error_listener,
                               CustomVolleyRetryPolicy.IRetryListener retry_listener) {
        super(method, url, jsonRequest, listener, error_listener);
        _user_email = user_email;
        _user_auth_token = user_auth_token;
        super.setRetryPolicy(new CustomVolleyRetryPolicy(5000, retry_listener));
    }

    @Override
    public Map<String, String> getHeaders() {
        Map<String, String>  params = new HashMap<>();
        params.put("x-drohub-user", _user_email);
        params.put("x-drohub-token", _user_auth_token);
        return params;
    }
}
