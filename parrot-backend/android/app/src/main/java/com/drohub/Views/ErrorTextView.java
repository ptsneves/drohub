package com.drohub.Views;

import android.app.Activity;
import android.content.Context;
import android.util.AttributeSet;
import androidx.annotation.Nullable;
import com.drohub.InfoDisplayBase;

import java.util.ArrayDeque;
import java.util.Vector;

public class ErrorTextView extends androidx.appcompat.widget.AppCompatTextView {
    final long _refresh_period_ms = 2000;

    private Vector<String> _errors;
    private ArrayDeque<String> _in_view_errors;
    final private TextViewInfoDisplay _display;

    public ErrorTextView(Context context) {
        this(context, null);
    }

    public ErrorTextView(Context context, @Nullable AttributeSet attrs) {
        super(context, attrs);
        _errors = new Vector<>();
        _in_view_errors = new ArrayDeque<>();
        _display = new TextViewInfoDisplay(this);
    }

    public TextViewInfoDisplay getInfoDisplay() {
        return _display;
    }

    public class TextViewInfoDisplay extends InfoDisplayBase {
        final ErrorTextView _view;
        public TextViewInfoDisplay(ErrorTextView view) {
            super(_refresh_period_ms);
            _view = view;
        }

        @Override
        protected void setText(String text) {
            _view.post(() ->  {
                _view.setText(text);
                _view.setVisibility(VISIBLE);
            });
        }

        @Override
        protected void hide() {
            _view.post(() -> _view.setVisibility(INVISIBLE));
        }
    }
}
