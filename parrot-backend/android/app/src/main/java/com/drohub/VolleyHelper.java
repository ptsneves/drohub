package com.drohub;

import com.android.volley.Cache;
import com.android.volley.Network;
import com.android.volley.RequestQueue;
import com.android.volley.toolbox.BasicNetwork;
import com.android.volley.toolbox.DiskBasedCache;
import com.android.volley.toolbox.HurlStack;

import java.io.File;

public class VolleyHelper {
    private final Cache _volley_cache;
    private final Network _volley_network;
    private final RequestQueue _request_queue;

    public VolleyHelper(File root_directory) {
        _volley_cache = new DiskBasedCache(root_directory, 1024); // 1kB cap
        _volley_network = new BasicNetwork(new HurlStack());
        _request_queue = new RequestQueue(_volley_cache, _volley_network);
        _request_queue.start();
    }

    public RequestQueue getRequestQueue() {
        return _request_queue;
    }
}
