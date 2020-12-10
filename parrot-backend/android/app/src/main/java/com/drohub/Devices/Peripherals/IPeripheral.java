package com.drohub.Devices.Peripherals;

import android.graphics.Bitmap;
import android.provider.ContactsContract;
import androidx.annotation.NonNull;
import com.drohub.Janus.PeerConnectionParameters;
import com.drohub.Models.FileEntry;

import java.io.IOException;
import java.io.InputStream;
import java.util.List;


public interface IPeripheral<P>{
    public void setPeripheralListener(IPeripheralListener<P> l);
    public void start();
    public interface IPeripheralListener<C> {
        void onChange(@NonNull C c);
        boolean onFirstTimeAvailable(@NonNull C c);
    }
    public interface IVideoCapturerListener<P> {
        boolean onCapturerAvailable(P peripheral, PeerConnectionParameters.CapturerGenerator capturer_generator);
    }

    public interface ICapturerProvider<S> {
        void setCapturerListener(
                int video_width,
                int video_height,
                int video_fps,
                IVideoCapturerListener<S> listener);
    }

    interface OnNewPhotoListener {
        void onChange(List<FileEntry> photos);
    }

    interface OnNewVideosListener {
        void onChange(List<FileEntry> videos);
    }

    interface IMediaStoreProvider {
        String AlbumName = "DROHUB";
        String ThumbnailFormat = "JPEG";
        interface InputStreamListener {
            void onAvailable(InputStream stream) throws IllegalAccessException;
        }

        void setNewPhotosListener(OnNewPhotoListener l);
        void setNewVideosListener(OnNewVideosListener l);
        String getMimeType(FileEntry file_entry);
        String getExtension(FileEntry file_entry);
        List<FileEntry> getPhotos();
        List<FileEntry> getVideos();
        void getThumbnail(FileEntry file, InputStreamListener listener) throws IllegalAccessException;
        void getMedia(FileEntry file, InputStreamListener listener) throws IllegalAccessException;
    }
}
