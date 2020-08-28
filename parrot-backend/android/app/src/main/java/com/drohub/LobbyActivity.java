package com.drohub;

import android.os.Bundle;
import androidx.appcompat.app.AppCompatActivity;

import static com.drohub.DroHubHelper.EXTRA_USER_AUTH_TOKEN;
import static com.drohub.DroHubHelper.EXTRA_USER_EMAIL;

public class LobbyActivity extends AppCompatActivity implements DroHubHelper.CredentialGetters {
    private String _user_email;
    private String _user_auth_token;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        _user_email = getIntent().getStringExtra(EXTRA_USER_EMAIL);
        _user_auth_token = getIntent().getStringExtra(EXTRA_USER_AUTH_TOKEN);
        setContentView(R.layout.activity_lobby);
    }

    @Override
    public String getUserEmail() {
        return _user_email;
    }

    @Override
    public String getAuthToken() {
        return _user_auth_token;
    }
}
