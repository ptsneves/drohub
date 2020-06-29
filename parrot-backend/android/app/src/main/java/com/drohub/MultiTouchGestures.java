package com.drohub;

import android.content.Context;
import android.view.GestureDetector;
import android.view.MotionEvent;
import android.view.ScaleGestureDetector;
import android.view.View;

public class MultiTouchGestures implements View.OnTouchListener, ScaleGestureDetector.OnScaleGestureListener, GestureDetector.OnGestureListener {
    public interface OnScaleListener {
        boolean onScale(float scale_factor);
    }

    public interface OnScrollListener {
        boolean onScroll(float dx, float dy);
    }

    private ScaleGestureDetector gesture_scale;
    private GestureDetector gesture_detector;

    private float _max_scale_factor = 10;
    private float _min_scale_factor = 1;
    private float _scale_factor = 1;

    private float _max_y_scroll = 10;
    private float _min_y_scroll = 0;
    private float _scroll_y_pos = 1;

    private float _max_x_scroll = 10;
    private float _min_x_scroll = 0;
    private float _scroll_x_pos = 1;

    private OnScrollListener _scroll_l;
    private OnScaleListener _scale_l;

    public MultiTouchGestures (Context c){
        gesture_scale = new ScaleGestureDetector(c, this);
        gesture_detector = new GestureDetector(c, this);
    }

    public void setOnScrollListener(float max_x_scroll, float min_x_scroll,
                                    float max_y_scroll, float min_y_scroll,
                                    OnScrollListener scroll_l) {

        _max_x_scroll = max_x_scroll;
        _min_x_scroll = min_x_scroll;

        _max_y_scroll = max_y_scroll;
        _min_y_scroll = min_y_scroll;
        _scroll_l = scroll_l;
    }

    public void setOnScaleListener(float min_scale_factor, float max_scale_factor, float initial_scale_factor, OnScaleListener scale_l) {
        _min_scale_factor = min_scale_factor;
        _max_scale_factor = max_scale_factor;
        _scale_factor = initial_scale_factor;
        _scale_l = scale_l;
    }

    @Override
    public boolean onTouch(View view, MotionEvent event) {
        gesture_detector.onTouchEvent(event);
        gesture_scale.onTouchEvent(event);
        return true;
    }

    @Override
    public boolean onScale(ScaleGestureDetector detector) {
        if (_scale_l == null)
            return false;
        _scale_factor *= detector.getScaleFactor();
        _scale_factor = Math.max(_scale_factor, _min_scale_factor);// prevent our view from becoming too small //
        _scale_factor = Math.min(_scale_factor, _max_scale_factor);// prevent our view from becoming too big //
        _scale_factor = ((float)((int)(_scale_factor * 100))) / 100; // Change precision to help with jitter when user just rests their fingers //

        return _scale_l.onScale(_scale_factor);
    }

    @Override
    public boolean onScroll(MotionEvent event1, MotionEvent event2, float distanceX, float distanceY) {
        if (_scroll_l == null)
            return false;
        float adim_dx = -distanceX/ Math.abs(_max_x_scroll - _min_x_scroll);
        float adim_dy = -distanceY/ Math.abs(_max_y_scroll - _min_y_scroll);

        return _scroll_l.onScroll(adim_dx, adim_dy);
    }


    @Override
    public boolean onDown(MotionEvent e) { return false; }

    @Override
    public void onShowPress(MotionEvent e) {    }

    @Override
    public boolean onSingleTapUp(MotionEvent e) {
        return false;
    }

    @Override
    public void onLongPress(MotionEvent e) {    }

    @Override
    public boolean onFling(MotionEvent e1, MotionEvent e2, float velocityX, float velocityY) { return false;}

    @Override
    public boolean onScaleBegin(ScaleGestureDetector detector) {  return true;}

    @Override
    public void onScaleEnd(ScaleGestureDetector detector) { }
}
