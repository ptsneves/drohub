package com.drohub.api;

import androidx.annotation.NonNull;
import com.android.volley.Request;
import com.android.volley.VolleyError;
import com.drohub.IInfoDisplay;
import org.json.JSONException;
import org.json.JSONObject;

public class ValidateTokenHelper {
    public interface Listener {
        void onValidToken();
        void onInvalidVersion();
        void onValidateTokenError(String error);
    }

    private final APIHelper _api_helper;
    private final double _rpc_api_version;
    private final Listener _listener;
    private final String _validate_token_url;
    private final String _WRONG_API_DESCRIPTION = "Application needs update";
    private final String _RESULT_KEY = "result";
    private final String _ERROR_KEY = "error";

    public ValidateTokenHelper(@NonNull Listener listener,
                               String validate_token_url,
                               String user_email,
                               String user_auth_token,
                               double rpc_api_version) {
        _api_helper = new APIHelper(user_email, user_auth_token);
        _rpc_api_version = rpc_api_version;
        _listener = listener;
        _validate_token_url = validate_token_url;
    }

    public void validateToken() {
        JSONObject json_req = new JSONObject();
        try {
            json_req.put("Version", _rpc_api_version);
        }
        catch (JSONException e) {
            _listener.onValidateTokenError("Could not create validate token json query");
            return;
        }

        DroHubJSONObjectRequest req = new DroHubJSONObjectRequest(
                _api_helper._user_email,
                _api_helper._user_auth_token,
                Request.Method.POST,
                _validate_token_url,
                json_req,
                this::processResponse,
                this::processError,
                this::processRetry);

        req.setShouldCache(false);
        _api_helper._volley.getRequestQueue().add(req);
    }

    private void processResponse(JSONObject response) {
        if (response.has(_RESULT_KEY)) {
            _listener.onValidToken();
        }
        else if (response.has(_ERROR_KEY)) {
            String error = response.optString(_ERROR_KEY);
            if (error.equals(_WRONG_API_DESCRIPTION))
                _listener.onInvalidVersion();
            else
                _listener.onValidateTokenError(error);
        }
        else {
            _listener.onValidateTokenError("Could not validate token. Re-log in");
        }
    }

    private void processError(VolleyError error) {
        _listener.onValidateTokenError(error.getMessage());
    }

    private void processRetry(VolleyError error, int retry_count) throws VolleyError {
        if (retry_count == 3)
            throw error;
        _listener.onValidateTokenError("Too slow response. Retrying again");
    }
}