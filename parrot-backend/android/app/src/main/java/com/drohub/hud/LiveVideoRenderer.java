package com.drohub.hud;

import android.graphics.Matrix;
import android.graphics.Rect;
import android.opengl.*;
import android.os.Handler;
import android.os.HandlerThread;
import android.os.SystemClock;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import com.parrot.drone.groundsdk.device.peripheral.StreamServer;
import com.parrot.drone.groundsdk.internal.stream.GlRenderSink;
import org.webrtc.*;
import org.webrtc.VideoFrame.TextureBuffer;

import java.io.InvalidObjectException;
import java.util.concurrent.TimeUnit;

import static android.os.Process.THREAD_PRIORITY_LOWEST;

public class LiveVideoRenderer implements GlRenderSink.Callback {
    /** Stream renderer. */
    @Nullable private GlRenderSink.Renderer _parrot_renderer;

    /** Rendering surface area. Also acts as ready indicator: when non-{@code null}, rendering may start. */
    @Nullable private Rect _gl_surface_rect;

    private static final int FramesPerSecond = 30;
    private final HandlerThread _render_thread;
    private final Handler _render_handler;
    private final StreamServer _stream_server;
    private final CapturerObserver _capturer_observer;
    private long elapsed_time_since_last_frame_ms;

    EglBase.Context _egl_context;
    EglBase _egl_base;

    public LiveVideoRenderer(StreamServer stream_server, CapturerObserver capturer_observer,
                             EglBase.Context egl_context, int width, int height) throws InvalidObjectException {

        if (stream_server == null)
            throw new InvalidObjectException("Stream server peripheral is invalid?");

        _stream_server = stream_server;
        _capturer_observer = capturer_observer;
        _render_thread = new HandlerThread("RenderingThread", THREAD_PRIORITY_LOWEST);
        _render_thread.start();
        _render_handler = new Handler(_render_thread.getLooper());
        _egl_context = egl_context;
        _gl_surface_rect = new Rect(0, 0, width, height);
        elapsed_time_since_last_frame_ms = System.currentTimeMillis();
    }

    public final void stopStream() {
        ThreadUtils.invokeAtFrontUninterruptibly(_render_handler, () -> {
            _stream_server.live(cameraLive -> {
                if (cameraLive != null) {
                    cameraLive.stop();
                }
                if(_stream_server!= null && _stream_server.streamingEnabled())
                    _stream_server.enableStreaming(false);
            });
        });
    }

    public final void startStream() {
        _stream_server.enableStreaming(true);
        _stream_server.live(cameraLive -> {
                if (cameraLive != null) {
                    cameraLive.openSink(GlRenderSink.config(this));
                    cameraLive.play();
                }
            }
        );
    }

    @Override
    public void onRenderingMayStart(@NonNull GlRenderSink.Renderer parrot_renderer) {
        _render_handler.post(() ->{
            if (_egl_base == null) {
                _egl_base = EglBase.create(_egl_context, EglBase.CONFIG_PIXEL_BUFFER);
                _egl_base.createDummyPbufferSurface();
                _egl_base.makeCurrent();
            }
            _parrot_renderer = parrot_renderer;
            if (_parrot_renderer.start(null)) {
                _parrot_renderer.setRenderZone(_gl_surface_rect);
            }
        });
    }

    public static long convertFramesPerSecondsToMillisPeriod(int frame_per_second) {
        return  (long)(1.0 / frame_per_second * 1000.0f);
    }

    @Override
    public void onRenderingMustStop(@NonNull GlRenderSink.Renderer parrot_renderer) {
        stopStream();
    }

    @Override
    public void onFrameReady(@NonNull GlRenderSink.Renderer parrot_renderer) {
        if(System.currentTimeMillis() - elapsed_time_since_last_frame_ms < convertFramesPerSecondsToMillisPeriod(FramesPerSecond))
            return;

        VideoFrame videoFrame = new VideoFrame(createRgbTextureBuffer(),
                0 /* rotation */,
                TimeUnit.MILLISECONDS.toNanos(SystemClock.elapsedRealtime()));
        _capturer_observer.onFrameCaptured(videoFrame);
        elapsed_time_since_last_frame_ms = System.currentTimeMillis();
        videoFrame.release();
    }

    @Override
    public void onContentZoneChange(@NonNull Rect new_live_video_dimensions) {
        _render_handler.post(() -> {
            _gl_surface_rect = new_live_video_dimensions;
            _parrot_renderer.setRenderZone(_gl_surface_rect);
        });
    }

    public TextureBuffer createRgbTextureBuffer() {
        return ThreadUtils.invokeAtFrontUninterruptibly(_render_handler, () -> {

            final GlTextureFrameBuffer textureFrameBuffer = new GlTextureFrameBuffer(GLES20.GL_RGBA);
            textureFrameBuffer.setSize(_gl_surface_rect.width(), _gl_surface_rect.height());

            GLES20.glBindFramebuffer(GLES20.GL_FRAMEBUFFER, textureFrameBuffer.getFrameBufferId());
            GlUtil.checkNoGLES2Error("glBindFramebuffer");
            GLES20.glClearColor(0,0,0,0);

            _parrot_renderer.renderFrame();
            GLES20.glBindFramebuffer(GLES20.GL_FRAMEBUFFER, 0);
            GLES20.glFinish();

            final YuvConverter yuvConverter = new YuvConverter();
            return new TextureBufferImpl(_gl_surface_rect.width(), _gl_surface_rect.height(), VideoFrame.TextureBuffer.Type.RGB,
                    textureFrameBuffer.getTextureId(),
                    new Matrix(), _render_handler, yuvConverter,
                    () -> _render_handler.post(() -> {
                textureFrameBuffer.release();
                yuvConverter.release();
            }));
        });
    }
}
