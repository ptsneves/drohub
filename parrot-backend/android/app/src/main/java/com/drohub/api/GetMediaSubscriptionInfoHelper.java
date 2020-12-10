package com.drohub.api;

import androidx.annotation.NonNull;
import com.android.volley.VolleyError;
import com.drohub.IInfoDisplay;
import org.json.JSONObject;

public class GetMediaSubscriptionInfoHelper extends APIHelper {
    public interface Listener {
        void onMediaSubscriptionInfoResponse(JSONObject response);
    }

    final private Listener _listener;
    final private String _url;
    final private IInfoDisplay _display;

    public GetMediaSubscriptionInfoHelper(IInfoDisplay display,
                                          @NonNull Listener listener,
                                          String user_email,
                                          String user_auth_token,
                                          String url) {

        super(display, user_email, user_auth_token);
        _listener = listener;
        _url = url;
        _display = display;
    }

    public void get() {
        super.get(_url, _listener::onMediaSubscriptionInfoResponse, this::onError, null);
    }

    private void onError(VolleyError error) {
        _display.addTemporarily("Error fetching DroHub media info", 5000);
    }
}
