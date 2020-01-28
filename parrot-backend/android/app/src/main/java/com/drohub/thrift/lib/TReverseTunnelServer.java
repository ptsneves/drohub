package com.drohub.thrift.lib;
import android.util.Log;

import org.apache.thrift.transport.TMemoryInputTransport;
import org.apache.thrift.transport.TServerTransport;
import org.apache.thrift.transport.TTransport;
import org.apache.thrift.transport.TTransportException;

import java.io.ByteArrayOutputStream;
import java.util.Arrays;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.LinkedBlockingDeque;

public class TReverseTunnelServer extends TServerTransport {

    class WrappedTransportForTunnel extends TTransport {
        private TMemoryInputTransport _read_buffer;
        private ByteArrayOutputStream _write_buffer;
        private boolean _is_finished;
        WrappedTransportForTunnel(byte[] read_buffer) {
            _read_buffer = new TMemoryInputTransport(read_buffer);
            _write_buffer = new ByteArrayOutputStream(70);
            _is_finished = false;
        }

        @Override
        public boolean isOpen() {
            return !_is_closed && !_is_finished;
        }

        @Override
        public void open() {
        }

        @Override
        public void close() {
        }

        @Override
        public int read(byte[] buf, int off, int len) throws TTransportException {
            return _read_buffer.read(buf, off, len);

        }

        @Override
        public void write(byte[] buf, int off, int len) throws TTransportException {
            _write_buffer.write(buf, off, len);
        }

        @Override
        public synchronized void flush() throws TTransportException {
            try {
                _write_queue.put(_write_buffer.toByteArray());
                _write_buffer.reset();
                _is_finished = true;
            } catch (InterruptedException e) {
                throw new TTransportException(e);
            }
        }
    }
    private static final String TAG = "TReverseTunnel";
    private TTransport _transport;
    private boolean _is_closed;
    private Future<Boolean> _master_connection_read;
    private Future<Boolean> _master_connection_write;
    private ExecutorService _executor_service = Executors.newFixedThreadPool(2);

    private LinkedBlockingDeque<byte[]> _read_queue;
    private LinkedBlockingDeque<byte[]> _write_queue;

    public TReverseTunnelServer(TTransport transport) {
        _transport = transport;
        _read_queue = new LinkedBlockingDeque<>(1024);
        _write_queue = new LinkedBlockingDeque<>(1024);
        _is_closed = true;
    }

    @Override
    protected TTransport acceptImpl() throws TTransportException {
        if (_is_closed)
            throw new TTransportException(TTransportException.NOT_OPEN, "Not open");

        try {
            return new WrappedTransportForTunnel(_read_queue.take());
        }
        catch(InterruptedException e) {
            throw new TTransportException(e);
        }
    }

    @Override
    public void close() {
        Log.i(TAG, "Closed reverse tunnel");
        _is_closed = true;
        try {
            _master_connection_write.get();
            _master_connection_read.get();
        } catch (Exception e) {
            e.printStackTrace();
        }

    }

    @Override
    public void interrupt() {
        _transport.close();
        super.interrupt();
    }

    @Override
    public void listen() throws TTransportException{
        _transport.open();
        _is_closed = false;

        _master_connection_read = _executor_service.submit(() -> {
            byte[] buf = new byte[1024];
            while (!_is_closed) {
                try {
                    int recvd = _transport.read(buf, 0, 1024);
                    _read_queue.put(Arrays.copyOf(buf, recvd));
                } catch (InterruptedException | TTransportException e) {
                    _is_closed = true;
                    throw new TTransportException(e);
                }
            }
            return true;
        });

        _master_connection_write = _executor_service.submit(() -> {
            while (!_is_closed) {
                try {
                    byte[] buf = _write_queue.take();
                    _transport.write(buf, 0, buf.length);
                } catch (InterruptedException | TTransportException e) {
                    _is_closed = true;
                    throw new TTransportException(e);
                }
            }
            return true;
        });

    }
}
