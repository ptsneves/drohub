package com.drohub;

import org.junit.Assert;
import org.junit.Test;

import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;

public class WatchDogTest {
    @Test
    public void AlarmCalled() throws InterruptedException {
        AtomicBoolean alarm_set = new AtomicBoolean(false);
        WatchDog w = new WatchDog(100, alarm -> {
            if (!alarm_set.get())
                Assert.assertEquals(WatchDog.ALARM_TYPE.EXPIRED, alarm);
            alarm_set.set(true);
        });
        w.start();
        Thread.sleep(250);
        Assert.assertEquals(true, alarm_set.get());
        w.stop();
    }

    @Test
    public void AlarmNotCalled() throws InterruptedException {
        AtomicBoolean alarm_set = new AtomicBoolean(false);
        WatchDog w = new WatchDog(500, alarm -> {
            alarm_set.set(true);
        });

        w.start();
        Thread.sleep(300);
        w.keepAlive();
        Thread.sleep(400);
        Assert.assertEquals(false, alarm_set.get());
        Thread.sleep(400);
        Assert.assertEquals(true, alarm_set.get());
        w.stop();
    }

    @Test
    public void AlarmCalledUntilKeepAlive() throws InterruptedException {
        AtomicBoolean alarm_set = new AtomicBoolean(false);
        WatchDog w = new WatchDog(100, alarm -> {
            alarm_set.set(true);
        });
        w.start();
        Thread.sleep(250);
        Assert.assertEquals(true, alarm_set.get());
        alarm_set.set(false);
        Thread.sleep(250);
        Assert.assertEquals(true, alarm_set.get());
        w.stop();
    }

    @Test
    public void AlarmStopInterrupts() throws InterruptedException {
        AtomicBoolean alarm_set = new AtomicBoolean(false);
        WatchDog w = new WatchDog(150, alarm -> {
            Assert.assertEquals(WatchDog.ALARM_TYPE.INTERRUPTED, alarm);
            alarm_set.set(true);
        });
        w.start();
        Thread.sleep(100);
        w.stop();
        Assert.assertEquals(true, alarm_set.get());
    }

    @Test
    public void AlarmDoubleStop1Interrupts() {
        AtomicInteger counter = new AtomicInteger(0);
        WatchDog w = new WatchDog(1000, alarm -> {
            Assert.assertEquals(WatchDog.ALARM_TYPE.INTERRUPTED, alarm);
            counter.getAndIncrement();
        });
        w.start();
        w.stop();
        Assert.assertEquals(1, counter.get());
    }

    @Test
    public void StartTwiceOnlyOneTrigger() throws InterruptedException {
        AtomicInteger counter = new AtomicInteger(0);
        WatchDog w = new WatchDog(1000, alarm -> counter.getAndIncrement());
        w.start();
        w.start();
        Thread.sleep(1500);
        Assert.assertEquals(1, counter.get());
        w.stop();
    }
}
