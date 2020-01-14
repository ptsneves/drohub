package com.drohub.thrift.lib;

import java.util.concurrent.LinkedBlockingDeque;
import org.apache.thrift.transport.TTransport;
import org.apache.thrift.transport.TTransportException;

import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.net.URI;
import java.net.URISyntaxException;
import org.java_websocket.client.WebSocketClient;
import java.util.Map;

import org.java_websocket.handshake.ServerHandshake;


public class TWebSocketClient extends TTransport {

    private class ExampleClient extends WebSocketClient {
        LinkedBlockingDeque<Byte> input;

        ExampleClient( URI serverUri, Map<String, String> httpHeaders ) {
            super(serverUri, httpHeaders);
            input = new LinkedBlockingDeque<>(1024);
        }

        @Override
        public void onOpen( ServerHandshake handshakedata ) {
            System.out.println( "opened connection" );
            // if you plan to refuse connection based on ip or httpfields overload: onWebsocketHandshakeReceivedAsClient
        }

        @Override
        public void onMessage( String message ) {
            try {
                byte a[] = message.getBytes(StandardCharsets.UTF_8 );
                for(Byte e : a) {
                    System.out.print(e);
                    input.put(e);
                }
            }
            catch (InterruptedException e){
                ;
            }

            System.out.println( "received1: " + message);
        }

        @Override
        public void onMessage( ByteBuffer message ) {
            try {
                byte a[] = message.array();
                for (Byte e: a) {
                    input.put(e);
                }
            }
            catch (InterruptedException e){
                ;
            }

            System.out.println( "received2: " + StandardCharsets.UTF_8.decode(message).toString() );
        }

        @Override
        public void onClose( int code, String reason, boolean remote ) {
            // The codecodes are documented in class org.java_websocket.framing.CloseFrame
            System.out.println( "Connection closed by " + ( remote ? "remote peer" : "us" ) + " Code: " + code + " Reason: " + reason );
        }

        @Override
        public void onError( Exception ex ) {
            ex.printStackTrace();
            // if the error is fatal then onClose will be called additionally
        }

    }
    private ExampleClient _client;
    private String host_uri;
    private  Map<String, String> http_headers;

    public TWebSocketClient(String host,  Map<String, String> httpHeaders) {
        super();
        host_uri = host;
        http_headers = httpHeaders;
    }
    @Override
    public boolean isOpen() {
        return _client.isOpen();
    }

    @Override
    public void open() throws TTransportException {
        try {
            _client = new ExampleClient( new URI( host_uri), http_headers);
            _client.connectBlocking();
        }catch (InterruptedException e) {
            throw new TTransportException(TTransportException.UNKNOWN, e.getMessage());
        }
        catch (URISyntaxException e) {
            throw new TTransportException(TTransportException.UNKNOWN, e.getMessage());
        }
    }

    @Override
    public void close() {
        try {
            _client.closeBlocking();
        }catch (InterruptedException e) {
            //Cannot throw on close
        }
    }

    @Override
    public int read(byte[] buf, int off, int len) throws TTransportException {
        try {
            for (int i = 0; i < len;i++)
                buf[off+i] = _client.input.take();

            return len;
        }catch (InterruptedException e) {
            System.out.println("Failed reading");
            throw new TTransportException(TTransportException.UNKNOWN, e.getMessage());
        }
    }

    @Override
    public void write(byte[] buf, int off, int len) throws TTransportException {
        _client.send(ByteBuffer.wrap(buf, off, len));
    }
}
