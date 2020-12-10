package com.drohub.api;

import com.android.volley.RetryPolicy;
import com.android.volley.VolleyError;
import org.jetbrains.annotations.Nullable;

public class CustomVolleyRetryPolicy implements RetryPolicy {
    public interface IRetryListener {
        void onRetry(VolleyError retry_error, int retry_count) throws VolleyError;
    }

    @Nullable
    final private IRetryListener _retry_callback;

    final private int _timeout_ms;

    private int _retry_count;

    CustomVolleyRetryPolicy(int timeout_ms, @Nullable IRetryListener retry_callback) {
        _timeout_ms = timeout_ms;
        _retry_count = 0;
        _retry_callback = retry_callback;
    }

    @Override
    public int getCurrentTimeout() {
        return _timeout_ms;
    }

    @Override
    public int getCurrentRetryCount() {
        return _retry_count;
    }

    @Override
    public void retry(VolleyError retry_error) throws VolleyError {
        _retry_count++;
        if (_retry_callback != null)
            _retry_callback.onRetry(retry_error,_retry_count);
    }
}
