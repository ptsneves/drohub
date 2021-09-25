package com.drohub.api;

import android.graphics.Bitmap;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import com.android.volley.Request;
import com.android.volley.Response;
import com.android.volley.VolleyError;
import com.drohub.*;
import org.json.JSONObject;

import java.util.HashMap;
import java.util.Map;

import javax.net.ssl.*;
import java.security.SecureRandom;
import java.security.cert.X509Certificate;

public class APIHelper {
    static Map<String, String> getHeaders(String user_email, String user_auth_token) {
        Map<String, String>  params = new HashMap<>();
        params.put("x-drohub-user", user_email);
        params.put("x-drohub-token", user_auth_token);
        return params;
    }

    protected final String _user_email;
    protected final String _user_auth_token;
    protected final VolleyHelper _volley;

    public APIHelper(@NonNull String user_email, @NonNull String user_auth_token) {
        _user_auth_token = user_auth_token;
        _user_email = user_email;
        _volley = new VolleyHelper();
    }

    private void execute(Request request) {
        request.setShouldCache(false);
        _volley.getRequestQueue().add(request);
    }

    public void abortOnRetry(VolleyError retry_error, int retry_count) throws VolleyError {
        throw retry_error;
    }

    public void get(String url,
                    Response.Listener<JSONObject> listener, @Nullable Response.ErrorListener error_listener,
                    @Nullable CustomVolleyRetryPolicy.IRetryListener retry_listener) {
        if (retry_listener == null)
            retry_listener = this::abortOnRetry;

        execute(new DroHubJSONObjectRequest(
                _user_email,
                _user_auth_token,
                Request.Method.GET,
                url,
                null,
                listener,
                error_listener,
                retry_listener)
        );
    }

    public void get(String url,
                    Response.Listener<Bitmap> listener,
                    @Nullable Response.ErrorListener error_listener) {

        execute(new DroHubImageRequest(
                _user_email,
                _user_auth_token,
                url,
                listener,
                error_listener,
                this::abortOnRetry));
    }

    public void post(String url,
                    JSONObject json_request,
                    Response.Listener<JSONObject> listener, @Nullable Response.ErrorListener error_listener,
                    @Nullable CustomVolleyRetryPolicy.IRetryListener retry_listener) {

        if (retry_listener == null)
            retry_listener = this::abortOnRetry;

        execute(new DroHubJSONObjectRequest(
                _user_email,
                _user_auth_token,
                Request.Method.POST,
                url,
                json_request,
                listener,
                error_listener,
                retry_listener)
        );
    }

    public static void ignoreSSLCerts() {
        try {
            TrustManager[] trustAllCerts = new TrustManager[] {
                    new X509TrustManager() {
                        public X509Certificate[] getAcceptedIssuers() {
                            X509Certificate[] myTrustedAnchors = new X509Certificate[0];
                            return myTrustedAnchors;
                        }

                        @Override
                        public void checkClientTrusted(X509Certificate[] certs, String authType) {}

                        @Override
                        public void checkServerTrusted(X509Certificate[] certs, String authType) {}
                    }
            };

            SSLContext sc = SSLContext.getInstance("SSL");
            sc.init(null, trustAllCerts, new SecureRandom());
            HttpsURLConnection.setDefaultSSLSocketFactory(sc.getSocketFactory());
            HttpsURLConnection.setDefaultHostnameVerifier(new HostnameVerifier() {
                @Override
                public boolean verify(String arg0, SSLSession arg1) {
                    return true;
                }
            });
        } catch (Exception e) {
        }
    }

}
