package com.drohub;

import com.drohub.mock.InfoDisplayMock;
import org.junit.Assert;
import org.junit.Test;

public class InfoDisplayBaseTest {

    final String test_string = "Hello";

    @Test
    public void addTemporarily() throws InterruptedException {
        final InfoDisplayMock t = new InfoDisplayMock(100);
        t.addTemporarily(test_string, 10);
        Thread.sleep(150);
        Assert.assertEquals(1, t.hide_set.get());
        Assert.assertEquals(false, t.visible_set.get());
        Assert.assertEquals(test_string, t.string_queue.pop());
        Assert.assertEquals(true, t.string_queue.isEmpty());
    }

    @Test
    public void AddCycleAndRemove() throws InterruptedException {
        final  InfoDisplayMock t = new InfoDisplayMock(90);
        t.add(test_string); //one setText
        t.add(test_string+"1"); //one seText
        Thread.sleep(320); //expect 3 texts
        t.remove(test_string);
        t.remove(test_string+"1");
        Assert.assertEquals(1, t.hide_set.get());
        Assert.assertEquals(false, t.visible_set.get());
        Assert.assertEquals(test_string, t.string_queue.pop());
        Assert.assertEquals(test_string+"1", t.string_queue.pop());
        Assert.assertEquals(test_string, t.string_queue.pop());
        Assert.assertEquals(test_string+"1", t.string_queue.pop());
        Assert.assertEquals(test_string, t.string_queue.pop());
        Assert.assertEquals(0, t.string_queue.size());
    }

    @Test
    public void DoubleAddDoesNotCycle() throws InterruptedException {
        final InfoDisplayMock t = new InfoDisplayMock(500);
        t.add(test_string);
        t.add(test_string); // 1 time for both

        Thread.sleep(1700); //3 times + initial

        t.remove(test_string);
        Assert.assertEquals(1, t.hide_set.get());
        Assert.assertEquals(false, t.visible_set.get());
        Assert.assertEquals(test_string, t.string_queue.pop());
        Assert.assertEquals(3, t.string_queue.size());
    }

    @Test
    public void EmptyRemoveNoError() {
        InfoDisplayBase t = new InfoDisplayMock(9);
        t.remove(test_string);
    }

    @Test
    public void RemoveOnNonExistingNoError() {
        final InfoDisplayBase t = new InfoDisplayMock(9);
        t.add(test_string+"1");
        t.remove(test_string);
    }

    @Test
    public void RemoveBeforeCycle() {
        final InfoDisplayMock t = new InfoDisplayMock(9);
        t.add(test_string);
        t.remove(test_string);
        Assert.assertEquals(1, t.hide_set.get());
        Assert.assertEquals(false, t.visible_set.get());
        Assert.assertEquals(test_string, t.string_queue.pop());
        Assert.assertEquals(0, t.string_queue.size());
    }
}
