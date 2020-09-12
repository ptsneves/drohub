package com.drohub.Devices.Peripherals.Parrot;

import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.MediaStore;

public class ParrotMediaStore {


    public interface StoredPhotoCountListener {
        void onChange(int new_photo_count);
    }

    public interface StoredVideoCountListener {
        void onChange(int new_video_count);
    }

    final ParrotMediaStorePriv _media_store_priv;

    public ParrotMediaStore(Drone drone) {
        _media_store_priv = new ParrotMediaStorePriv(drone);
    }

    public void setStoredPhotoCountListener(StoredPhotoCountListener l) {
        _media_store_priv.setStoredPhotoCountListener(l);
    }

    public void setStoredVideoCountListener(StoredVideoCountListener l) {
        _media_store_priv.setStoredVideoCountListener(l);
    }

    private class ParrotMediaStorePriv extends ParrotPeripheralPrivBase<MediaStore> {
        private int _photo_count;
        private int _video_count;
        private StoredPhotoCountListener _stored_photo_count_listener;
        private StoredVideoCountListener _stored_video_count_listener;

        ParrotMediaStorePriv(Drone drone) {
            super(drone, MediaStore.class);
        }

        void setStoredPhotoCountListener(StoredPhotoCountListener l) {
            _stored_photo_count_listener = l;
        }

        void setStoredVideoCountListener(StoredVideoCountListener l) {
            _stored_video_count_listener = l;
        }

        @Override
        public void onChange(MediaStore media_store) {
            int new_photo_count = media_store.getPhotoMediaCount();
            int new_video_count = media_store.getVideoMediaCount();

            if (new_photo_count != _photo_count && _stored_photo_count_listener != null)
                _stored_photo_count_listener.onChange(new_photo_count);

            if (new_video_count != _video_count && _stored_video_count_listener != null)
                _stored_video_count_listener.onChange(new_video_count);

            if (_peripheral_listener != null)
                _peripheral_listener.onChange(media_store);

            _photo_count = new_photo_count;
            _video_count = new_video_count;
        }

        @Override
        public boolean onFirstTimeAvailable(MediaStore media_store) {
            _photo_count = media_store.getPhotoMediaCount();
            _video_count = media_store.getVideoMediaCount();

            if (_peripheral_listener != null)
                return _peripheral_listener.onFirstTimeAvailable(media_store);
            return true;
        }

    }

}
