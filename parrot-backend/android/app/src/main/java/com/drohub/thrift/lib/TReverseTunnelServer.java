package com.drohub.thrift.lib;
import android.util.Log;

import org.apache.thrift.transport.TMemoryBuffer;
import org.apache.thrift.transport.TServerTransport;
import org.apache.thrift.transport.TTransport;
import org.apache.thrift.transport.TTransportException;

import java.io.IOException;
import java.util.concurrent.ArrayBlockingQueue;

public class TReverseTunnelServer extends TServerTransport {
    class WrappedTransportForTunnel extends TTransport {

        WrappedTransportForTunnel() throws InterruptedException {
            _accepted_workers.put(true);
        }

        @Override
        public boolean isOpen() {
            return true;
        }

        @Override
        public void open() throws TTransportException {
        }

        @Override
        public void close() {

        }

        @Override
        public synchronized int read(byte[] buf, int off, int len) throws TTransportException {
            return _transport.read(buf, off, len);

        }

        @Override
        public synchronized void write(byte[] buf, int off, int len) throws TTransportException {
            _transport.write(buf, off, len);
        }

        @Override
        public void flush() throws TTransportException {
            _transport.flush();
            try {
                _accepted_workers.take();
                //for the exceptions
            } catch (InterruptedException e) {
                System.out.println("Close was interrupted");
            }
            System.out.println(_accepted_workers.remainingCapacity());
        }
    }
    private static final String TAG = "TReverseTunnel";
    private TTransport _transport;
    private ArrayBlockingQueue<Object> _accepted_workers;
    private boolean _is_accepting;

    public TReverseTunnelServer(TTransport transport, int acceptable_clients) {
        _transport = transport;
        _accepted_workers = new ArrayBlockingQueue<>(acceptable_clients);
        _is_accepting = false;
    }

    @Override
    protected TTransport acceptImpl() throws TTransportException {
        if (_is_accepting) {
            try {
                System.out.println("Accepted connection" + _accepted_workers.remainingCapacity());
                return new TBufferedTransport(new WrappedTransportForTunnel(), 1024);
            }
            catch(InterruptedException | IOException e) {
               System.out.println("Accept was interrupted");
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
        _is_accepting = false;
        _transport.close();
        super.interrupt();
    }

    @Override
    public void listen() throws TTransportException {
        _transport.open();
        _is_accepting = true;
    }
}
