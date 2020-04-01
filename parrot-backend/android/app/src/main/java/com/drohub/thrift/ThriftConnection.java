package com.drohub.thrift;

import android.content.Intent;
import android.util.Log;

import com.drohub.thift.gen.Drone;
import com.drohub.thrift.lib.TMessageValidatorProtocol;
import com.drohub.thrift.lib.TReverseTunnelServer;
import com.drohub.thrift.lib.TWebSocketClient;

import org.apache.thrift.protocol.TJSONProtocol;
import org.apache.thrift.protocol.TProtocolFactory;
import org.apache.thrift.server.TThreadPoolServer;
import java.util.HashMap;

public class ThriftConnection {
    private TThreadPoolServer _server_engine;
    private Thread _server_thread;
    private DroHubHandler _drohub_handler;

    public void onStart(String drone_serial, String thrift_ws_url,
                        DroHubHandler drohub_handler, String user, String password) {
        Log.w("COPTER", "Started thrift connection to " + thrift_ws_url );
        HashMap<String, String> http_headers = new HashMap<>();
        http_headers.put("User-Agent", "AirborneProjects");
        http_headers.put("Content-Type", "application/x-thrift");
        http_headers.put("x-drohub-user", user);
        http_headers.put("x-drohub-token", password);
        http_headers.put("x-device-expected-serial", drone_serial);
        TWebSocketClient tws = new TWebSocketClient(thrift_ws_url, http_headers);

        TReverseTunnelServer trts = new TReverseTunnelServer(tws);
        TProtocolFactory message_validator_factory = new TMessageValidatorProtocol.Factory(
                new TJSONProtocol.Factory(),
                TMessageValidatorProtocol.ValidationModeEnum.KEEP_READING,
                TMessageValidatorProtocol.OperationModeEnum.SEQID_SLAVE);


        TThreadPoolServer.Args args = new TThreadPoolServer.Args(trts);
        args.minWorkerThreads(8);
        args.maxWorkerThreads(8);
        _drohub_handler = drohub_handler;
        args.processor(new Drone.Processor<>(_drohub_handler));
        args.inputProtocolFactory(message_validator_factory);
        args.outputProtocolFactory(message_validator_factory);

        _server_engine = new TThreadPoolServer(args);
        _server_thread = new Thread(() -> _server_engine.serve());
        _server_thread.start();
    }

    public void handleActivityResult(int requestCode, int resultCode, Intent data) {
        if (_drohub_handler != null)
            _drohub_handler.handleCapturePermissionCallback(requestCode, resultCode, data);
    }

    public void onStop() {
        Log.w("ThriftConnection", "Stoping thrift connection");
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
