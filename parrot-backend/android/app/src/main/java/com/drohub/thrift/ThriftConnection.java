package com.drohub.thrift;

import android.content.Intent;
import android.util.Log;

import com.drohub.GroundSdkActivityBase;
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
    private DroHubHandler drohub_handler;
    private Thread _server_thread;

    public void onStart(String drone_serial, String thrift_ws_url, String janus_websocket_uri,
                        GroundSdkActivityBase activity) {

        HashMap<String, String> http_headers = new HashMap<>();
        http_headers.put("User-Agent", "AirborneProjects");
        http_headers.put("Content-Type", "application/x-thrift");
        http_headers.put("x-device-expected-serial", drone_serial);
        TWebSocketClient tws = new TWebSocketClient(thrift_ws_url, http_headers);

        TReverseTunnelServer trts = new TReverseTunnelServer(tws);
        TProtocolFactory message_validator_factory = new TMessageValidatorProtocol.Factory(
                new TJSONProtocol.Factory(),
                TMessageValidatorProtocol.ValidationModeEnum.KEEP_READING,
                TMessageValidatorProtocol.OperationModeEnum.SEQID_SLAVE);

        drohub_handler = new DroHubHandler(drone_serial, janus_websocket_uri, activity);
        TThreadPoolServer.Args args = new TThreadPoolServer.Args(trts);
        args.minWorkerThreads(6);
        args.maxWorkerThreads(6);

        args.processor(new Drone.Processor<>(drohub_handler));
        args.inputProtocolFactory(message_validator_factory);
        args.outputProtocolFactory(message_validator_factory);
        _server_engine = new TThreadPoolServer(args);
        _server_thread = new Thread(() -> _server_engine.serve());
        _server_thread.start();
    }

    public void handleActivityResult(int requestCode, int resultCode, Intent data) {
        if (drohub_handler != null)
            drohub_handler.handleCapturePermissionCallback(requestCode, resultCode, data);
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
