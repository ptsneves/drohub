package com.drohub.Fragments.GalleryFragment;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Rect;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.GridView;
import android.widget.ImageView;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.IInfoDisplay;
import com.drohub.Models.FileEntry;
import com.drohub.R;

import java.io.IOException;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.stream.Collectors;
import java.util.stream.Stream;

class ImageAdapter extends BaseAdapter {
    private static class Item {
        private final ImageView image_view;
        private final FileEntry file_entry;
        private final AtomicBoolean _retrieved;

        private Item(ImageView image_view, FileEntry file_entry) {
            this.image_view = image_view;
            this.file_entry = file_entry;
            _retrieved = new AtomicBoolean(false);
        }

        private void setRetrieved() {
            _retrieved.set(true);
        }

        private boolean isRetrieved() {
            return _retrieved.get();
        }
    }

    final private Context _context;
    private List<Item> _items;
    private final IInfoDisplay _error_display;

    ImageAdapter(Context context, IInfoDisplay error_display) {
        _context = context;
        _items = new ArrayList<>();
        _error_display = error_display;
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
        item.file_entry.media_provider.getThumbnail(item.file_entry, new IPeripheral.IMediaStoreProvider.InputStreamListener() {
            @Override
            public void onAvailable(FileEntry f, InputStream stream) {
                item.setRetrieved();
                try {
                    Bitmap d = decodeSampledBitmapFromResource(stream, 50, 50);
                    if (d == null)
                        _error_display.addTemporarily("Failed to decode a thumbnail", 2000);
                    else
                        item.image_view.setImageBitmap(d);
                }
                catch (IOException e) {
                    _error_display.addTemporarily(e.getMessage(), 2000);
                }
            }

            @Override
            public void onError(FileEntry f, Exception e) {
                _error_display.addTemporarily(e.getMessage(), 4000);
            }

            @Override
            public void onProgress(FileEntry f, int progress_percent) {

            }
        });
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
                                                  int reqWidth, int reqHeight) throws IOException {

        // First decode with inJustDecodeBounds=true to check dimensions
        final BitmapFactory.Options options = new BitmapFactory.Options();

        options.inJustDecodeBounds = true;

        //We need this because the stream is read once and we need to seek back to the beginning.
        //for the actual decode.
        input_stream.mark(input_stream.available());
        BitmapFactory.decodeStream(input_stream, new Rect(), options);
        // Calculate inSampleSize
        options.inSampleSize = calculateInSampleSize(options, reqWidth, reqHeight);
        input_stream.reset();
        // Decode bitmap with inSampleSize set
        options.inJustDecodeBounds = false;
        Bitmap result = BitmapFactory.decodeStream(input_stream, new Rect(), options);
        input_stream.reset();
        return result;
    }

    synchronized void generateMediaList(List<FileEntry> drohub_files, List<FileEntry> device_files) {
        _items = Stream.concat(drohub_files.stream(), device_files.stream())
                .sorted(Comparator.comparingLong(f -> f.creation_time_unix_ms))
                .map(this::createNewItem)
                .collect(Collectors.toList());
        notifyDataSetChanged();
    }

    private Item createNewItem(FileEntry f) {
        ImageView image_view = new ImageView(_context);
        image_view.setLayoutParams(new GridView.LayoutParams(200, 200));
        image_view.setScaleType(ImageView.ScaleType.CENTER_CROP);
        image_view.setPadding(8, 8, 8, 8);
        image_view.setImageResource(R.drawable.common_google_signin_btn_icon_light_normal);
        Item r = new Item(image_view, f);
        return r;
    }
}