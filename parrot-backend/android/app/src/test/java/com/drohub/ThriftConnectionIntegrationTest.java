package com.drohub;


import com.drohub.thrift.ThriftConnection;

import org.junit.Test;

import static org.junit.Assert.*;

/**
 * Example local unit test, which will execute on the development machine (host).
 *
 * @see <a href="http://d.android.com/tools/testing">Testing documentation</a>
 */
public class ThriftConnectionIntegrationTest {
    @Test
    public void SimpleTest() throws InterruptedException {
        ThriftConnection c = new ThriftConnection();
        c.onStart("PI040416BA8H083705", "ws://localhost:5000/ws");
        Thread.sleep(5000);
        c.onStop();
    }
}