package com.drohub;

import android.app.Activity;
import android.content.Intent;
import android.view.View;
import android.view.inputmethod.InputMethodManager;
import android.widget.TextView;
import androidx.annotation.NonNull;
import androidx.appcompat.app.AppCompatActivity;
import com.parrot.drone.sdkcore.ulog.ULogTag;

public class DroHubActivityBase  extends AppCompatActivity {
    @NonNull
    public static String withKey(@NonNull String key) {
        return "com.drohub.EXTRA_" + key;
    }

    public static final String DRONE_UID = withKey("DEVICE_UID");
    public static final String RC_UID = withKey("RC_UID");
    public static final String EXTRA_USER_AUTH_TOKEN = withKey("USER_AUTH_TOKEN");
    public static final String EXTRA_USER_EMAIL = withKey("USER_EMAIL");
    /** Logging tag. */
    protected static final ULogTag TAG = new ULogTag("DROHUB");

    public static void addThriftDataToIntent(Intent intent, String user_name, String token, String serial, String rc_serial) {
        intent.putExtra(DRONE_UID, serial);
        intent.putExtra(RC_UID, rc_serial);
        intent.putExtra(EXTRA_USER_EMAIL, user_name);
        intent.putExtra(EXTRA_USER_AUTH_TOKEN, token);
    }

    protected void setStatusText(TextView status_view, String text, int color) {
        runOnUiThread(() -> {
            status_view.setText(text);
            status_view.setTextColor(color);
            status_view.setVisibility(View.VISIBLE);
        });
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
}
