package com.drohub;

import org.junit.Assert;
import org.junit.Before;
import org.junit.Test;

import java.util.ArrayDeque;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;

public class InfoDisplayBaseTest {
    final AtomicInteger hide_set = new AtomicInteger(0);
    final ArrayDeque<String> string_queue = new ArrayDeque<>();
    final AtomicBoolean visible_set = new AtomicBoolean(false);
    final String test_string = "Hello";

    public class TestSubclass extends InfoDisplayBase {
        public TestSubclass(long refresh_period) {
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

    @Before
    public void init() {
        hide_set.set(0);
        string_queue.clear();
    }

    @Test
    public void addTemporarily() throws InterruptedException {
        final InfoDisplayBase t = new TestSubclass(20);
        t.addTemporarily(test_string, 10);
        Thread.sleep(30);
        Assert.assertEquals(1, hide_set.get());
        Assert.assertEquals(false, visible_set.get());
        Assert.assertEquals(test_string, string_queue.pop());
        Assert.assertEquals(true, string_queue.isEmpty());
    }

    @Test
    public void AddCycleAndRemove() throws InterruptedException {
        final  InfoDisplayBase t = new TestSubclass(9);
        t.add(test_string); //one setText
        t.add(test_string+"1"); //and seText
        Thread.sleep(30); //expect 3 texts
        t.remove(test_string);
        t.remove(test_string+"1");
        Assert.assertEquals(1, hide_set.get());
        Assert.assertEquals(false, visible_set.get());
        Assert.assertEquals(test_string, string_queue.pop());
        Assert.assertEquals(test_string+"1", string_queue.pop());
        Assert.assertEquals(test_string, string_queue.pop());
        Assert.assertEquals(test_string+"1", string_queue.pop());
        Assert.assertEquals(test_string, string_queue.pop());
        Assert.assertEquals(0, string_queue.size());
    }

    @Test
    public void DoubleAddDoesNotCycle() throws InterruptedException {
        final InfoDisplayBase t = new TestSubclass(9);
        t.add(test_string);
        t.add(test_string); // 1 time for both
        Thread.sleep(30); //3 times
        t.remove(test_string);
        Assert.assertEquals(1, hide_set.get());
        Assert.assertEquals(false, visible_set.get());
        Assert.assertEquals(test_string, string_queue.pop());
        Assert.assertEquals(2, string_queue.size());
    }

    @Test
    public void EmptyRemoveNoError() {
        InfoDisplayBase t = new TestSubclass(9);
        t.remove(test_string);
    }

    @Test
    public void RemoveOnNonExistingNoError() {
        final InfoDisplayBase t = new TestSubclass(9);
        t.add(test_string+"1");
        t.remove(test_string);
    }

    @Test
    public void RemoveBeforeCycle() {
        final InfoDisplayBase t = new TestSubclass(9);
        t.add(test_string);
        t.remove(test_string);
        Assert.assertEquals(1, hide_set.get());
        Assert.assertEquals(false, visible_set.get());
        Assert.assertEquals(test_string, string_queue.pop());
        Assert.assertEquals(0, string_queue.size());
    }
}
