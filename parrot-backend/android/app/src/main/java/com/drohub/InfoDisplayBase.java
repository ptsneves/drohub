package com.drohub;

import java.util.ArrayDeque;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;

public abstract class InfoDisplayBase implements IInfoDisplay {
    final long _refresh_period_ms;
    final private ScheduledExecutorService _executor;
    final private ArrayDeque<String> messages;

    private ScheduledFuture _cycle;
    private int _cycle_index;


    public InfoDisplayBase(long refresh_period_ms) {
        messages = new ArrayDeque<>();
        _executor = Executors.newScheduledThreadPool(10);
        _refresh_period_ms = refresh_period_ms;
    }

    public synchronized void add(String msg) {
        if (messages.contains(msg))
            return;
        messages.push(msg);
        _cycle_index = 0;
        cycle();
    }

    public synchronized void remove(String msg) {
        if (!messages.remove(msg))
            return;
        if (messages.isEmpty()) {
            hide();
            _cycle.cancel(true);
        }
    }

    public synchronized void addTemporarily(String msg, long time_available_ms) {
        add(msg);
        _executor.schedule(() -> remove(msg), time_available_ms, TimeUnit.MILLISECONDS);
    }

    private synchronized void cycle() {
        if (messages.isEmpty())
            return;

        if (_cycle != null)
            _cycle.cancel(true);
        setText((String)messages.toArray()[_cycle_index % messages.size()]);
        _cycle_index++;
        _cycle = _executor.schedule(this::cycle, _refresh_period_ms, TimeUnit.MILLISECONDS);
    }

    abstract protected void setText(String text);
    abstract protected void hide();
}
