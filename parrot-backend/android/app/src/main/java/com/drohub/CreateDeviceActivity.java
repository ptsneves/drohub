package com.drohub;

import android.content.Intent;
import android.graphics.Color;
import android.util.Log;
import android.widget.TextView;

import android.os.Bundle;
import android.view.View;
import android.widget.EditText;
import com.android.volley.Cache;
import com.android.volley.Network;
import com.android.volley.Request;
import com.android.volley.RequestQueue;
import com.android.volley.toolbox.BasicNetwork;
import com.android.volley.toolbox.DiskBasedCache;
import com.android.volley.toolbox.HurlStack;
import com.android.volley.toolbox.JsonObjectRequest;
import org.json.JSONException;
import org.json.JSONObject;

public class CreateDeviceActivity extends DroHubActivityBase {
    private static String TAG = "CreateDeviceActivity";
    private EditText _device_name_input;
    private String _device_serial;
    private String _user_email;
    private String _user_token;
    private TextView status_view;
    private Cache _volley_cache;
    private Network _volley_network;
    private RequestQueue _request_queue;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_create_device);
        status_view = findViewById(R.id.status_text);
        _device_serial = getIntent().getStringExtra(DRONE_UID);
        _user_email = getIntent().getStringExtra(EXTRA_USER_EMAIL);
        _user_token = getIntent().getStringExtra(EXTRA_USER_AUTH_TOKEN);
        if (_device_serial == null || _user_email == null || _user_token == null) {
            Log.e(TAG, "Device uid, user or auth_token were not passed in intent");
            finish();
            return;
        }

        _device_name_input = findViewById(R.id.device_name_input);
        String hint_with_serial = _device_name_input.getHint().toString() + " " + _device_serial;
        _device_name_input.setHint(hint_with_serial);
        initializeVolley();
    }

    private void initializeVolley() {
        _volley_cache = new DiskBasedCache(getCacheDir(), 1024); // 1kB cap
        _volley_network = new BasicNetwork(new HurlStack());
        _request_queue = new RequestQueue(_volley_cache, _volley_network);
        _request_queue.start();
    }

    public void tryCreateDevice(View view) {
        String device_name = _device_name_input.getText().toString();
        String url = getString(R.string.drohub_url) + "/api/AndroidApplication/CreateDevice";
        JSONObject request = new JSONObject();
        try {
            JSONObject device = new JSONObject();
            device.put("SerialNumber", _device_serial);
            device.put("Name", device_name);
            request.put("Device", device);
        } catch (JSONException e) {
            setStatusText(status_view, "Could not create a json query", Color.RED);
        }

        JsonObjectRequest device_creation_request = new DroHubObjectRequest(_user_email, _user_token, Request.Method.POST,
                url, request, response -> {
            try {
                if (response.getString("result").equals("ok")) {
                    Intent intent = new Intent(this, MainActivity.class);
                    this.startActivity(intent);
                }
                else {
                    setStatusText(status_view, "Failed to create device try again", Color.RED);
                    //TODO: Allow for retry
//                    findViewById(R.id.login_group).setVisibility(View.VISIBLE);
                }
            } catch (JSONException e) {
                setStatusText(status_view, "Unexpected answer" + response.toString(), Color.RED);
            }
        }, error -> setStatusText(status_view, "Error Could not authenticate token", Color.RED));
        device_creation_request.setShouldCache(false);
        _request_queue.add(device_creation_request);
    }
}
