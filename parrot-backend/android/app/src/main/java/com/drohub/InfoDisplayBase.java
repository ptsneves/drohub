package com.drohub;

import java.util.ArrayDeque;
import java.util.Vector;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

public abstract class InfoDisplayBase implements IInfoDisplay {
    final long _refresh_period_ms;
    final private ScheduledExecutorService _executor;
    final private Vector<String> messages;
    final private ArrayDeque<String> _in_view_errors;


    public InfoDisplayBase(long refresh_period_ms) {
        messages = new Vector<>();
        _in_view_errors = new ArrayDeque<>();
        _executor = Executors.newScheduledThreadPool(10);
        _refresh_period_ms = refresh_period_ms;
    }

    public synchronized void add(String msg) {
        if (messages.contains(msg))
            return;
        messages.add(msg);
        if (messages.size() == 1) {
            cycle();
            show();
        }
    }

    public synchronized void remove(String msg) {
        if (!messages.remove(msg))
            return;
        if (messages.size() == 0)
            hide();
    }

    public synchronized void addTemporarily(String msg, long time_available_ms) {
        add(msg);
        _executor.schedule(() -> remove(msg), time_available_ms, TimeUnit.MILLISECONDS);
    }

    private synchronized void cycle() {
        if (_in_view_errors.size() == 0) {
            if (messages.isEmpty())
                return;
            for (String error : messages)
                _in_view_errors.push(error);
        }
        setText(_in_view_errors.pop());
        if (messages.size() != 0)
            _executor.schedule(this::cycle, _refresh_period_ms, TimeUnit.MILLISECONDS);
    }

    abstract protected void setText(String text);
    abstract protected void show();
    abstract protected void hide();
}
