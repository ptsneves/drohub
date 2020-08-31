package com.drohub;

import androidx.annotation.NonNull;

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

    private boolean _active;
    private Thread _thread;


    public WatchDog(long watch_duration_ms, @NonNull Listener listener) {
        _watch_duration_ms = watch_duration_ms;
        _listener = listener;
        _time_ms = new AtomicLong(0);
        _active = false;
        _thread = new Thread(this::watch);
    }

    private void watch() {
        _time_ms.set(System.currentTimeMillis());
        while(_active) {
            try {
                Thread.sleep(_watch_duration_ms);
            } catch (InterruptedException e) {
                _active = false;
                _listener.onAlarm(ALARM_TYPE.INTERRUPTED);
            }

            if (getTimeToExpire() > _watch_duration_ms)
                _listener.onAlarm(ALARM_TYPE.EXPIRED);
        }
    }

    public long getTimeToExpire() {
        return System.currentTimeMillis() - _time_ms.get();
    }

    public void keepAlive() {
        _time_ms.set(System.currentTimeMillis());
    }

    public void start() {
        _active = true;
        _thread.start();
    }

    public void stop() {
        _active = false;
        _thread.interrupt();
        do {
            try {
                _thread.join();
            } catch (InterruptedException e) {
                e.printStackTrace();
            }
        } while (_thread.isAlive());
    }
}
