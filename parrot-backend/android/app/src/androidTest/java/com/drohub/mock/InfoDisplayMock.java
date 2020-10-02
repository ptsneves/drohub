package com.drohub.mock;

import com.drohub.InfoDisplayBase;

import java.util.ArrayDeque;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;

public class InfoDisplayMock extends InfoDisplayBase {
    public final AtomicInteger hide_set = new AtomicInteger(0);
    public final ArrayDeque<String> string_queue = new ArrayDeque<>();
    public final AtomicBoolean visible_set = new AtomicBoolean(false);

    public InfoDisplayMock(long refresh_period) {
        super(refresh_period);
    }

    @Override
    protected void setText(String text) {
        string_queue.add(text);
    }

    @Override
    protected void hide() {
        hide_set.getAndIncrement();
        visible_set.set(false);
    }
}
