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
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.PointF;
import android.graphics.drawable.Drawable;
import android.util.AttributeSet;
import android.view.View;

import androidx.core.content.ContextCompat;

import com.drohub.R;


public class HeadingView extends View {

    private Paint mPaint;

    private PointF mCenter;

    private float mRadius;

    private float mHeading;

    private Drawable mHeadingDrawable;

    public HeadingView(Context context) {
        super(context);
        init();
    }

    public HeadingView(Context context, AttributeSet attrs) {
        this(context, attrs, 0);
    }

    public HeadingView(Context context, AttributeSet attrs, int defStyleAttr) {
        super(context, attrs, defStyleAttr);
        init();
    }

    private void init() {
        Context context = getContext();

        mCenter = new PointF();

        mPaint = new Paint();
        mPaint.setStyle(Paint.Style.STROKE);
        mPaint.setColor(ContextCompat.getColor(context, android.R.color.black));
        mPaint.setStrokeWidth(5);
        mPaint.setAntiAlias(true);

        mHeadingDrawable = ContextCompat.getDrawable(context, R.drawable.ic_heading);
    }

    @Override
    protected void onMeasure(int widthMeasureSpec, int heightMeasureSpec) {
        super.onMeasure(widthMeasureSpec, heightMeasureSpec);
        mCenter.x = getMeasuredWidth() / 2.0f;
        mCenter.y = getMeasuredHeight() / 2.0f;
        mRadius = Math.min(getMeasuredWidth() - getPaddingStart() - getPaddingEnd(),
                getMeasuredHeight() - getPaddingTop() - getPaddingBottom()) / 2.0f;
        mHeadingDrawable.setBounds((int) (mCenter.x - mRadius / 2.0f), (int) (mCenter.y - mRadius / 2.0f),
                (int) (mCenter.x + mRadius / 2.0f), (int) (mCenter.y + mRadius / 2.0f));
    }

    public void setHeading(float heading) {
        if (Double.compare(mHeading, heading) != 0) {
            mHeading = heading;
            invalidate();
        }
    }

    @Override
    protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);
        canvas.drawCircle(mCenter.x, mCenter.y, mRadius, mPaint);
        canvas.save();
        canvas.rotate(mHeading, mCenter.x, mCenter.y);
        mHeadingDrawable.draw(canvas);
        canvas.restore();
    }
}
