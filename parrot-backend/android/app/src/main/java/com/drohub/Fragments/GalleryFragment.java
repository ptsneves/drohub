package com.drohub.Fragments;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Rect;
import android.net.ConnectivityManager;
import android.net.Network;
import android.net.NetworkCapabilities;
import android.net.NetworkRequest;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.GridView;
import android.widget.ImageView;
import androidx.annotation.NonNull;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.DroHubHelper;
import com.drohub.Models.FileEntry;
import com.drohub.R;
import com.drohub.api.GetSubscriptionMediaInfoHelper;
import com.drohub.api.UploadMediaHelper;

import java.io.InputStream;
import java.net.URISyntaxException;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.stream.Collectors;

public class GalleryFragment extends DeviceFragment {
    private ImageAdapter _image_adapter;
    private GridView _gallery_grid_view;
    private IPeripheral.IMediaStoreProvider _drone_media_store;
    private AtomicBoolean _has_unmetered_network;

    public GalleryFragment() {
        super(R.layout.fragment_gallery);
    }

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
        View ret = super.onCreateView(inflater, container, savedInstanceState);

        _has_unmetered_network = new AtomicBoolean(false);
        initializeNetworkMonitors();

        _image_adapter = new ImageAdapter(this.getContext());
        _gallery_grid_view = getFragmentViewById(R.id.gridview);
        _gallery_grid_view.setAdapter(_image_adapter);
        return ret;
    }

    private void initializeNetworkMonitors() {
        ConnectivityManager connection_manager = (ConnectivityManager) _view.getContext().getSystemService(Context.CONNECTIVITY_SERVICE);
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
            }

            @Override
            public void onCapabilitiesChanged(@NonNull Network network, @NonNull NetworkCapabilities networkCapabilities) {
                super.onCapabilitiesChanged(network, networkCapabilities);
            }
        });
        _has_unmetered_network.set(!connection_manager.isActiveNetworkMetered());
    }

    @Override
    public void onNewMediaStore(IPeripheral.IMediaStoreProvider media_store) {
        super.onNewMediaStore(_drone_media_store);

        String subscription_media_url;
        String upload_media_url;
        try {
            subscription_media_url = DroHubHelper.getURL(getContext(), R.string.get_subscription_media_info_url);
            upload_media_url = DroHubHelper.getURL(getContext(), R.string.upload_media_url);
        }
        catch (URISyntaxException e) {
            throw new RuntimeException(e);
        }

        UploadMediaHelper upload_media_helper = new UploadMediaHelper(_error_display,
                new UploadMediaHelper.Listener() {
                    @Override
                    public void onSuccess() {
                        _error_display.addTemporarily("File Upload", 2000);
                    }

                    @Override
                    public void onUploadError(String error) {
                    }

                    @Override
                    public boolean onProgress(int percent) {
                        return true;
                    }
                },
                _user_email,
                _user_auth_token,
                upload_media_url,
                false
        );

        GetSubscriptionMediaInfoHelper drohub_media_helper = new GetSubscriptionMediaInfoHelper(_error_display,
                error_message -> _error_display.addTemporarily(error_message, 2000),
                _user_email,
                _user_auth_token,
                subscription_media_url,
                drohub_media_store -> {
                    List<FileEntry> drohub_media_list = drohub_media_store.getAllMedia();
                    List<FileEntry> drone_media_list = media_store.getAllMedia();
                    drone_media_list
                            .stream()
                            .filter(drone_file ->
                                    drohub_media_list
                                            .stream()
                                            .anyMatch(drohub_file ->
                                                    drohub_file.creation_time_unix_ms == drone_file.creation_time_unix_ms
                                                            && !drohub_file.resource_id.isEmpty()
                                            )
                            )
                            .forEach(drone_file -> {
                                try {
                                    if (_has_unmetered_network.get())
                                        upload_media_helper.upload(drone_file);
                                }
                                catch (IllegalAccessException e) {
                                    _error_display.addTemporarily(e.getMessage(), 5000);
                                }
                            });
                });

        _drone_media_store = media_store;
        _drone_media_store.setNewMediaListener(media -> {
            _image_adapter.generateMediaList(media);
            drohub_media_helper.get();
        });
    }

    private class ImageAdapter extends BaseAdapter {
        private class Item {
            final ImageView image_view;
            final FileEntry file_entry;
            private boolean _retrieved;

            private Item(ImageView image_view, FileEntry file_entry) {
                this.image_view = image_view;
                this.file_entry = file_entry;
                _retrieved = false;
            }

            private void setRetrieved() {
                _retrieved = true;
            }

            private boolean isRetrieved() {
                return _retrieved;
            }
        }

        final private Context _context;
        private List<Item> _items;
        private List<FileEntry> _device_media;

        private ImageAdapter(Context context) {
            _context = context;
            _items = new ArrayList<>();
            _device_media = new ArrayList<>();
        }

        @Override
        public int getCount() {
            return _items.size();
        }

        @Override
        public Object getItem(int i) {
            return null;
        }

        @Override
        public long getItemId(int i) {
            return 0;
        }

        @Override
        public View getView(int i, View view, ViewGroup viewGroup) {
            Item item = _items.get(i);
            if (!item.isRetrieved())
                requestThumbnail(item);
            return item.image_view;
        }

        private void requestThumbnail(Item item) {
            try {
                _drone_media_store.getThumbnail(item.file_entry, input_stream -> {
                    item.setRetrieved();
                    item.image_view.setImageBitmap(decodeSampledBitmapFromResource(input_stream, 50, 50));
                });
            }
            catch (IllegalAccessException ignored) {
            }
        }

        // https://developer.android.com/topic/performance/graphics/load-bitmap
        public int calculateInSampleSize(BitmapFactory.Options options, int req_width, int req_height) {
            // Raw height and width of image
            final int height = options.outHeight;
            final int width = options.outWidth;
            int input_sample_sizes = 1;

            if (height > req_height || width > req_width) {

                final int half_height = height / 2;
                final int half_width = width / 2;

                // Calculate the largest inSampleSize value that is a power of 2 and keeps both
                // height and width larger than the requested height and width.
                while ((half_height / input_sample_sizes) >= req_height
                        && (half_width / input_sample_sizes) >= req_width) {
                    input_sample_sizes *= 2;
                }
            }

            return input_sample_sizes;
        }

        // https://developer.android.com/topic/performance/graphics/load-bitmap
        public Bitmap decodeSampledBitmapFromResource(InputStream input_stream,
                                                      int reqWidth, int reqHeight) {

            // First decode with inJustDecodeBounds=true to check dimensions
            final BitmapFactory.Options options = new BitmapFactory.Options();
            options.inJustDecodeBounds = true;
            BitmapFactory.decodeStream(input_stream, new Rect(), options);
            // Calculate inSampleSize
            options.inSampleSize = calculateInSampleSize(options, reqWidth, reqHeight);

            // Decode bitmap with inSampleSize set
            options.inJustDecodeBounds = false;
            return BitmapFactory.decodeStream(input_stream, new Rect(), options);
        }

        private synchronized void generateMediaList(List<FileEntry> files) {
            _device_media = files;

            _items = _device_media.stream()
                    .sorted(Comparator.comparingLong(f -> f.creation_time_unix_ms))
                    .map(this::createNewItem)
                    .collect(Collectors.toList());
            _image_adapter.notifyDataSetChanged();
        }

        private Item createNewItem(FileEntry f) {
            ImageView image_view = new ImageView(_context);
            image_view.setLayoutParams(new GridView.LayoutParams(200, 200));
            image_view.setScaleType(ImageView.ScaleType.CENTER_CROP);
            image_view.setPadding(8, 8, 8, 8);
            image_view.setImageResource(R.drawable.common_google_signin_btn_icon_light_normal);
            return new Item(image_view, f);
        }
    }
}
