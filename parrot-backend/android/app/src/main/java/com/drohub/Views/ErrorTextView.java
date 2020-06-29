package com.drohub.Views;

import android.content.Context;
import android.util.AttributeSet;
import android.widget.TextView;
import androidx.annotation.Nullable;
import com.drohub.R;

import java.util.ArrayDeque;
import java.util.Vector;

public class ErrorTextView extends androidx.appcompat.widget.AppCompatTextView {
    final long _refresh_period_ms = 2000;

    private Vector<String> _errors;
    private ArrayDeque<String> _in_view_errors;

    public ErrorTextView(Context context) {
        this(context, null);
    }

    public ErrorTextView(Context context, @Nullable AttributeSet attrs) {
        super(context, attrs);
        _errors = new Vector<>();
        _in_view_errors = new ArrayDeque<>();
    }

    public synchronized void addError(String msg) {
        _errors.add(msg);
        if (_errors.size() == 1) {
            cycleText();
            show();
        }
    }

    public synchronized void removeError(String msg) {
        _errors.remove(msg);
        if (_errors.size() == 0)
            hide();
    }

    public void addErrorTemporarily(String msg, long time_available) {
        addError(msg);
        postDelayed(() -> removeError(msg), time_available);
    }

    private void cycleText() {
        if (_in_view_errors.size() == 0) {
            if (_errors.isEmpty())
                return;
            for (String error : _errors)
                _in_view_errors.push(error);
        }
        setText(_in_view_errors.pop());
        if (_errors.size() != 0)
            postDelayed(() -> cycleText(), _refresh_period_ms);
    }


    private void show() {
        TextView info_warning_errors = findViewById(R.id.info_warnings_errors);
        info_warning_errors.setVisibility(VISIBLE);
    }

    private void hide() {
        TextView info_warning_errors = findViewById(R.id.info_warnings_errors);
        info_warning_errors.setVisibility(INVISIBLE);
    }
}
