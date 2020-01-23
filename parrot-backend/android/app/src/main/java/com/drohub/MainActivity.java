package com.drohub;

import android.os.Bundle;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;

import com.parrot.drone.groundsdk.device.Drone;

public class MainActivity extends GroundSdkActivityBase {
    private Drone mDrone;
    private WebView _web_view;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        _web_view = findViewById(R.id.webview);
        WebSettings webSettings = _web_view.getSettings();
        webSettings.setJavaScriptEnabled(true);
        _web_view.setWebContentsDebuggingEnabled(true);
        _web_view.loadUrl(this.getString(R.string.drohub_url));
        _web_view.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, String url){
                return false; // then it is not handled by default action
            }
        });
    }
}
