package com.drohub.Janus;

import android.util.Log;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.Random;
import java.util.concurrent.ConcurrentHashMap;

public class JanusTransactions {
    public interface Listener {
        enum CallBackType {
            ERROR,
            SUCCESS,
            ACK,
        }
        void onTransaction(CallBackType type, JSONObject jo);
    }

    final private ConcurrentHashMap<String, JanusTransaction> transactions = new ConcurrentHashMap<>();
    public static boolean isTransaction(JSONObject jsonObject) {
        String response_type = jsonObject.optString("janus");
        return response_type.equals("success") ||
                response_type.equals("error") ||
                response_type.equals("ack");
    }

    public void processTransaction(JSONObject jsonObject) {
        String response_type = jsonObject.optString("janus");
        String transaction = jsonObject.optString("transaction");

        JanusTransaction jt = transactions.get(transaction);
        if (jt == null) {
            Log.e("JanusTransactions", "Notified of a transaction not on record");
            transactions.remove(transaction);
            return;
        }

        switch (response_type) {
            case "success":
                jt.listener.onTransaction(Listener.CallBackType.SUCCESS, jsonObject);
                break;
            case "error":
                jt.listener.onTransaction(Listener.CallBackType.ERROR, jsonObject);
                break;
            case "ack":
                jt.listener.onTransaction(Listener.CallBackType.ACK, jsonObject);
                break;
        }

        transactions.remove(transaction);
    }

    private String randomString(Integer length) {
        final String str = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZz";
        final Random rnd = new Random();
        StringBuilder sb = new StringBuilder(length);
        for (int i = 0; i < length; i++) {
            sb.append(str.charAt(rnd.nextInt(str.length())));
        }
        return sb.toString();
    }

    private void doNoOp(Listener.CallBackType type, JSONObject jo) {

    }

    private String addTransaction(@Nullable JanusTransactions.Listener listener) {
        Listener l = (listener == null) ? this::doNoOp : listener;
        JanusTransaction t = new JanusTransaction(randomString(12), l);
        transactions.put(t.tid, t);
        return t.tid;
    }

    public JSONObject addTransaction(String verb, @Nullable JanusTransactions.Listener listener) {
        JSONObject msg = new JSONObject();
        try {
            msg.putOpt("janus", verb);
            msg.putOpt("transaction", addTransaction(listener));
        } catch (JSONException e) {
            throw new RuntimeException(e);
        }
        return msg;
    }

    private class JanusTransaction {
        JanusTransaction(String tid, @NonNull Listener listener) {
            this.listener = listener;
            this.tid = tid;
        }
        final String tid;
        final JanusTransactions.Listener listener;
    }
}
