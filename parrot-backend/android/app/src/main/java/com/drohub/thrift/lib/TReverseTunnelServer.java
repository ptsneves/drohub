package com.drohub.thrift.lib;
import android.util.Log;

import org.apache.thrift.transport.TServerTransport;
import org.apache.thrift.transport.TTransport;
import org.apache.thrift.transport.TTransportException;

import java.util.concurrent.ArrayBlockingQueue;
import java.nio.ByteBuffer;

public class TReverseTunnelServer extends TServerTransport {
    private static final String TAG = "TReverseTunnel";
    private TTransport _transport;
    private ArrayBlockingQueue<Object> _accepted_workers;
    private boolean _is_stopped;

    public TReverseTunnelServer(TTransport transport, int acceptable_clients) {
        _transport = transport;
        _accepted_workers = new ArrayBlockingQueue<>(2);
        _is_stopped = false;

    }

    @Override
    protected TTransport acceptImpl() throws TTransportException {
        if (!_is_stopped) {
            try {
                _accepted_workers.put(true);
                System.out.println("Accepted connection");
                return _transport;
            }
            catch(InterruptedException e) {
               Log.w(TAG, "Accept was interrupted");
            }
        }
        throw new TTransportException(TTransportException.NOT_OPEN, "Not open");
    }

    @Override
    public void close() {
        Log.i(TAG, "Closed reverse tunnel");
    }

    @Override
    public void interrupt() {
        _is_stopped = true;
        _transport.close();
        super.interrupt();
    }

    @Override
    public void listen() throws TTransportException {
        if (!_is_stopped) {
            _transport.open();
        }
    }
}
