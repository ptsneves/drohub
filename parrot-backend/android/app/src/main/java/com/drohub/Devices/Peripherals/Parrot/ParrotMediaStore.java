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

    public void setNewMediaListener(OnNewMediaListener l) {
        _media_store_priv.setNewMediaListener(l);
    }

    @Override
    public void setPeripheralListener(IPeripheralListener<ParrotMediaStore> l) {
        _media_store_priv.setPeripheralListener(ParrotPeripheralManager.PeripheralListener.convert(l, this));
    }

    @Override
    public void start() {
        _media_store_priv.start();
    }

    @Override
    public List<FileEntry> getAllMedia() {
        return _media_store_priv.toFileEntries(_media_store_priv._media_items);
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
        private final int _thumbnail_quality;
        private Bitmap.CompressFormat _thumbnail_format;
        private MediaStore _media_store;
        private OnNewMediaListener _new_media_listener;
        private List<MediaItem> _media_items;

        ParrotMediaStorePriv(Drone drone, int thumbnail_quality, Bitmap.CompressFormat thumbnail_format) {
            super(drone, MediaStore.class);
            _serial_number = drone.getUid();
            _media_items = new ArrayList<>();
            _thumbnail_quality = thumbnail_quality;
            _thumbnail_format = thumbnail_format;
        }

        private void setNewMediaListener(OnNewMediaListener l) {
            _new_media_listener = l;
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
            _media_store.fetchThumbnailOf(findMediaItem(file), obj -> {
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
                } catch (IOException | IllegalAccessException ignored) {
                }
            });
        }

        private void getMedia(FileEntry file_entry, InputStreamListener l) throws IllegalAccessException {
            if (_media_store == null)
                throw new IllegalAccessException("Media store not available");

            List media_item = findMediaItem(file_entry).getResources();
            final AtomicBoolean ran = new AtomicBoolean(false);
            _media_store.download(media_item, MediaDestination.platformMediaStore(AlbumName), obj -> {
                if (obj == null || ran.getAndSet(true))
                    return;

                try {
                    l.onAvailable(new FileInputStream(obj.getDownloadedFile()));
                } catch (IllegalAccessException | FileNotFoundException ignored) {
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
                                    e.getUid(),
                                    toFileResourceType(e),
                                    e.getCreationDate().getTime(),
                                    _serial_number,
                                    new ArrayList<>()))
                    .collect(Collectors.toList());
        }

        private MediaItem findMediaItem(FileEntry entry) throws IllegalAccessException {
            List<MediaItem> items;
            items = _media_items;

            Optional<MediaItem> item = items.stream()
                    .filter(e -> e.getUid().equals(entry.resource_id))
                    .findFirst();

            if (item.isPresent())
                return item.get();
            else
                throw new IllegalAccessException("File entry not known");
        }

        private synchronized void setMediaItemList(List<MediaItem> new_list) {
            ArrayList<MediaItem> diff_result = new ArrayList<>(new_list);
            List<MediaItem> old_list;
            old_list =_media_items;
            _media_items = new_list;
            if (_new_media_listener != null) {
                diff_result.removeAll(new HashSet<>(old_list));
                if (!diff_result.isEmpty())
                    _new_media_listener.onChange(toFileEntries(diff_result));
            }
        }

        @Override
        public boolean onFirstTimeAvailable(MediaStore media_store) {
            _media_store = media_store;
            media_store.browse(this::setMediaItemList);

            if (_peripheral_listener != null)
                return _peripheral_listener.onFirstTimeAvailable(media_store);
            return true;
        }
    }

}
