package com.drohub;

import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.os.Bundle;
import android.view.View;
import android.widget.EditText;
import android.widget.TextView;
import com.android.volley.*;
import com.android.volley.toolbox.BasicNetwork;
import com.android.volley.toolbox.DiskBasedCache;
import com.android.volley.toolbox.HurlStack;
import com.android.volley.toolbox.JsonObjectRequest;
import com.google.android.material.textfield.TextInputEditText;
import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.RemoteControl;
import org.json.JSONException;
import org.json.JSONObject;

public class MainActivity extends GroundSdkActivityBase {
    public static final String USER_EMAIL_STORE_KEY = "USER_NAME";
    public static final String USER_AUTH_TOKEN_STORE_KEY = "USER_AUTH_TOKEN";
    private static String TAG = "MainActivity";
    private static final String ACCOUNTS = "com.drohub.accounts";
    private final VolleyHelper _volley;
    private SharedPreferences _saved_accounts;
    private Drone _connected_drone;
    private RemoteControl _connected_rc;

    private TextView status_view;
    TextInputEditText email_ctrl;
    EditText password_ctrl;

    MainActivity() {
        super();
        _volley = new VolleyHelper(getCacheDir());
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);
        status_view = findViewById(R.id.login_status);

        email_ctrl = findViewById(R.id.email_input);
        password_ctrl = findViewById(R.id.password_input);
        _saved_accounts = getSharedPreferences(ACCOUNTS, MODE_PRIVATE);
        if (_saved_accounts == null)
            return;

        _user_auth_token = _saved_accounts.getString(USER_AUTH_TOKEN_STORE_KEY, null);
        _user_email = _saved_accounts.getString(USER_EMAIL_STORE_KEY, null);
        if (_user_auth_token != null && _user_email != null) {
            email_ctrl.setText(_user_email);
            validateDeviceRegisteredAndLaunchIfPossible();
        }
    }

    private void processQueryDeviceInfoResponse(JSONObject response) {
        if (_connected_drone == null) {
            hideLoginGroup();
            return;
        }
        if (!response.isNull("result")) {
            Intent intent = new Intent(this, CopterHudActivity.class);
            addThriftDataToIntent(intent, _user_email, _user_auth_token, _connected_drone.getUid(), _connected_rc.getUid());
            intent.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
            this.startActivity(intent);
            finish();
        }
        else if (response.has("error")) {
            try {
                if (response.getString("error").equalsIgnoreCase("Device does not exist.")) {
                    Intent intent = new Intent(this, CreateDeviceActivity.class);
                    addThriftDataToIntent(intent, _user_email, _user_auth_token, _connected_drone.getUid(), _connected_rc.getUid());
                    intent.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
                    this.startActivity(intent);
                    finish();
                } else {
                    setStatusText(status_view, response.getString("error"), Color.RED);
                    showLoginGroup();
                }
            } catch (JSONException e) {
                setStatusText(status_view, "Error Could not Query device info..", Color.RED);
                showLoginGroup();
            }
        }
    }

    private void processQueryDeviceInfoError(VolleyError error) {
        if(error.networkResponse == null)
            setStatusText(status_view,"No response. Are you connected to the internet?", Color.RED);
        else if (error.networkResponse.statusCode == 401)
            setStatusText(status_view,"Unauthorized. Is your subscription or drone valid?", Color.RED);
        else
            setStatusText(status_view,"Error Could not Query device info..", Color.RED);

        showLoginGroup();
    }

    private void processQueryDeviceInfoRetry(VolleyError error, int retry_count) throws VolleyError {
        if (retry_count == 3)
            throw error;
        setStatusText(status_view,"Too slow response. Retrying again " + error.getMessage(), Color.RED);
        showLoginGroup();
    }

    private void validateDeviceRegisteredAndLaunchIfPossible() {
        if ( _user_email == null || _user_auth_token == null)
            return;

        hideLoginGroup();
        String url = getString(R.string.drohub_url) + "/api/AndroidApplication/QueryDeviceInfo";
        if (_connected_drone == null) {
            setStatusText(status_view,
                    "Using stored credentials. Waiting for drone to be connected",
                    0xFF168849);
            return;
        }

        String device_serial =  _connected_drone.getUid();
        JSONObject request = new JSONObject();

        try {
            request.put("DeviceSerialNumber", device_serial);
        }
         catch (JSONException e) {
            setStatusText(status_view,"Could not create a json query", Color.RED);
            showLoginGroup();
        }

        setStatusText(status_view,"Retrieving device info", Color.BLACK);

        DroHubObjectRequest token_validation_request = new DroHubObjectRequest(_user_email, _user_auth_token,
                Request.Method.POST, url, request, response -> processQueryDeviceInfoResponse(response),
                error -> processQueryDeviceInfoError(error),
                (retry_error, retry_count) -> processQueryDeviceInfoRetry(retry_error, retry_count));

        token_validation_request.setShouldCache(false);
        _volley.getRequestQueue().add(token_validation_request);
    }

    @Override
    protected void onDroneConnected(Drone drone, RemoteControl rc) {
        _connected_drone = drone;
        _connected_rc = rc;
        validateDeviceRegisteredAndLaunchIfPossible();
    }

    @Override
    protected void onDroneDisconnected() {
        _connected_drone = null;
        _connected_rc = null;
    }

    public void showLoginGroup() {
        runOnUiThread(() -> findViewById(R.id.login_group).setVisibility(View.VISIBLE));
    }

    public void hideLoginGroup() {
        runOnUiThread(() -> {
            hideKeyboard(this);
            findViewById(R.id.login_group).setVisibility(View.GONE);
        });
    }

    public void tryPilotLogin(View view) {
        _user_email = email_ctrl.getText().toString();
        String password = password_ctrl.getText().toString();
        String url = getString(R.string.drohub_url) + "/api/GetToken/GetApplicationToken";
        JSONObject request = new JSONObject();

        if (_user_email.isEmpty() || password.isEmpty()) {
            setStatusText(status_view, "Email or password is empty.", Color.RED);
            return;
        }

        try {
            request.put("UserName", _user_email);
            request.put("Password", password);
        }
        catch (JSONException e) {
            setStatusText(status_view,"This is a bug. Please report", Color.RED);
        }


        hideLoginGroup();
        setStatusText(status_view,"Retrieving token...", Color.BLACK);
        JsonObjectRequest login_request = new JsonObjectRequest(Request.Method.POST,
                url, request, response ->
        {
            String result = null;
            try {
                result = response.getString("result");
            } catch (JSONException e) {
                setStatusText(status_view,"Unexpected server response" + response, Color.RED);
                showLoginGroup();
            }

            if (result.equals("nok")) {
                setStatusText(status_view,"Credentials provided are incorrect", Color.RED);
                showLoginGroup();
            }

            else {
                _user_auth_token = result;
                SharedPreferences.Editor ed = _saved_accounts.edit();
                ed.putString(USER_EMAIL_STORE_KEY, _user_email);
                ed.putString(USER_AUTH_TOKEN_STORE_KEY, _user_auth_token);
                ed.commit();
                setStatusText(status_view,
                        "Successfully authenticated user. Waiting for drone to be connected",
                        0xFF168849);
                validateDeviceRegisteredAndLaunchIfPossible();
            }
        },
        error -> {
            setStatusText(status_view,
                    "An error occurred contacting Drohub servers " + error.toString(), Color.RED);
            showLoginGroup();
        });

        login_request.setShouldCache(false);
        _volley.getRequestQueue().add(login_request);
    }
}
