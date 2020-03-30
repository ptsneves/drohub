package com.drohub;

import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.os.Bundle;
import android.view.View;
import android.widget.AutoCompleteTextView;
import android.widget.EditText;
import android.widget.TextView;

import com.android.volley.Cache;
import com.android.volley.Network;
import com.android.volley.Request;
import com.android.volley.RequestQueue;
import com.android.volley.toolbox.BasicNetwork;
import com.android.volley.toolbox.DiskBasedCache;
import com.android.volley.toolbox.HurlStack;
import com.android.volley.toolbox.JsonObjectRequest;
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

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);
        status_view = findViewById(R.id.login_status);
        initializeVolley();
        _saved_accounts = getSharedPreferences(ACCOUNTS, MODE_PRIVATE);
        if (_saved_accounts == null)
            return;

        user_auth_token = _saved_accounts.getString(USER_AUTH_TOKEN_STORE_KEY, null);
        user_email = _saved_accounts.getString(USER_EMAIL_STORE_KEY, null);
        if (user_auth_token != null && user_email != null) {
            validateTokenAndLaunchIfPossible();
        }
    }

    private void initializeVolley() {
            _volley_cache = new DiskBasedCache(getCacheDir(), 1024); // 1kB cap
            _volley_network = new BasicNetwork(new HurlStack());
            _request_queue = new RequestQueue(_volley_cache, _volley_network);
            _request_queue.start();
    }

    private void validateDeviceRegisteredAndLaunchIfPossible() {
        if (_connected_drone == null || user_email == null || user_auth_token == null)
            return;
        String url = getString(R.string.drohub_url) + "/api/AndroidApplication/QueryDeviceInfo";
        JSONObject request = new JSONObject();
        try {
            request.put("UserName", user_email);
            request.put("Token", user_auth_token);
            request.put("DeviceSerialNumber", _connected_drone.getUid());
        }
         catch (JSONException e) {
            setStatusText(status_view,"Could not create a json query", Color.RED);
        }
        setStatusText(status_view,"Retrieving device info", Color.BLACK);
        JsonObjectRequest token_validation_request = new JsonObjectRequest(Request.Method.POST,
                url, request, response -> {
            if (!response.isNull("result")) { //We just care that there is something on the system
                Intent intent = new Intent(this, CopterHudActivity.class);
                addThriftDataToIntent(intent, user_email, user_auth_token, _connected_drone.getUid());
                this.startActivity(intent);
            }
            else {
                Intent intent = new Intent(this, CreateDeviceActivity.class);
                addThriftDataToIntent(intent, user_email, user_auth_token, _connected_drone.getUid());
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
        validateTokenAndLaunchIfPossible(); //racing with GUI validateAndLaunch
    }

    @Override
    protected void onDroneDisconnected() {
        _connected_drone = null;
    }

    private void validateTokenAndLaunchIfPossible() {
        String url = getString(R.string.drohub_url) + "/api/AndroidApplication/AuthenticateToken";
        JSONObject request = new JSONObject();
        try {
            request.put("UserName", user_email);
            request.put("Token", user_auth_token);
            setStatusText(status_view,"Validating token", Color.BLACK);
            JsonObjectRequest token_validation_request = new JsonObjectRequest(Request.Method.POST,
                    url, request, response -> {
                try {
                    if (!response.getString("result").equals("ok")) {
                        setStatusText(status_view,"Token Stored is invalid?", Color.RED);
                        return;
                    }
                } catch (JSONException e) {
                    setStatusText(status_view,"Unexpected answer" + response.toString(), Color.RED);
                }
                showWaitingScreen();
                validateDeviceRegisteredAndLaunchIfPossible();
            }, error -> setStatusText(status_view,"Error Could not authenticate token", Color.RED));
            token_validation_request.setShouldCache(false);
            _request_queue.add(token_validation_request);
        }
        catch (JSONException e) {
            setStatusText(status_view,"Could not create a json query", Color.RED);
        }
    }

    public void showWaitingScreen() {
        hideKeyboard(this);
        setStatusText(status_view,
                "Successfully authenticated user. Waiting for drone to be connected",
                Color.GREEN);
        findViewById(R.id.login_group).setVisibility(View.GONE);
    }

    public void tryPilotLogin(View view) {
        AutoCompleteTextView email_ctrl = findViewById(R.id.email_input);
        EditText password_ctrl = findViewById(R.id.password_input);


        user_email = email_ctrl.getText().toString();
        String password = password_ctrl.getText().toString();
        String url = getString(R.string.drohub_url) + "/api/AndroidApplication/GetApplicationToken";
        JSONObject request = new JSONObject();
        try {
            request.put("UserName", user_email);
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
                user_auth_token = result;
                SharedPreferences.Editor ed = _saved_accounts.edit();
                ed.putString(USER_EMAIL_STORE_KEY, user_email);
                ed.putString(USER_AUTH_TOKEN_STORE_KEY, user_auth_token);
                ed.commit();
                validateTokenAndLaunchIfPossible();
            }
        },
        error -> setStatusText(status_view,
                "An error occurred contacting Drohub servers" + request.toString(), Color.RED));

        login_request.setShouldCache(false);
        _request_queue.add(login_request);
    }
}
