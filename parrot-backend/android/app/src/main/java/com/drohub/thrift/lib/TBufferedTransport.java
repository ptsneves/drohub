package com.drohub.thrift.lib;

import java.io.IOException;
import java.io.PipedInputStream;
import java.io.PipedOutputStream;
import java.nio.ByteBuffer;
import org.apache.thrift.transport.TTransport;
import org.apache.thrift.transport.TTransportException;

/**
 * In memory transport with separate buffers for input and output.
 */
public class TBufferedTransport extends TTransport {

    private final int capaticy;
    private final TTransport transport;
    private final PipedInputStream read_source;
    private final PipedOutputStream read_sink;
    private final ByteBuffer outputBuffer;

    public TBufferedTransport(TTransport transport, int buffer_size) throws IOException {
        capaticy = buffer_size;
        read_sink = new PipedOutputStream();
        read_source = new PipedInputStream(capaticy);
        read_source.connect(read_sink);


        outputBuffer = ByteBuffer.allocate(buffer_size);
        this.transport = transport;
    }

    @Override
    public boolean isOpen() {
        return transport.isOpen();
    }

    /**
     * Opening on an in memory transport should have no effect.
     */
    @Override
    public void open() throws TTransportException {
        transport.open();
    }

    @Override
    public void close() {
        transport.close();
        try {
//            System.out.println("Closing but available " + read_source.available());
            read_source.close();
            read_sink.close();
        } catch (IOException e) {
            e.printStackTrace();
        }

    }

    @Override
    public synchronized int read(byte[] buf, int off, int len) throws TTransportException {
        int read_cnt = 0;
        try {
            if (read_source.available() < len) {
                int to_write = capaticy - read_source.available();
                byte tmp[] = new byte[to_write];
                int write_cnt = transport.read(tmp, off, to_write);
                read_sink.write(tmp, off, write_cnt);
                read_sink.flush();
            }

            read_cnt = read_source.read(buf, off, len);
        } catch (IOException e) {
            throw new TTransportException(e);
        }

//        System.out.println("Byte buffer: " + read_cnt + " " + StandardCharsets.UTF_8.decode(ByteBuffer.wrap(buf, off, read_cnt)).toString() );
        return read_cnt;
    }

    @Override
    public void flush() throws TTransportException {
        transport.write(outputBuffer.array());
        outputBuffer.rewind();
        transport.flush();
    }

    @Override
    public void write(byte[] buf, int off, int len) throws TTransportException {
        outputBuffer.put(buf, off, len);
    }
}