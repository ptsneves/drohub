package com.drohub.hud;

import android.content.Context;
import android.graphics.Matrix;
import android.graphics.Rect;
import android.opengl.GLES20;
import android.os.Handler;
import android.os.HandlerThread;
import android.os.SystemClock;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import com.parrot.drone.groundsdk.internal.stream.GlRenderSink;
import org.webrtc.*;
import org.webrtc.VideoFrame.TextureBuffer;

import java.util.concurrent.TimeUnit;

import static android.os.Process.THREAD_PRIORITY_LOWEST;

public class GroundSDKVideoCapturer implements GlRenderSink.Callback, VideoCapturer{
    public interface IParrotStreamServerControl {
        boolean startStream(GlRenderSink.Callback render_sink_cb);
        boolean stopStream();
    }
    /** Stream renderer. */
    @Nullable private GlRenderSink.Renderer _parrot_renderer;

    /** Rendering surface area. Also acts as ready indicator: when non-{@code null}, rendering may start. */
    @Nullable private Rect _gl_surface_rect;
    private final Rect _original_rect;

    private static final int FramesPerSecond = 30;
    private final Handler _render_handler;
    private final  EglBase.Context _egl_context;
    private final IParrotStreamServerControl _stream_control;
    private CapturerObserver _capturer_observer;
    private long elapsed_time_since_last_frame_ms;
    private GlTextureFrameBuffer textureFrameBuffer;

    private EglBase _egl_base;

    public GroundSDKVideoCapturer(@NonNull IParrotStreamServerControl stream_control,
                                  EglBase.Context egl_context, int width, int height) {


        _stream_control = stream_control;
        HandlerThread _render_thread = new HandlerThread("RenderingThread", THREAD_PRIORITY_LOWEST);
        _render_thread.start();
        _render_handler = new Handler(_render_thread.getLooper());
        _egl_context = egl_context;
        _gl_surface_rect = new Rect(0, 0, width, height);
        _original_rect = new Rect(0, 0, width, height);
        elapsed_time_since_last_frame_ms = System.currentTimeMillis();
    }

    @Override
    public void onRenderingMayStart(@NonNull GlRenderSink.Renderer parrot_renderer) {
        _render_handler.post(() ->{
            if (_egl_base == null) {
                _egl_base = EglBase.create(_egl_context, EglBase.CONFIG_PIXEL_BUFFER);
                _egl_base.createDummyPbufferSurface();
                _egl_base.makeCurrent();
                textureFrameBuffer = new GlTextureFrameBuffer(GLES20.GL_RGBA);
            }
            _parrot_renderer = parrot_renderer;
            if (_parrot_renderer.start(null)) {
                _parrot_renderer.setScaleType(GlRenderSink.Renderer.ScaleType.FIT);
                _parrot_renderer.setPaddingFill(GlRenderSink.Renderer.PaddingFill.NONE);
                _parrot_renderer.setRenderZone(_gl_surface_rect);
            }
        });
    }

    public static long convertFramesPerSecondsToMillisPeriod(int frame_per_second) {
        return  (long)(1.0 / frame_per_second * 1000.0f);
    }

    @Override
    public void onRenderingMustStop(@NonNull GlRenderSink.Renderer parrot_renderer) {
        ThreadUtils.invokeAtFrontUninterruptibly(_render_handler, _stream_control::stopStream);
    }

    @Override
    public void onFrameReady(@NonNull GlRenderSink.Renderer parrot_renderer) {
        if(System.currentTimeMillis() - elapsed_time_since_last_frame_ms < convertFramesPerSecondsToMillisPeriod(FramesPerSecond))
            return;

        TextureBuffer tb = createRgbTextureBuffer();
        if (tb == null) {
            return;
        }
        VideoFrame videoFrame = new VideoFrame(tb,
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
            if (_parrot_renderer != null)
                _parrot_renderer.setRenderZone(_gl_surface_rect);
        });
    }

    public TextureBuffer createRgbTextureBuffer() {
        return ThreadUtils.invokeAtFrontUninterruptibly(_render_handler, () -> {
            if (_gl_surface_rect.width() <= 0 || _gl_surface_rect.height() <= 0) {
                _gl_surface_rect = _original_rect;
                _parrot_renderer.setRenderZone(_gl_surface_rect);
            }

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
                    () -> _render_handler.post(yuvConverter::release));
        });
    }

    @Override
    public void initialize(SurfaceTextureHelper surfaceTextureHelper, Context context, CapturerObserver capturerObserver) {
        _capturer_observer = capturerObserver;
    }

    @Override
    public void startCapture(int i, int i1, int i2) {
        _stream_control.startStream(this);
    }

    @Override
    public void stopCapture() {
        _stream_control.stopStream();
        if (textureFrameBuffer != null) {
            textureFrameBuffer.release();
            textureFrameBuffer = null;
        }
    }

    @Override
    public void changeCaptureFormat(int i, int i1, int i2) {
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