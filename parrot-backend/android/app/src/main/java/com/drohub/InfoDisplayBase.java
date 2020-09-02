package com.drohub;

import java.util.ArrayDeque;
import java.util.Vector;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

public abstract class InfoDisplayBase implements IInfoDisplay {
    final long _refresh_period_ms;
    final private ScheduledExecutorService _executor;

    private Vector<String> _errors;
    private ArrayDeque<String> _in_view_errors;


    public InfoDisplayBase(long refresh_period_ms) {
        _errors = new Vector<>();
        _in_view_errors = new ArrayDeque<>();
        _executor = Executors.newScheduledThreadPool(10);
        _refresh_period_ms = refresh_period_ms;
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

    public void addErrorTemporarily(String msg, long time_available_ms) {
        addError(msg);
        _executor.schedule(() -> removeError(msg), time_available_ms, TimeUnit.MILLISECONDS);
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
            _executor.schedule(this::cycleText, _refresh_period_ms, TimeUnit.MILLISECONDS);
    }

    abstract protected void setText(String text);
    abstract protected void show();
    abstract protected void hide();
}
