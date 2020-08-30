package com.drohub.api;

import android.view.View;
import androidx.annotation.NonNull;
import com.android.volley.Request;
import com.android.volley.VolleyError;
import com.drohub.DroHubHelper;
import com.drohub.DroHubObjectRequest;
import com.drohub.VolleyHelper;
import org.json.JSONException;
import org.json.JSONObject;


public class QueryDeviceInfoHelper {
    public interface Listener {
        void onDeviceInfoResponse(JSONObject response);
        void onDeviceNotRegistered();
    }

    private final View _snackbar_view;
    private final Listener _listener;
    private final String _user_email;
    private final String _user_auth_token;
    private final String _serial;
    private final String _query_device_url;
    private final VolleyHelper _volley;

    public QueryDeviceInfoHelper(View snackbar_info_view,
                                 @NonNull Listener listener,
                                 String query_device_string,
                                 String user_email,
                                 String user_auth_token,
                                 String serial) {

        _snackbar_view = snackbar_info_view;
        _listener = listener;
        _user_email = user_email;
        _user_auth_token = user_auth_token;
        _serial = serial;
        _query_device_url = query_device_string;
        _volley = new VolleyHelper();
    }

    public void validateDeviceRegistered() {
        if ( _user_email == null || _user_auth_token == null)
            DroHubHelper.setStatusText(_snackbar_view, "User or token not set???");

        JSONObject request = new JSONObject();
        try {
            request.put("DeviceSerialNumber", _serial);
        }
        catch (JSONException e) {
            DroHubHelper.setStatusText(_snackbar_view, "Could not create a json query");
            return;
        }

        DroHubObjectRequest token_validation_request = new DroHubObjectRequest(
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
                    DroHubHelper.setStatusText(_snackbar_view, response.getString("error"));
                }
            } catch (JSONException e) {
                DroHubHelper.setStatusText(_snackbar_view, "Error Could not Query device info.");
            }
        }
    }

    private void processQueryDeviceInfoError(VolleyError error) {
        if(error.networkResponse == null)
            DroHubHelper.setStatusText(_snackbar_view,"No response. Are you connected to the internet?");
        else if (error.networkResponse.statusCode == 401)
            DroHubHelper.setStatusText(_snackbar_view,"Unauthorized. Is your subscription or drone valid?");
        else
            DroHubHelper.setStatusText(_snackbar_view,"Error Could not Query device info..");
    }

    private void processQueryDeviceInfoRetry(VolleyError error, int retry_count) throws VolleyError {
        if (retry_count == 3)
            throw error;
        DroHubHelper.setStatusText(_snackbar_view,"Too slow response. Retrying again");
    }
}
