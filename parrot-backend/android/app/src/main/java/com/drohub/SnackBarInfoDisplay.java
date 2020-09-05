package com.drohub;

import android.os.Build;
import android.view.Gravity;
import android.view.View;
import android.widget.TextView;
import com.google.android.material.snackbar.Snackbar;

public class SnackBarInfoDisplay extends InfoDisplayBase {
    final private Snackbar _snackbar;
    public SnackBarInfoDisplay(View root_view, long refresh_period_ms) {
        super(refresh_period_ms);
        _snackbar = Snackbar.make(root_view, "", Snackbar.LENGTH_INDEFINITE);
        View view = _snackbar.getView();
        TextView tv = view.findViewById(com.google.android.material.R.id.snackbar_text);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M)
            tv.setTextAlignment(View.TEXT_ALIGNMENT_CENTER);
        else
            tv.setGravity(Gravity.CENTER_HORIZONTAL);
    }

    @Override
    protected void setText(String text) {
        _snackbar.setText(text);
        _snackbar.show();
    }

    @Override
    protected void hide() {
        _snackbar.dismiss();
    }
}
