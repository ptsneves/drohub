package com.drohub;

import androidx.annotation.NonNull;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicLong;

public class WatchDog {
    public enum ALARM_TYPE {
        INTERRUPTED,
        EXPIRED,
    }

    public interface Listener {
        void onAlarm(ALARM_TYPE alarm);
    }

    private final Listener _listener;
    private final long _watch_duration_ms;
    final private AtomicLong _time_ms;
    final private ScheduledExecutorService ses = Executors.newSingleThreadScheduledExecutor();

    private ScheduledFuture task;
    private boolean _active;


    public WatchDog(long watch_duration_ms, @NonNull Listener listener) {
        _watch_duration_ms = watch_duration_ms;
        _listener = listener;
        _time_ms = new AtomicLong(0);
        _active = false;
    }

    public long getTimeToExpire() {
        return System.currentTimeMillis() - _time_ms.get();
    }

    public void keepAlive() {
        _time_ms.set(System.currentTimeMillis());
    }

    public void start() {
        if (_active)
            return;
        _active = true;
        keepAlive();
        task = ses.scheduleAtFixedRate(() -> {
            if (getTimeToExpire() > _watch_duration_ms)
                _listener.onAlarm(ALARM_TYPE.EXPIRED);
        }, 0, _watch_duration_ms, TimeUnit.MILLISECONDS);
    }

    public void stop() {
        if (!_active)
            return;
        _active = false;
        task.cancel(true);
        _listener.onAlarm(ALARM_TYPE.INTERRUPTED);
    }
}
