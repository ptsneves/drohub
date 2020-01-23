package com.drohub.thrift;

import android.util.Log;

import com.drohub.thift.gen.Drone;
import com.drohub.thrift.lib.TMessageValidatorProtocol;
import com.drohub.thrift.lib.TReverseTunnelServer;
import com.drohub.thrift.lib.TWebSocketClient;

import org.apache.thrift.protocol.TJSONProtocol;
import org.apache.thrift.protocol.TProtocolFactory;
import org.apache.thrift.server.TServer;
import org.apache.thrift.server.TSimpleServer;
import org.apache.thrift.transport.TFramedTransport;

import java.util.HashMap;

public class ThriftConnection {
    private TWebSocketClient tws;
    private TFramedTransport ft;
    private TReverseTunnelServer trts;
    private TSimpleServer _server_engine;
    private Thread _server_thread;

    public void onStart(String drone_serial, String ws_url) {
        HashMap<String, String> http_headers = new HashMap<>();
        http_headers.put("User-Agent", "AirborneProjects");
        http_headers.put("Content-Type", "application/x-thrift");
        http_headers.put("x-device-expected-serial", drone_serial);
        tws = new TWebSocketClient(ws_url, http_headers);
        ft = new TFramedTransport(tws);
        trts = new TReverseTunnelServer(ft, 1);
        TProtocolFactory message_validator_factory = new TMessageValidatorProtocol.Factory(new TJSONProtocol.Factory(),
                TMessageValidatorProtocol.ValidationModeEnum.KEEP_READING,
                TMessageValidatorProtocol.OperationModeEnum.SEQID_SLAVE);

        DroHubHandler handler = new DroHubHandler(drone_serial);
        TServer.AbstractServerArgs args = new TServer.Args(trts);

        args.processor(new Drone.Processor(handler));
        args.inputProtocolFactory(message_validator_factory);
        args.outputProtocolFactory(message_validator_factory);
        _server_engine = new TSimpleServer(args);
        _server_thread = new Thread(() -> {
            _server_engine.serve();
        });
        _server_thread.start();
    }

    public void onStop() {
        if (_server_engine != null) {
            _server_engine.stop();
        }
        if (_server_thread != null) {
            try {
                _server_thread.join();
            } catch (InterruptedException e) {
                e.printStackTrace();
            }
        }
        Log.w("ThriftConnection", "Stopped thrift connection");
    }
}
