package com.drohub;

import android.accounts.Account;
import android.app.Activity;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.os.Bundle;
import android.view.View;
import android.view.inputmethod.InputMethodManager;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
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

import java.util.ArrayList;

public class MainActivity extends GroundSdkActivityBase implements AdapterView.OnItemClickListener {
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
        _saved_accounts = getSharedPreferences(ACCOUNTS, MODE_PRIVATE);
        if (_saved_accounts != null) {
            ArrayAdapter<String> adapter = new ArrayAdapter<>(this,
                    android.R.layout.select_dialog_item, new ArrayList<>(_saved_accounts.getAll().keySet()));
            AutoCompleteTextView email_ctrl = findViewById(R.id.email_input);
            email_ctrl.setThreshold(1);
            email_ctrl.setAdapter(adapter);
            email_ctrl.setOnItemClickListener(this);
        }

        _connected_drone = null;
        _volley_network = null;
        _volley_cache = null;
        _request_queue = null;
    }

    private void launchCopterHudActivity() {
        Intent intent = new Intent(this, CopterHudActivity.class);
        intent.putExtra(EXTRA_DEVICE_UID, _connected_drone.getUid());
        intent.putExtra(EXTRA_USER_EMAIL, user_email);
        intent.putExtra(EXTRA_USER_PASSWORD, password);
        this.startActivity(intent);
    }

    @Override
    protected void onDroneConnected(Drone drone) {
        _connected_drone = drone;
        if (password != null && user_email != null) {
            launchCopterHudActivity();
        }
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

    public void tryPilotLogin(View view) throws JSONException {
        if (_volley_cache == null) {
            _volley_cache = new DiskBasedCache(getCacheDir(), 1024); // 1kB cap
        }
        if (_volley_network == null) {
            _volley_network = new BasicNetwork(new HurlStack());
        }

        if (_request_queue == null) {
            _request_queue = new RequestQueue(_volley_cache, _volley_network);
            _request_queue.start();
        }
        AutoCompleteTextView email_ctrl = findViewById(R.id.email_input);
        EditText password_ctrl = findViewById(R.id.password_input);


        String email = email_ctrl.getText().toString();
        String password = password_ctrl.getText().toString();
        String url = getString(R.string.drohub_url) + "/api/User/Authenticate";
        JSONObject request = new JSONObject();
        request.put("UserName", email);
        request.put("Password", password);

        JsonObjectRequest login_request = new JsonObjectRequest(Request.Method.POST,
                url, request, (JSONObject response) -> {
            try {
                String result = response.getString("result");
                if (result.equals("ok")) {
                    this.user_email = email;
                    this.password = password;
                    SharedPreferences.Editor ed = _saved_accounts.edit();
                    ed.putString(email, password);
                    ed.commit();
                    hideKeyboard(this);
                    setLoginStatusText(
                            "Successfully authenticated user. Waiting for drone to be connected",
                            Color.GREEN);
                    findViewById(R.id.login_group).setVisibility(View.GONE);


                    if (_connected_drone != null) {
                        launchCopterHudActivity();
                    }
                }
                else if (result.equals("nok")) {
                    setLoginStatusText("Credentials provided are incorrect", Color.RED);
                }
                else {
                    setLoginStatusText("Unexpected answer from server: " + result, Color.RED);
                }
            } catch (JSONException e) {
                setLoginStatusText("The server returned an unexpected answer" + response.toString(), Color.RED);
            }
        },
        error -> setLoginStatusText("An error occurred contacting Drohub servers" + request.toString(), Color.RED));
        login_request.setShouldCache(false);
        _request_queue.add(login_request);
    }

    @Override
    public void onItemClick(AdapterView<?> parent, View view, int position, long id) {
        String item = parent.getItemAtPosition(position).toString();
        EditText password_ctrl = findViewById(R.id.password_input);
        password_ctrl.setText(_saved_accounts.getString(item, null));
    }
}
