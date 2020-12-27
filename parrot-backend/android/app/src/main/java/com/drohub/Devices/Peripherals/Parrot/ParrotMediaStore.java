package com.drohub.Devices.Peripherals.Parrot;

import android.graphics.Bitmap;
import androidx.annotation.NonNull;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.Models.FileEntry;
import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.MediaStore;
import com.parrot.drone.groundsdk.device.peripheral.media.MediaDestination;
import com.parrot.drone.groundsdk.device.peripheral.media.MediaItem;

import java.io.*;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Optional;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.stream.Collectors;

public class ParrotMediaStore implements IPeripheral<ParrotMediaStore>, IPeripheral.IMediaStoreProvider {

    final ParrotMediaStorePriv _media_store_priv;

    public ParrotMediaStore(Drone drone, int thumbnail_quality, Bitmap.CompressFormat thumbnail_format) {
        _media_store_priv = new ParrotMediaStorePriv(drone, thumbnail_quality, thumbnail_format);
    }

    public void setNewPhotosListener(OnNewPhotoListener l) {
        _media_store_priv.setNewPhotosListener(l);
    }

    public void setNewVideosListener(OnNewVideosListener l) {
        _media_store_priv.setNewVideosListener(l);
    }

    @Override
    public String getMimeType(FileEntry file_entry) {
        if (file_entry.resource_type == FileEntry.FileResourceType.IMAGE)
            return "image/jpeg";
        else if (file_entry.resource_type == FileEntry.FileResourceType.VIDEO)
            return "video/mp4";
        return  "application/octet-stream";
    }

    @Override
    public String getExtension(FileEntry file_entry) {
        if (file_entry.resource_type == FileEntry.FileResourceType.IMAGE)
            return ".jpeg";
        else if (file_entry.resource_type == FileEntry.FileResourceType.VIDEO)
            return ".mp4";
        return  "bin";
    }

    @Override
    public void setPeripheralListener(IPeripheralListener<ParrotMediaStore> l) {
        _media_store_priv.setPeripheralListener(ParrotPeripheralManager.PeripheralListener.convert(l, this));
    }

    @Override
    public void start() {
        _media_store_priv.start();
    }

    public List<FileEntry> getPhotos() {
        return _media_store_priv.toFileEntries(_media_store_priv._photo_items);
    }

    public List<FileEntry> getVideos() {
        return _media_store_priv.toFileEntries(_media_store_priv._video_items);
    }

    @Override
    public void getThumbnail(FileEntry file, InputStreamListener l) throws IllegalAccessException {
        _media_store_priv.getThumbnail(file, l);
    }

    @Override
    public void getMedia(FileEntry file, InputStreamListener l) throws IllegalAccessException {
        _media_store_priv.getMedia(file, l);
    }

    private class ParrotMediaStorePriv extends ParrotPeripheralPrivBase<MediaStore> {
        private final String _serial_number;
        private int _thumbnail_quality;
        private Bitmap.CompressFormat _thumbnail_format;
        private MediaDestination _media_destination;
        private MediaStore _media_store;
        private OnNewPhotoListener _new_photo_listener;
        private OnNewVideosListener _new_video_listener;
        private List<MediaItem> _video_items;
        private List<MediaItem> _photo_items;

        ParrotMediaStorePriv(Drone drone, int thumbnail_quality, Bitmap.CompressFormat thumbnail_format) {
            super(drone, MediaStore.class);
            _serial_number = drone.getUid();
            _video_items = new ArrayList<>();
            _photo_items = new ArrayList<>();
            _thumbnail_quality = thumbnail_quality;
            _thumbnail_format = thumbnail_format;
            _media_destination = MediaDestination.platformMediaStore(IMediaStoreProvider.AlbumName);
        }

        private void setNewPhotosListener(OnNewPhotoListener l) {
            _new_photo_listener = l;
        }

        private void setNewVideosListener(OnNewVideosListener l) {
            _new_video_listener = l;
        }


        @Override
        public void onChange(@NonNull MediaStore media_store) {

            if (_peripheral_listener != null)
                _peripheral_listener.onChange(media_store);
        }

        private void getThumbnail(FileEntry file, InputStreamListener l) throws IllegalAccessException {
            if (_media_store == null)
                throw new IllegalAccessException("Media store not available");

            final AtomicBoolean ran = new AtomicBoolean(false);
            _media_store.fetchThumbnailOf(toMediaItem(file), obj -> {
                if (obj == null || ran.getAndSet(true))
                    return;

                try {
                    File thumbnail_temp_file = File.createTempFile("ParrotMediaStore", file.serial_number);
                    OutputStream tempo_file_stream = new FileOutputStream(thumbnail_temp_file);
                    obj.compress(_thumbnail_format, _thumbnail_quality, tempo_file_stream);

                    tempo_file_stream.flush();
                    tempo_file_stream.close();
                    thumbnail_temp_file.deleteOnExit();
                    l.onAvailable(new FileInputStream(thumbnail_temp_file));
                } catch (IOException | IllegalAccessException e) {
                    return;
                }
            });
        }

        private void getMedia(FileEntry file_entry, InputStreamListener l) throws IllegalAccessException {
            if (_media_store == null)
                throw new IllegalAccessException("Media store not available");

            List media_item = toMediaItem(file_entry).getResources();
            final AtomicBoolean ran = new AtomicBoolean(false);
            _media_store.download(media_item, MediaDestination.platformMediaStore(AlbumName), obj -> {
                if (obj == null || ran.getAndSet(true))
                    return;

                try {
                    l.onAvailable(new FileInputStream(obj.getDownloadedFile()));
                } catch (IllegalAccessException | FileNotFoundException e) {
                }
            });
        }

        private FileEntry.FileResourceType toFileResourceType(MediaItem item) {
            switch (item.getType()) {
                case PHOTO:
                    return FileEntry.FileResourceType.IMAGE;
                case VIDEO:
                    return FileEntry.FileResourceType.VIDEO;
                default:
                    return FileEntry.FileResourceType.OTHER;
            }
        }

        private List<FileEntry> toFileEntries(List<MediaItem> src) {
            return src
                    .stream()
                    .map(
                            e -> new FileEntry(ParrotMediaStore.this,
                                    e.getUid(),
                                    toFileResourceType(e),
                                    e.getCreationDate().getTime(),
                                    _serial_number))
                    .collect(Collectors.toList());
        }

        private MediaItem toMediaItem(FileEntry entry) throws IllegalAccessException {
            List<MediaItem> items;
            if (entry.resource_type == FileEntry.FileResourceType.IMAGE) {
                items = _photo_items;
            }
            else if (entry.resource_type == FileEntry.FileResourceType.VIDEO) {
                items = _video_items;
            }
            else
                throw new RuntimeException("File type not known");

            Optional<MediaItem> item = items.stream()
                    .filter(e -> e.getUid().equals(entry.resource_id))
                    .findFirst();

            if (item.isPresent())
                return item.get();
            else
                throw new IllegalAccessException("File entry not known");
        }

        private synchronized void setMediaItemList(MediaItem.Type type, ArrayList<MediaItem> new_list) {

            ArrayList<MediaItem> diff_result = new ArrayList<>(new_list);
            List<MediaItem> old_list;
            switch (type) {
                case VIDEO:
                    old_list =_video_items;
                    _video_items = new_list;
                    if (_new_video_listener != null) {
                        diff_result.removeAll(new HashSet<>(old_list));
                        if (!diff_result.isEmpty())
                            _new_video_listener.onChange(toFileEntries(diff_result));
                    }
                    break;
                case PHOTO:
                    old_list = _photo_items;
                    _photo_items = new_list;
                    if (_new_photo_listener != null) {
                        diff_result.removeAll(new HashSet<>(old_list));
                        if (!diff_result.isEmpty())
                            _new_photo_listener.onChange(toFileEntries(diff_result));
                    }

                    break;
            }
        }

        private void onMediaItemChangedChanged(List<MediaItem> obj) {
            ArrayList<MediaItem> photo_list = new ArrayList<>();
            ArrayList<MediaItem> video_list = new ArrayList<>();

            for (MediaItem item: obj) {
                switch (item.getType()) {
                    case PHOTO:
                        photo_list.add(item);
                        break;
                    case VIDEO:
                        video_list.add(item);
                        break;
                }
            }

            setMediaItemList(MediaItem.Type.PHOTO, photo_list);
            setMediaItemList(MediaItem.Type.VIDEO, video_list);
        }

        @Override
        public boolean onFirstTimeAvailable(MediaStore media_store) {
            _media_store = media_store;
            media_store.browse(this::onMediaItemChangedChanged);

            if (_peripheral_listener != null)
                return _peripheral_listener.onFirstTimeAvailable(media_store);
            return true;
        }

    }

}
