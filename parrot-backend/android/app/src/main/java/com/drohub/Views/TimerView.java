package com.drohub.Views;

import android.content.Context;
import android.util.AttributeSet;
import androidx.annotation.Nullable;

public class TimerView extends androidx.appcompat.widget.AppCompatTextView {

    final private int _update_period_in_ms = 1000;
    private long _timer_start_time;
    private Boolean _is_started;

    private Runnable _updateTimeText = new Runnable() {
        @Override
        public void run() {
            if (!_is_started)
                return;

            final long total_time_passed_s = (System.currentTimeMillis() - _timer_start_time) /1000;
            final long time_passed_minute = total_time_passed_s / 60;
            final long time_passed_s = total_time_passed_s % 60;
            String time_text = String.format("%02d:%02d", time_passed_minute, time_passed_s);

            setText(time_text);
            postDelayed(_updateTimeText, _update_period_in_ms);
        }
    };

    public TimerView(Context context) {
        this(context, null);
    }

    public TimerView(Context context, @Nullable AttributeSet attrs) {
        super(context, attrs);
    }

    public void startTimer() {
        _timer_start_time = System.currentTimeMillis();
        _is_started = true;
        postDelayed(_updateTimeText, _update_period_in_ms);
    }

    public void stopTimer() {
        _is_started = false;
    }

}
