package com.drohub;

import android.content.Intent;
import android.graphics.Color;
import com.drohub.api.APIHelper;
import com.drohub.mock.InfoDisplayMock;
import org.json.JSONObject;

import androidx.test.platform.app.InstrumentationRegistry;
import androidx.test.ext.junit.runners.AndroidJUnit4;


import static com.drohub.DroHubHelper.EXTRA_USER_AUTH_TOKEN;
import static com.drohub.DroHubHelper.EXTRA_USER_EMAIL;

public class ValidateTokenTest {
    private final String _user_name;
    private final String _user_auth_token;
    public ValidateTokenTest() {
        _user_name = System.getenv("UserName");
        _user_auth_token = System.getenv("Token");
        if (_user_name == null || _user_auth_token == null)
            throw new IllegalStateException("UserName or Token env variables not set, but required.");
    }

    @Test
    public void ValidateTokenTest() {
        final InfoDisplayMock t = new InfoDisplayMock(100);
        APIHelper api_helper = new APIHelper(t, _user_name, _user_auth_token);
        JSONObject request = new JSONObject();
        api_helper.get(
                _validate_token_url,
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
}
