package com.drohub.Views;

import android.content.Context;
import android.util.AttributeSet;

public class ToggleButtonExView extends androidx.appcompat.widget.AppCompatButton {
    public interface OnActiveListener {
        Boolean onActive(ToggleButtonExView v);
    }

    public interface OnInactiveListener {
        Boolean onInactive(ToggleButtonExView v);
    }

    private Boolean _is_active;
    private OnActiveListener _on_active_listener;
    private OnInactiveListener _on_inactive_listener;

    public ToggleButtonExView(Context context) {
        this(context, null);
    }

    public ToggleButtonExView(Context context, AttributeSet attrs) {
        super(context, attrs);
        _is_active = false;
        super.setOnClickListener(v -> {
            _is_active = !_is_active;
            if (_is_active && _on_active_listener != null)
                _is_active = _on_active_listener.onActive(this);
            else if (!_is_active && _on_inactive_listener != null)
                _is_active = !_on_inactive_listener.onInactive(this);
        });
    }

    public void setActiveListener(OnActiveListener l) {
        _on_active_listener = l;
    }

    public void setInactiveListener(OnInactiveListener l) {
        _on_inactive_listener = l;
    }
}
