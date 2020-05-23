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

import org.json.JSONException;
import org.json.JSONObject;

public class MainActivity extends GroundSdkActivityBase {
    public static final String USER_EMAIL_STORE_KEY = "USER_NAME";
    public static final String USER_AUTH_TOKEN_STORE_KEY = "USER_AUTH_TOKEN";
    private static String TAG = "MainActivity";
    private static final String ACCOUNTS = "com.drohub.accounts";
    private SharedPreferences _saved_accounts;
    private Drone _connected_drone;
    private Cache _volley_cache;
    private Network _volley_network;
    private RequestQueue _request_queue;
    private TextView status_view;
    TextInputEditText email_ctrl;
    EditText password_ctrl;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);
        status_view = findViewById(R.id.login_status);

        email_ctrl = findViewById(R.id.email_input);
        password_ctrl = findViewById(R.id.password_input);
        initializeVolley();
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

    private void initializeVolley() {
            _volley_cache = new DiskBasedCache(getCacheDir(), 1024); // 1kB cap
            _volley_network = new BasicNetwork(new HurlStack());
            _request_queue = new RequestQueue(_volley_cache, _volley_network);
            _request_queue.start();
    }

    private void validateDeviceRegisteredAndLaunchIfPossible() {
        if ( _user_email == null || _user_auth_token == null)
            return;

        String url = getString(R.string.drohub_url) + "/api/AndroidApplication/QueryDeviceInfo";
        String device_serial =  _connected_drone == null ? "NODEVICE" : _connected_drone.getUid();
        JSONObject request = new JSONObject();
        try {
            request.put("DeviceSerialNumber", device_serial);
        }
         catch (JSONException e) {
            setStatusText(status_view,"Could not create a json query", Color.RED);
        }
        setStatusText(status_view,"Retrieving device info", Color.BLACK);
        DroHubObjectRequest token_validation_request = new DroHubObjectRequest(_user_email, _user_auth_token,
                Request.Method.POST, url, request, response -> {
            if (_connected_drone == null) {
                showWaitingScreen();
                return;
            }
            if (!response.isNull("result")) { //We just care that there is something on the system
                Intent intent = new Intent(this, CopterHudActivity.class);
                addThriftDataToIntent(intent, _user_email, _user_auth_token, _connected_drone.getUid());
                this.startActivity(intent);
            }
            else {
                Intent intent = new Intent(this, CreateDeviceActivity.class);
                addThriftDataToIntent(intent, _user_email, _user_auth_token, _connected_drone.getUid());
                this.startActivity(intent);
            }
        }, error -> {
            setStatusText(status_view,"Error Could not Query device info..", Color.RED);
            findViewById(R.id.login_group).setVisibility(View.VISIBLE);
        });
        token_validation_request.setShouldCache(false);
        _request_queue.add(token_validation_request);
    }

    @Override
    protected void onDroneConnected(Drone drone) {
        _connected_drone = drone;
        validateDeviceRegisteredAndLaunchIfPossible();
    }

    @Override
    protected void onDroneDisconnected() {
        _connected_drone = null;
    }


    public void showWaitingScreen() {
        hideKeyboard(this);
        setStatusText(status_view,
                "Successfully authenticated user. Waiting for drone to be connected",
                0xFF168849);
        findViewById(R.id.login_group).setVisibility(View.GONE);
    }

    public void tryPilotLogin(View view) {
        _user_email = email_ctrl.getText().toString();
        String password = password_ctrl.getText().toString();
        String url = getString(R.string.drohub_url) + "/api/GetToken/GetApplicationToken";
        JSONObject request = new JSONObject();
        try {
            request.put("UserName", _user_email);
            request.put("Password", password);
        }
        catch (JSONException e) {
            setStatusText(status_view,"The server returned an unexpected answer", Color.RED);
        }

        setStatusText(status_view,"Retrieving token...", Color.BLACK);
        JsonObjectRequest login_request = new JsonObjectRequest(Request.Method.POST,
                url, request, response ->
        {
            String result = null;
            try {
                result = response.getString("result");
            } catch (JSONException e) {
                setStatusText(status_view,"Unexpected server response" + response, Color.RED);
            }

            if (result.equals("nok")) {
                setStatusText(status_view,"Credentials provided are incorrect", Color.RED);
            }

            else {
                _user_auth_token = result;
                SharedPreferences.Editor ed = _saved_accounts.edit();
                ed.putString(USER_EMAIL_STORE_KEY, _user_email);
                ed.putString(USER_AUTH_TOKEN_STORE_KEY, _user_auth_token);
                ed.commit();
                showWaitingScreen();
                validateDeviceRegisteredAndLaunchIfPossible();
            }
        },
        error -> setStatusText(status_view,
                "An error occurred contacting Drohub servers" + request.toString(), Color.RED));

        login_request.setShouldCache(false);
        _request_queue.add(login_request);
    }
}
