package com.drohub;

import android.app.Activity;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.os.Bundle;
import android.view.View;
import android.view.inputmethod.InputMethodManager;
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

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);
        initializeVolley();
        _saved_accounts = getSharedPreferences(ACCOUNTS, MODE_PRIVATE);
        if (_saved_accounts == null)
            return;

        user_auth_token = _saved_accounts.getString(USER_AUTH_TOKEN_STORE_KEY, null);
        user_email = _saved_accounts.getString(USER_EMAIL_STORE_KEY, null);
        if (user_auth_token != null && user_email != null) {
            validateAndLaunchIfPossible();
        }
    }

    private void initializeVolley() {
            _volley_cache = new DiskBasedCache(getCacheDir(), 1024); // 1kB cap
            _volley_network = new BasicNetwork(new HurlStack());
            _request_queue = new RequestQueue(_volley_cache, _volley_network);
            _request_queue.start();
    }

    private void launchCopterHudActivity() {
        if (_connected_drone == null || user_email == null || user_auth_token == null)
            return;

        Intent intent = new Intent(this, CopterHudActivity.class);
        intent.putExtra(EXTRA_DEVICE_UID, _connected_drone.getUid());
        intent.putExtra(EXTRA_USER_EMAIL, user_email);
        intent.putExtra(EXTRA_USER_AUTH_TOKEN, user_auth_token);
        this.startActivity(intent);
    }

    @Override
    protected void onDroneConnected(Drone drone) {
        _connected_drone = drone;
        validateAndLaunchIfPossible(); //racing with GUI validateAndLaunch
    }

    @Override
    protected void onDroneDisconnected() {
        _connected_drone = null;
    }

    private void setLoginStatusText(String text, int color) {
        TextView login_status = findViewById(R.id.login_status);
        login_status.setText(text);
        login_status.setTextColor(color);
        login_status.setVisibility(View.VISIBLE);
    }

    public static void hideKeyboard(Activity activity) {
        InputMethodManager imm = (InputMethodManager) activity.getSystemService(Activity.INPUT_METHOD_SERVICE);
        //Find the currently focused view, so we can grab the correct window token from it.
        View view = activity.getCurrentFocus();
        //If no view currently has focus, create a new one, just so we can grab a window token from it
        if (view == null) {
            view = new View(activity);
        }
        imm.hideSoftInputFromWindow(view.getWindowToken(), 0);
    }

    private void validateAndLaunchIfPossible() {
        String url = getString(R.string.drohub_url) + "/api/User/AuthenticateToken";
        JSONObject request = new JSONObject();
        try {
            request.put("UserName", user_email);
            request.put("Token", user_auth_token);
            setLoginStatusText("Validating token", Color.BLACK);
            JsonObjectRequest token_validation_request = new JsonObjectRequest(Request.Method.POST,
                    url, request, response -> {
                try {
                    if (!response.getString("result").equals("ok")) {
                        setLoginStatusText("Token Stored is invalid?", Color.RED);
                        findViewById(R.id.login_group).setVisibility(View.VISIBLE);
                        return;
                    }
                } catch (JSONException e) {
                    setLoginStatusText("Unexpected answer" + response.toString(), Color.RED);
                }

                showWaitingScreen();
                launchCopterHudActivity();
            }, error -> setLoginStatusText("Error Could not authenticate token", Color.RED));
            token_validation_request.setShouldCache(false);
            _request_queue.add(token_validation_request);
        }
        catch (JSONException e) {
            setLoginStatusText("Could not create a json query", Color.RED);
        }
    }

    public void showWaitingScreen() {
        hideKeyboard(this);
        setLoginStatusText(
                "Successfully authenticated user. Waiting for drone to be connected",
                Color.GREEN);
        findViewById(R.id.login_group).setVisibility(View.GONE);
    }

    public void tryPilotLogin(View view) {
        AutoCompleteTextView email_ctrl = findViewById(R.id.email_input);
        EditText password_ctrl = findViewById(R.id.password_input);


        user_email = email_ctrl.getText().toString();
        String password = password_ctrl.getText().toString();
        String url = getString(R.string.drohub_url) + "/api/User/GetApplicationToken";
        JSONObject request = new JSONObject();
        try {
            request.put("UserName", user_email);
            request.put("Password", password);
        }
        catch (JSONException e) {
            setLoginStatusText("The server returned an unexpected answer", Color.RED);
        }

        setLoginStatusText("Retrieving token...", Color.BLACK);
        JsonObjectRequest login_request = new JsonObjectRequest(Request.Method.POST,
                url, request, response ->
        {
            String result = null;
            try {
                result = response.getString("result");
            } catch (JSONException e) {
                setLoginStatusText("Unexpected server response" + response, Color.RED);
            }

            if (result.equals("nok")) {
                setLoginStatusText("Credentials provided are incorrect", Color.RED);
            }

            else {
                user_auth_token = result;
                SharedPreferences.Editor ed = _saved_accounts.edit();
                ed.putString(USER_EMAIL_STORE_KEY, user_email);
                ed.putString(USER_AUTH_TOKEN_STORE_KEY, user_auth_token);
                ed.commit();
                validateAndLaunchIfPossible();
            }
        },
        error -> setLoginStatusText("An error occurred contacting Drohub servers" + request.toString(), Color.RED));
        login_request.setShouldCache(false);
        _request_queue.add(login_request);
    }
}
