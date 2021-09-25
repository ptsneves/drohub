package com.drohub.api;

import androidx.annotation.NonNull;
import com.android.volley.Request;
import com.android.volley.VolleyError;
import org.json.JSONException;
import org.json.JSONObject;


public class QueryDeviceInfoHelper extends APIHelper {
    public interface Listener {
        void onDeviceInfoResponse(JSONObject response);
        void onDeviceNotRegistered();
        void onDeviceQueryInfoError(String error);
    }

    private final Listener _listener;
    private final String _serial;
    private final String _query_device_url;

    public QueryDeviceInfoHelper(@NonNull Listener listener,
                                 String query_device_url,
                                 String user_email,
                                 String user_auth_token,
                                 String serial) {
        super(user_email, user_auth_token);
        _listener = listener;
        _serial = serial;
        _query_device_url = query_device_url;
    }

    public void validateDeviceRegistered() {
        JSONObject request = new JSONObject();
        try {
            request.put("DeviceSerialNumber", _serial);
        }
        catch (JSONException e) {
            _listener.onDeviceQueryInfoError("Could not create a json query");
            return;
        }

        DroHubJSONObjectRequest token_validation_request = new DroHubJSONObjectRequest(
                _user_email,
                _user_auth_token,
                Request.Method.POST,
                _query_device_url,
                request, this::processQueryDeviceInfoResponse,
                this::processQueryDeviceInfoError,
                this::processQueryDeviceInfoRetry);

        token_validation_request.setShouldCache(false);
        _volley.getRequestQueue().add(token_validation_request);
    }

    private void processQueryDeviceInfoResponse(JSONObject response) {
        if (!response.isNull("result")) {
            _listener.onDeviceInfoResponse(response);
        }
        else if (response.has("error")) {
            try {
                if (response.getString("error").equalsIgnoreCase("Device does not exist.")) {
                    _listener.onDeviceNotRegistered();
                } else {
                    _listener.onDeviceQueryInfoError(response.getString("error"));
                }
            } catch (JSONException e) {
                _listener.onDeviceQueryInfoError("Error Could not Query device info.");
            }
        }
    }

    private void processQueryDeviceInfoError(VolleyError error) {
        if(error.networkResponse == null)
            _listener.onDeviceQueryInfoError("No response. Are you connected to the internet?");
        else if (error.networkResponse.statusCode == 401)
            _listener.onDeviceQueryInfoError("Unauthorized. Is your subscription or drone valid?");
        else
            _listener.onDeviceQueryInfoError("Error Could not Query device info..");
    }

    private void processQueryDeviceInfoRetry(VolleyError error, int retry_count) throws VolleyError {
        if (retry_count == 3)
            throw error;
        _listener.onDeviceQueryInfoError("Too slow response. Retrying again");
    }
}
