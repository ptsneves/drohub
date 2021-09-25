package com.drohub.api;

import android.view.View;
import androidx.annotation.NonNull;
import com.android.volley.VolleyError;
import com.drohub.IInfoDisplay;
import org.json.JSONObject;

public class GetSubscriptionInfoHelper extends APIHelper {
    public interface Listener {
        void onSubscriptionInfoResponse(JSONObject response);
        void onSubscriptionInfoError(String error);
    }

    final private Listener _listener;
    final private String _url;

    public GetSubscriptionInfoHelper(@NonNull Listener listener,
                                     String user_email,
                                     String user_auth_token,
                                     String url) {

        super(user_email, user_auth_token);
        _listener = listener;
        _url = url;
    }

    public void get() {
        super.get(_url, _listener::onSubscriptionInfoResponse, this::onError, null);
    }

    private void onError(VolleyError error) {
        _listener.onSubscriptionInfoError("Error fetching flight area info");
    }
}
