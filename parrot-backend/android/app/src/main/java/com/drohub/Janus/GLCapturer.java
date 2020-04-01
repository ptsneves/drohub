/*
 *  Copyright 2016 The WebRTC Project Authors. All rights reserved.
 *
 *  Use of this source code is governed by a BSD-style license
 *  that can be found in the LICENSE file in the root of the source
 *  tree. An additional intellectual property rights grant can be found
 *  in the file PATENTS.  All contributing project authors may
 *  be found in the AUTHORS file in the root of the source tree.
 */
package com.drohub.Janus;
import android.content.Context;
import com.drohub.hud.LiveVideoRenderer;
import com.parrot.drone.groundsdk.device.peripheral.StreamServer;
import org.webrtc.*;

import java.io.InvalidObjectException;


public class GLCapturer implements VideoCapturer {

    private CapturerObserver _capturer_observer;
    private EglBase.Context _context;
    private LiveVideoRenderer _renderer;
    private final StreamServer _stream_server;

    public GLCapturer(EglBase.Context context, StreamServer stream_server) {
        _context = context;
        _stream_server = stream_server;
    }

    @Override
    public void initialize(SurfaceTextureHelper surfaceTextureHelper, Context applicationContext,
                           CapturerObserver capturerObserver) {
        this._capturer_observer = capturerObserver;
    }

    @Override
    public void startCapture(int width, int height, int framerate) {
        try {
            _renderer = new LiveVideoRenderer(_stream_server, _capturer_observer, _context, width, height);
            _renderer.startStream();
        } catch (InvalidObjectException e) {
            e.printStackTrace();
        }
    }

    @Override
    public void stopCapture() {
        if (_renderer != null)
            _renderer.stopStream();
    }

    @Override
    public void changeCaptureFormat(int width, int height, int framerate) {
        // Empty on purpose
    }

    @Override
    public void dispose() {
    }

    @Override
    public boolean isScreencast() {
        return false;
    }
}