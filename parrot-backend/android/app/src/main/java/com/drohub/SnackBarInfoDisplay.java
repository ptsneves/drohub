package com.drohub;

import android.view.View;
import com.google.android.material.snackbar.Snackbar;

public class SnackBarInfoDisplay extends InfoDisplayBase {
    final private Snackbar _snackbar;
    public SnackBarInfoDisplay(View root_view, long refresh_period_ms) {
        super(refresh_period_ms);
        _snackbar = Snackbar.make(root_view, "", Snackbar.LENGTH_INDEFINITE);
    }

    @Override
    protected void setText(String text) {
        _snackbar.setText(text);
    }

    @Override
    protected void show() {
        _snackbar.show();
    }

    @Override
    protected void hide() {
        _snackbar.dismiss();
    }
}
