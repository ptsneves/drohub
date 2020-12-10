package com.drohub.api;

import androidx.annotation.NonNull;
import com.android.volley.Request;
import com.android.volley.VolleyError;
import com.drohub.IInfoDisplay;
import org.json.JSONException;
import org.json.JSONObject;


public class QueryDeviceInfoHelper extends APIHelper {
    public interface Listener {
        void onDeviceInfoResponse(JSONObject response);
        void onDeviceNotRegistered();
    }

    private final Listener _listener;
    private final String _serial;
    private final String _query_device_url;

    public QueryDeviceInfoHelper(IInfoDisplay display,
                                 @NonNull Listener listener,
                                 String query_device_url,
                                 String user_email,
                                 String user_auth_token,
                                 String serial) {
        super(display, user_email, user_auth_token);
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
            _display.addTemporarily( "Could not create a json query", 5000);
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
                    _display.addTemporarily(response.getString("error"), 5000);
                }
            } catch (JSONException e) {
                _display.addTemporarily("Error Could not Query device info.", 5000);
            }
        }
    }

    private void processQueryDeviceInfoError(VolleyError error) {
        if(error.networkResponse == null)
            _display.addTemporarily("No response. Are you connected to the internet?", 5000);
        else if (error.networkResponse.statusCode == 401)
            _display.addTemporarily("Unauthorized. Is your subscription or drone valid?", 5000);
        else
            _display.addTemporarily("Error Could not Query device info..", 5000);
    }

    private void processQueryDeviceInfoRetry(VolleyError error, int retry_count) throws VolleyError {
        if (retry_count == 3)
            throw error;
        _display.addTemporarily("Too slow response. Retrying again", 5000);
    }
}
