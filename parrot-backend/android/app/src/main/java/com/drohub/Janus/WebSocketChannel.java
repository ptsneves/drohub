package com.drohub.Janus;

import android.app.Activity;
import android.os.Handler;
import android.os.HandlerThread;
import android.text.TextUtils;
import android.util.Log;
import com.drohub.IInfoDisplay;
import com.drohub.Janus.PeerConnectionParameters.PeerConnectionParameters;
import com.drohub.WatchDog;
import org.java_websocket.client.WebSocketClient;
import org.java_websocket.drafts.Draft_6455;
import org.java_websocket.handshake.ServerHandshake;
import org.java_websocket.protocols.Protocol;
import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;
import org.webrtc.IceCandidate;
import org.webrtc.SessionDescription;

import java.io.InvalidObjectException;
import java.math.BigInteger;
import java.net.URI;
import java.net.URISyntaxException;
import java.util.Collections;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.TimeUnit;

public class WebSocketChannel extends WebSocketClient {
    private static final String TAG = "WebSocketChannel";

    final private IInfoDisplay _display;

    private String displayName;
    private JanusTransactions janusTransactions = new JanusTransactions();
    private ConcurrentHashMap<BigInteger, JanusHandle> handles = new ConcurrentHashMap<>();
    private ConcurrentHashMap<BigInteger, JanusHandle> feeds = new ConcurrentHashMap<>();
    HandlerThread keepaliveThread;
    private Handler keepaliveHandler;
    private BigInteger mSessionId;
    private JanusRTCInterface delegate;
    private Activity _activity;
    private long _room_id;
    private PeerConnectionParameters _peerConnectionParameters;

    public static WebSocketChannel createWebSockeChannel(long room_id,
                                                         String displayName,
                                                         Activity activity,
                                                         JanusRTCInterface delegate,
                                                         String url,
                                                         PeerConnectionParameters connection_parameters,
                                                         IInfoDisplay display
                                                         ) throws URISyntaxException, InterruptedException, InvalidObjectException {
        Draft_6455 janus_draft = new Draft_6455(Collections.emptyList(),
                Collections.singletonList(new Protocol("janus-protocol")));
        return new WebSocketChannel(room_id, displayName, activity, delegate, url, janus_draft,
                connection_parameters, display);
    }

    private WebSocketChannel(long room_id,
                             String displayName,
                             Activity activity,
                             JanusRTCInterface delegate,
                             String url,
                             Draft_6455 janus_draft,
                             PeerConnectionParameters peerConnectionParameters,
                             IInfoDisplay display) throws URISyntaxException, InterruptedException, InvalidObjectException {

        super(new URI(url), janus_draft);
        this.displayName = "drone-" + displayName + "-" + System.currentTimeMillis();
        this.delegate = delegate;
        _activity = activity;
        _room_id = room_id;
        _peerConnectionParameters = peerConnectionParameters;
        _display = display;
        if (!connectBlocking(10, TimeUnit.SECONDS))
            throw new InvalidObjectException("Could not connect to janus");
    }

    @Override
    public void onOpen(ServerHandshake handshakedata) {
        keepaliveThread = new HandlerThread("KeepaliveThread");
        keepaliveThread.start();
        keepaliveHandler = new Handler(keepaliveThread.getLooper());

        JSONObject transaction = janusTransactions.addTransaction("create", (type, jo) -> {
            if (type != JanusTransactions.Listener.CallBackType.SUCCESS)
                return;

            mSessionId = new BigInteger(jo.optJSONObject("data").optString("id"));
            keepaliveHandler.post(fireKeepAlive);
            publisherCreateHandle();
        });

        send(transaction.toString());
    }

    @Override
    public void onMessage(String message) {
        _activity.runOnUiThread(() -> {
            Log.e(TAG, "onMessage" + message);
            JSONObject jo;
            try {
                jo = new JSONObject(message);
            } catch (JSONException e) {
                e.printStackTrace();
                return;
            }

            String janus = jo.optString("janus");
            if (JanusTransactions.isTransaction(jo)) {
                janusTransactions.processTransaction(jo);
                return;
            }

            JanusHandle handle = handles.get(new BigInteger(jo.optString("sender")));
            if (handle == null) {
                Log.e(TAG, "missing handle");
                return;
            }

            switch (janus) {
                case "event":
                    processEvent(handle, jo);
                    break;
                case "detached":
                    handle.onLeaving.onJoined(handle);
                    break;
            }
        })  ;
    }

    private void processEvent(JanusHandle handle, JSONObject jo) {
        JSONObject plugin_data = jo.optJSONObject("plugindata");
        JSONObject plugin;
        if (plugin_data != null && (plugin = plugin_data.optJSONObject("data")) != null){
            if (plugin.optString("videoroom").equals("joined"))
                handle.onJoined.onJoined(handle);

            JSONArray publishers = plugin.optJSONArray("publishers");
            if (publishers != null && publishers.length() > 0) {
                for (int i = 0, size = publishers.length(); i <= size - 1; i++) {
                    JSONObject publisher = publishers.optJSONObject(i);
                    BigInteger feed = new BigInteger(publisher.optString("id"));
                    String display = publisher.optString("display");
                    subscriberCreateHandle(feed, display);
                }
            }

            String leaving = plugin.optString("leaving");
            if (!TextUtils.isEmpty(leaving)) {
                JanusHandle jhandle = feeds.get(new BigInteger(leaving));
                if (jhandle != null)
                    jhandle.onLeaving.onJoined(jhandle);
            }
        }

        JSONObject jsep = jo.optJSONObject("jsep");
        if (jsep != null) {
            handle.onRemoteJsep.onRemoteJsep(handle, jsep);
        }
    }

    @Override
    public void send(String message) {
        Log.e(TAG, "send" + message);
        super.send(message);
    }

    private void publisherCreateHandle() {
        JSONObject transaction = janusTransactions.addTransaction("attach", (type, jo) -> {
            if (type != JanusTransactions.Listener.CallBackType.SUCCESS)
                return;
            JSONObject data = jo.optJSONObject("data");
            if (data == null)
                return;
            JanusHandle janusHandle = new JanusHandle();
            janusHandle.handleId = new BigInteger(data.optString("id"));
            janusHandle.onJoined = jh -> delegate.onPublisherJoined(jh.handleId);
            janusHandle.onRemoteJsep = (jh, jsep) -> delegate.onPublisherRemoteJsep(jh.handleId, jsep);
            handles.put(janusHandle.handleId, janusHandle);
            publisherJoinRoom(janusHandle);
        });

        try {
            transaction.putOpt("plugin", "janus.plugin.videoroom");
            transaction.putOpt("session_id", mSessionId);
        } catch (JSONException e) {
            throw new RuntimeException(e);
        }
        send(transaction.toString());
    }

    private void publisherJoinRoom(JanusHandle handle) {
        JSONObject msg = janusTransactions.addTransaction("message", null);
        JSONObject body = new JSONObject();
        try {
            body.putOpt("request", "join");
            body.putOpt("room", _room_id);
            body.putOpt("ptype", "publisher");
            body.putOpt("display", displayName);

            msg.putOpt("body", body);
            msg.putOpt("session_id", mSessionId);
            msg.putOpt("handle_id", handle.handleId);
        } catch (JSONException e) {
            e.printStackTrace();
        }
        send(msg.toString());
    }

    public void publisherCreateOffer(final BigInteger handleId, final SessionDescription sdp) {
        JSONObject publish = new JSONObject();
        JSONObject jsep = new JSONObject();
        JSONObject message = janusTransactions.addTransaction("message", null);
        try {
            publish.putOpt("request", "configure");
            publish.putOpt("bitrate", _peerConnectionParameters.videoStartBitrate);

            publish.putOpt("video", true);
            publish.putOpt("videocodec", _peerConnectionParameters.videoCodec);

            if (_peerConnectionParameters.audioCodec != null) {
                publish.putOpt("audio", true);
                publish.putOpt("audiocodec", _peerConnectionParameters.audioCodec);
            }

            publish.putOpt("record", true);
            publish.putOpt("filename", displayName);

            jsep.putOpt("type", sdp.type);
            jsep.putOpt("sdp", sdp.description);

            message.putOpt("body", publish);
            message.putOpt("jsep", jsep);
            message.putOpt("session_id", mSessionId);
            message.putOpt("handle_id", handleId);

        } catch (JSONException e) {
            e.printStackTrace();
        }
        send(message.toString());
    }

    public void subscriberCreateAnswer(final BigInteger handleId, final SessionDescription sdp) {
        JSONObject body = new JSONObject();
        JSONObject jsep = new JSONObject();
        JSONObject message = janusTransactions.addTransaction("message", null);

        try {
            body.putOpt("request", "start");
            body.putOpt("room", _room_id);

            jsep.putOpt("type", sdp.type);
            jsep.putOpt("sdp", sdp.description);

            message.putOpt("body", body);
            message.putOpt("jsep", jsep);
            message.putOpt("session_id", mSessionId);
            message.putOpt("handle_id", handleId);
            Log.e(TAG, "-------------"  + message.toString());

        } catch (JSONException e) {
            e.printStackTrace();
        }

        send(message.toString());
    }

    public void trickleCandidate(final BigInteger handleId, final IceCandidate iceCandidate) {
        JSONObject candidate = new JSONObject();
        JSONObject message = janusTransactions.addTransaction("trickle", null);
        try {
            candidate.putOpt("candidate", iceCandidate.sdp);
            candidate.putOpt("sdpMid", iceCandidate.sdpMid);
            candidate.putOpt("sdpMLineIndex", iceCandidate.sdpMLineIndex);

            message.putOpt("candidate", candidate);
            message.putOpt("session_id", mSessionId);
            message.putOpt("handle_id", handleId);
        } catch (JSONException e) {
            e.printStackTrace();
        }
        send(message.toString());
    }

    public void trickleCandidateComplete(final BigInteger handleId) {
        JSONObject candidate = new JSONObject();
        JSONObject message = janusTransactions.addTransaction("trickle", null);
        try {
            candidate.putOpt("completed", true);

            message.putOpt("candidate", candidate);
            message.putOpt("session_id", mSessionId);
            message.putOpt("handle_id", handleId);
        } catch (JSONException e) {
            e.printStackTrace();
        }
    }

    private void subscriberCreateHandle(final BigInteger feed, final String display) {

        JSONObject transaction = janusTransactions.addTransaction("attach",
                (type, jo) -> {

            JSONObject data = jo.optJSONObject("data");
            if (data == null)
                return;
            JanusHandle janusHandle = new JanusHandle();
            janusHandle.handleId = new BigInteger(data.optString("id"));
            janusHandle.feedId = feed;
            janusHandle.display = display;
            janusHandle.onRemoteJsep = (jh, jsep) -> delegate.subscriberHandleRemoteJsep(jh.handleId, jsep);
            janusHandle.onLeaving = this::subscriberOnLeaving;
            handles.put(janusHandle.handleId, janusHandle);
            feeds.put(janusHandle.feedId, janusHandle);
            subscriberJoinRoom(janusHandle);
        });

        try {
            transaction.putOpt("plugin", "janus.plugin.videoroom");
            transaction.putOpt("session_id", mSessionId);
        } catch (JSONException e) {
            e.printStackTrace();
        }

        send(transaction.toString());
    }

    private void subscriberJoinRoom(JanusHandle handle) {
        JSONObject msg = janusTransactions.addTransaction("message", null);
        JSONObject body = new JSONObject();
        try {
            body.putOpt("request", "join");
            body.putOpt("room", _room_id);
            body.putOpt("ptype", "listener");
            body.putOpt("feed", handle.feedId);

            msg.putOpt("body", body);
            msg.putOpt("session_id", mSessionId);
            msg.putOpt("handle_id", handle.handleId);
        } catch (JSONException e) {
            e.printStackTrace();
        }
        send(msg.toString());
    }

    private void subscriberOnLeaving(final JanusHandle handle) {
        JSONObject transaction = janusTransactions.addTransaction("detatch",
                (type, jo) -> {
            if (type != JanusTransactions.Listener.CallBackType.SUCCESS)
                return;
            delegate.onLeaving(handle.handleId);
            handles.remove(handle.handleId);
            feeds.remove(handle.feedId);
        });

        try {
            transaction.putOpt("session_id", mSessionId);
            transaction.putOpt("handle_id", handle.handleId);
        } catch (JSONException e) {
            e.printStackTrace();
        }
        send(transaction.toString());
    }

    private final Runnable fireKeepAlive = new Runnable() {
        @Override
        public void run() {
            JSONObject msg = janusTransactions.addTransaction("keepalive", null);
            try {
                msg.putOpt("session_id", mSessionId);
            } catch (JSONException e) {
                e.printStackTrace();
            }
            send(msg.toString());
            keepaliveHandler.postDelayed(fireKeepAlive, 30000);
        }
    };

    @Override
    public void onClose(int code, String reason, boolean remote) {
        Log.e(TAG, "Connection closed by " + ( remote ? "remote peer" : "us" ) + " Code: " + code + " Reason: " + reason );
        keepaliveThread.quitSafely();
    }

    @Override
    public void onError(Exception ex) {
        Log.e(TAG, "onFailure " + ex.getMessage());
        ex.printStackTrace();
        keepaliveThread.quitSafely();
    }
}
