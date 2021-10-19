package com.drohub.Fragments.GalleryFragment;

import android.content.Context;
import android.net.ConnectivityManager;
import android.net.Network;
import android.net.NetworkCapabilities;
import android.net.NetworkRequest;
import androidx.annotation.NonNull;
import com.drohub.DroHubHelper;
import com.drohub.IInfoDisplay;
import com.drohub.Models.FileEntry;
import com.drohub.R;
import com.drohub.api.UploadMediaHelper;

import java.util.Iterator;
import java.util.List;
import java.util.concurrent.atomic.AtomicBoolean;


class GalleryUploadMediaHelper implements UploadMediaHelper.Listener {
    final private AtomicBoolean _has_unmetered_network;
    final private IInfoDisplay _error_display;
    final private Context _context;
    final private Iterator<FileEntry> _files;
    final private UploadMediaHelper _upload_media_helper;
    final private UploadMediaHelper.Listener _on_upload_listener;

    GalleryUploadMediaHelper(Context context,
                             IInfoDisplay error_display,
                             String user_email,
                             String user_auth_token,
                             List<FileEntry> files,
                             UploadMediaHelper.Listener progress) {
        _context = context;
        _error_display = error_display;
        _has_unmetered_network = new AtomicBoolean(false);
        _files = files.iterator();
        _on_upload_listener = progress;

        initializeNetworkMonitors();
        _upload_media_helper = new UploadMediaHelper(
                this,
                user_email,
                user_auth_token,
                DroHubHelper.getURL(_context, R.string.upload_media_url),
                false
        );
    }

    synchronized void upload() {
        if (_files.hasNext() && _has_unmetered_network.get()) {
            try {
                _upload_media_helper.upload(_files.next());
            }
            catch (IllegalAccessException e) {
                _error_display.addTemporarily(e.getMessage(), 3000);
            }
        }
        else {
            _error_display.addTemporarily("Lost unmettered connection. Stopping upload", 1000);
        }
    }

    @Override
    public void onSuccess(FileEntry f) {
        _on_upload_listener.onSuccess(f);
        upload();
    }

    @Override
    public void onUploadError(FileEntry f, String error) {
        _on_upload_listener.onUploadError(f, error);
    }

    @Override
    public boolean onProgress(FileEntry f, int percent) {
        return _on_upload_listener.onProgress(f, percent);
    }

    private void initializeNetworkMonitors() {
        ConnectivityManager connection_manager = (ConnectivityManager) _context.getSystemService(Context.CONNECTIVITY_SERVICE);
        NetworkRequest builder = new NetworkRequest.Builder()
                .addCapability(NetworkCapabilities.NET_CAPABILITY_NOT_METERED)
                .build();

        connection_manager.registerNetworkCallback(builder, new ConnectivityManager.NetworkCallback() {
            @Override
            public void onAvailable(@NonNull Network network) {
                super.onAvailable(network);
                _has_unmetered_network.set(true);
            }

            @Override
            public void onLost(@NonNull Network network) {
                super.onLost(network);
                _has_unmetered_network.set(false);
                _error_display.addTemporarily("Lost unmetered connection. Not uploading", 5000);
            }

            @Override
            public void onCapabilitiesChanged(@NonNull Network network, @NonNull NetworkCapabilities networkCapabilities) {
                super.onCapabilitiesChanged(network, networkCapabilities);
            }
        });
        _has_unmetered_network.set(!connection_manager.isActiveNetworkMetered());
    }
}
