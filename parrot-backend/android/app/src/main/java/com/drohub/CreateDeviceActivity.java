package com.drohub;

import android.content.Intent;
import android.graphics.Color;
import android.graphics.drawable.ColorDrawable;
import android.util.Log;
import android.widget.Button;
import android.widget.TextView;

import android.os.Bundle;
import android.view.View;
import android.widget.EditText;
import androidx.appcompat.app.AppCompatActivity;
import com.android.volley.Request;
import com.android.volley.VolleyError;
import com.android.volley.toolbox.JsonObjectRequest;
import com.drohub.api.DroHubJSONObjectRequest;
import org.json.JSONException;
import org.json.JSONObject;

import static com.drohub.DroHubHelper.*;

public class CreateDeviceActivity extends AppCompatActivity {
    final private static String TAG = "CreateDeviceActivity";
    private final VolleyHelper _volley_helper;
    private EditText _device_name_input;

    private Button _create_button;
    private int original_create_button_color;

    private String _device_serial;
    private String _user_email;
    private String _user_token;
    private TextView status_view;

    public CreateDeviceActivity() {
        _volley_helper = new VolleyHelper();
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_create_device);

        status_view = findViewById(R.id.status_text);
        _create_button = findViewById(R.id.create_button);
        original_create_button_color = ((ColorDrawable)_create_button.getBackground()).getColor();

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
    }

    public void enableInput() {
        runOnUiThread(() -> {
            _device_name_input.setEnabled(true);
            _create_button.setEnabled(true);
            _create_button.setBackgroundColor(original_create_button_color);
        });
    }

    public void disableInput() {
        runOnUiThread(() -> {
            _device_name_input.setEnabled(false);
            _create_button.setEnabled(false);
            _create_button.setBackgroundColor(R.color.common_google_signin_btn_text_light_disabled);
        });
    }

    public void processCreateDeviceResponse(JSONObject response) {
        try {
            if (response.getString("result").equals("ok")) {
                setResult(RESULT_OK);
                finish();
            }
            else {
                setStatusText(status_view, "Failed to create device try again", Color.RED);
                enableInput();
                //TODO: Allow for retry
//                    findViewById(R.id.login_group).setVisibility(View.VISIBLE);
            }
        } catch (JSONException e) {
            setStatusText(status_view, "Unexpected answer" + response.toString(), Color.RED);
            enableInput();
        }
    }

    public void processCreateDeviceError(VolleyError error) {
        setStatusText(status_view, "Error Could not authenticate this mobile device", Color.RED);
        enableInput();
    }

    public void processCreateDeviceRetryError(VolleyError retry_error, int retry_count) throws VolleyError {
        if (retry_count == 3)
            throw retry_error;
        setStatusText(status_view,"Too slow response. Retrying again", Color.RED);
        enableInput();
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
            enableInput();
        }
        disableInput();
        setStatusText(status_view, "Registering new device...Please wait", Color.BLACK);
        JsonObjectRequest device_creation_request = new DroHubJSONObjectRequest(
                _user_email,
                _user_token,
                Request.Method.POST,
                url,
                request,
                this::processCreateDeviceResponse,
                this::processCreateDeviceError,
                this::processCreateDeviceRetryError);

        device_creation_request.setShouldCache(false);
        _volley_helper.getRequestQueue().add(device_creation_request);
    }


    protected void setStatusText(TextView status_view, String text, int color) {
        runOnUiThread(() -> {
            status_view.setText(text);
            status_view.setTextColor(color);
            status_view.setVisibility(View.VISIBLE);
        });
    }
}
