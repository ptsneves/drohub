package com.drohub;

import android.Manifest;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.os.Bundle;
import android.text.Editable;
import android.view.View;
import android.widget.EditText;
import android.widget.TextView;
import android.widget.Toast;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import com.drohub.api.APIHelper;
import com.google.android.material.textfield.TextInputEditText;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.HashSet;
import java.util.Set;

import static com.drohub.DroHubHelper.EXTRA_USER_AUTH_TOKEN;
import static com.drohub.DroHubHelper.EXTRA_USER_EMAIL;

public class MainActivity extends AppCompatActivity {
    public static final String USER_EMAIL_STORE_KEY = "USER_NAME";
    public static final String USER_AUTH_TOKEN_STORE_KEY = "USER_AUTH_TOKEN";
    private static final String ACCOUNTS = "com.drohub.accounts";
    protected String _user_auth_token;
    protected String _user_email;
    private SharedPreferences _saved_accounts;

    private TextView status_view;
    TextInputEditText email_ctrl;
    EditText password_ctrl;


    private static final String[] PERMISSIONS_NEEDED = new String[] {
            Manifest.permission.ACCESS_NETWORK_STATE,
            Manifest.permission.WRITE_EXTERNAL_STORAGE, /* for ULog Recorder. */
            Manifest.permission.CAMERA, /* For HMD see-through. */
            Manifest.permission.INTERNET,
            Manifest.permission.ACCESS_FINE_LOCATION,
            Manifest.permission.RECORD_AUDIO,
    };

    /** Code for permission request result handling. */
    private static final int REQUEST_CODE_PERMISSIONS_REQUEST = 1;


    public MainActivity() {
        super();
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

        Set<String> permissionsToRequest = new HashSet<>();
        for (String permission : PERMISSIONS_NEEDED) {
            if (ContextCompat.checkSelfPermission(this, permission) != PackageManager.PERMISSION_GRANTED) {
                if (ActivityCompat.shouldShowRequestPermissionRationale(this, permission)) {
                    Toast.makeText(this, "Please allow permission " + permission, Toast.LENGTH_LONG).show();
                    finish();
                    return;
                } else {
                    permissionsToRequest.add(permission);
                }
            }
        }
        if (!permissionsToRequest.isEmpty()) {
            ActivityCompat.requestPermissions(this, permissionsToRequest.toArray(new String[0]),
                    REQUEST_CODE_PERMISSIONS_REQUEST);
        }


        _user_auth_token = _saved_accounts.getString(USER_AUTH_TOKEN_STORE_KEY, null);
        _user_email = _saved_accounts.getString(USER_EMAIL_STORE_KEY, null);
        email_ctrl.setText(_user_email);
        hideLoginGroup();

        if (_user_auth_token != null && _user_email != null) {
            validateAndLaunchLobbyActivity();
        }
    }

    private void validateAndLaunchLobbyActivity() {
        APIHelper api_helper = new APIHelper(findViewById(android.R.id.content), _user_email, _user_auth_token);
        api_helper.get(
                getString(R.string.validate_token_url),
                response -> {
                    email_ctrl.setText(_user_email);
                    Intent intent = new Intent(this, LobbyActivity.class);
                    intent.putExtra(EXTRA_USER_EMAIL, _user_email);
                    intent.putExtra(EXTRA_USER_AUTH_TOKEN, _user_auth_token);
                    intent.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
                    this.startActivity(intent);
                    finish();
                },
                error -> {
                    showLoginGroup();
                    setStatusText(status_view, "Token is not valid. Re-log in", Color.RED);
                },
                null);
    }

    protected void setStatusText(TextView status_view, String text, int color) {
        runOnUiThread(() -> {
            status_view.setText(text);
            status_view.setTextColor(color);
            status_view.setVisibility(View.VISIBLE);
        });
    }

    private void showLoginGroup() {
        runOnUiThread(() -> findViewById(R.id.login_group).setVisibility(View.VISIBLE));
    }

    private void hideLoginGroup() {
        runOnUiThread(() -> {
            DroHubHelper.hideKeyboard(this);
            findViewById(R.id.login_group).setVisibility(View.GONE);
        });
    }

    public void tryPilotLogin(View view) {
        Editable d = email_ctrl.getText();
        if (d == null) //for whatever reason it needs a null check, contrary to the password view
            return;
        _user_email = d.toString();


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
        APIHelper api_helper = new APIHelper(findViewById(android.R.id.content), _user_email, _user_auth_token);
        api_helper.post(url,
                request,
                response -> {
            if (response == null)
                return;

            String result;
            try {
                result = response.getString("result");
            } catch (JSONException e) {
                setStatusText(status_view,"Unexpected server response" + response, Color.RED);
                showLoginGroup();
                return;
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

                Bundle bundle = new Bundle();
                bundle.putString(USER_EMAIL_STORE_KEY, _user_email);
                bundle.putString(USER_AUTH_TOKEN_STORE_KEY, _user_auth_token);

                setStatusText(status_view,
                        "Successfully authenticated user. Waiting for drone to be connected",
                        0xFF168849);

                validateAndLaunchLobbyActivity();
            }
        },
        error -> {
            setStatusText(status_view,
                    "An error occurred contacting Drohub servers " + error.toString(), Color.RED);
            showLoginGroup();
        },
        null);
    }
}
