/*
 *     Copyright (C) 2019 Parrot Drones SAS
 *
 *     Redistribution and use in source and binary forms, with or without
 *     modification, are permitted provided that the following conditions
 *     are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in
 *       the documentation and/or other materials provided with the
 *       distribution.
 *     * Neither the name of the Parrot Company nor the names
 *       of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written
 *       permission.
 *
 *     THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 *     "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 *     LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
 *     FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
 *     PARROT COMPANY BE LIABLE FOR ANY DIRECT, INDIRECT,
 *     INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
 *     BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS
 *     OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED
 *     AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 *     OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT
 *     OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF
 *     SUCH DAMAGE.
 *
 */

package com.drohub.hud;

import android.content.Context;
import android.graphics.Rect;
import android.opengl.GLSurfaceView;
import android.util.AttributeSet;
import android.view.SurfaceHolder;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.parrot.drone.groundsdk.internal.stream.GlRenderSink;
import com.parrot.drone.groundsdk.stream.Stream;

import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

public class DroHubStreamView extends GLSurfaceView {
    /** GL surface view renderer , used entirely on rendering thread. */
    @NonNull
    private final Renderer mViewRenderer;

    /** Stream content zone, after scaling and excluding padding. */
    @NonNull
    private final Rect mContentZone;

    private Stream.Sink mSink;


    public DroHubStreamView(@NonNull Context context, AttributeSet set) {
        super(context, set);
        mContentZone = new Rect();
        mViewRenderer = new Renderer();

        setEGLContextClientVersion(2);
        setRenderer(mViewRenderer);
        setRenderMode(GLSurfaceView.RENDERMODE_WHEN_DIRTY);
        onPause();
    }

    @Override
    public void surfaceDestroyed(SurfaceHolder holder) {
        queueEvent(mViewRenderer::onSurfaceDestroyed);
        super.surfaceDestroyed(holder);
    }

    /**
     * Attaches stream to be rendered.
     * <p>
     * Client is responsible to detach (call setStream(null)) any stream before the view is disposed, otherwise, leak
     * may occur.
     *
     * @param stream stream to render, {@code null} to detach stream
     */
    public final void setStream(Stream stream) {
        if (mSink != null) {
            mSink.close();
            mSink = null;
        }
        if (stream != null)
            mSink = stream.openSink(GlRenderSink.config(mSinkCallback));
    }

    /**
     * Notifies that rendering starts.
     * <p>
     * Called on a dedicated GL rendering thread
     * <p>
     * Subclasses may override this method to implement any custom behavior that must happen on the GL rendering thread
     * when rendering starts. <br/>
     * Default implementation does nothing.
     */
    protected void onStartRendering() {
    }

    /**
     * Notifies that rendering stops.
     * <p>
     * Called on a dedicated GL rendering thread
     * <p>
     * Subclasses may override this method to implement any custom behavior that must happen on the GL rendering thread
     * when rendering stops. <br/>
     * Default implementation does nothing.
     */
    protected void onStopRendering() {
    }

    /** Listens to GL render sink events. Callbacks are called on main thread. */
    private final GlRenderSink.Callback mSinkCallback = new GlRenderSink.Callback() {

        @Override
        public void onRenderingMayStart(@NonNull GlRenderSink.Renderer renderer) {
            onResume();
            queueEvent(() -> mViewRenderer.setStreamRenderer(renderer));
        }

        @Override
        public void onRenderingMustStop(@NonNull GlRenderSink.Renderer renderer) {
            queueEvent(() -> mViewRenderer.setStreamRenderer(null));
            onPause();
        }

        @Override
        public void onFrameReady(@NonNull GlRenderSink.Renderer renderer) {
            requestRender();
        }

        @Override
        public void onContentZoneChange(@NonNull Rect contentZone) {
            mContentZone.set(contentZone);
        }
    };


    /** Custom GL surface view renderer. Manages surface lifecycle and rendering on a dedicated GL rendering thread. */
    private final class Renderer implements GLSurfaceView.Renderer {

        /** Stream renderer. */
        @Nullable
        private GlRenderSink.Renderer mStreamRenderer;

        /** Rendering surface area. Also acts as ready indicator: when non-{@code null}, rendering may start. */
        @Nullable
        private Rect mSurfaceZone;

        @Override
        public void onSurfaceCreated(GL10 gl, EGLConfig config) {
            onSurfaceReset();
        }

        @Override
        public void onSurfaceChanged(GL10 gl, int width, int height) {
            Rect prevZone = mSurfaceZone;
            mSurfaceZone = new Rect(0, 0, width, height);
            if (mStreamRenderer != null) {
                if (prevZone == null) {
                    startRenderer();
                } else {
                    mStreamRenderer.setRenderZone(mSurfaceZone);
                }
            }
        }

        @Override
        public void onDrawFrame(GL10 gl) {
            if (mStreamRenderer != null) {
                mStreamRenderer.renderFrame();
            }
        }

        /**
         * Installs stream renderer.
         * <p>
         * Once a renderer is installed, rendering starts as soon as the surface is ready.
         * <p>
         * Any previously installed renderer is stopped beforehand.
         *
         * @param renderer stream renderer, {@code null} to stop rendering
         */
        void setStreamRenderer(@Nullable GlRenderSink.Renderer renderer) {
            if (mStreamRenderer != null) {
                stopRenderer();
            }
            mStreamRenderer = renderer;
            if (mStreamRenderer != null && mSurfaceZone != null) {
                startRenderer();
            }
        }

        /**
         * Called when the surface is about to be destroyed.
         */
        void onSurfaceDestroyed() {
            onSurfaceReset();
        }

        /**
         * Called when the GL surface becomes invalid.
         */
        private void onSurfaceReset() {
            mSurfaceZone = null;
            if (mStreamRenderer != null) {
                stopRenderer();
            }
        }

        /**
         * Stops rendering.
         * <p>
         * {@link #mStreamRenderer} must be non-{@code null} before calling this method.
         */
        private void stopRenderer() {
            assert mStreamRenderer != null;
            if (mStreamRenderer.stop()) {
                post(mContentZone::setEmpty);
                onStopRendering();
            }
        }

        /**
         * Starts rendering.
         * <p>
         * Both {@link #mSurfaceZone} and {@link #mStreamRenderer} must be non-{@code null} before calling this method.
         */
        private void startRenderer() {
            assert mStreamRenderer != null && mSurfaceZone != null;
            if (mStreamRenderer.start(null)) {
                mStreamRenderer.setRenderZone(mSurfaceZone);
                onStartRendering();
            }
        }
    }
}