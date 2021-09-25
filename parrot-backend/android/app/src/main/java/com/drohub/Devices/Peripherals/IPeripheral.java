package com.drohub.Devices.Peripherals;

import android.graphics.Bitmap;
import androidx.annotation.NonNull;
import com.drohub.Janus.PeerConnectionParameters;
import com.drohub.Models.FileEntry;
import com.drohub.utils.MarkableFileInputStream;

import java.io.*;
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

    interface OnNewMediaListener {
        void onChange(List<FileEntry> media);
    }

    interface IMediaStoreProvider {
        interface ProviderListener {
            void onNewMediaStore(IPeripheral.IMediaStoreProvider media_store);
        }
        String AlbumName = "DROHUB";
        String ThumbnailFormat = "JPEG";
        interface InputStreamListener {
            void onAvailable(FileEntry f, InputStream stream) throws IllegalAccessException;
            void onError(FileEntry f, Exception e);
            void onProgress(FileEntry f, int progress_percent);
        }

        void setNewMediaListener(OnNewMediaListener listener);

        static InputStream convertToInputStream(Bitmap bitmap,
                                                    Bitmap.CompressFormat compress_format,
                                                    int _thumbnail_quality) throws IOException {

            File thumbnail_temp_file = File.createTempFile(AlbumName, AlbumName);
            OutputStream tempo_file_stream = new FileOutputStream(thumbnail_temp_file);
            bitmap.compress(compress_format, _thumbnail_quality, tempo_file_stream);
            tempo_file_stream.flush();
            tempo_file_stream.close();
            thumbnail_temp_file.deleteOnExit();
            return new MarkableFileInputStream(new FileInputStream(thumbnail_temp_file));
        }

        static String getMimeType(FileEntry file_entry) {
            if (file_entry.resource_type == FileEntry.FileResourceType.IMAGE)
                return "image/jpeg";
            else if (file_entry.resource_type == FileEntry.FileResourceType.VIDEO)
                return "video/mp4";
            return  "application/octet-stream";
        }

        static String getExtension(FileEntry file_entry) {
            if (file_entry.resource_type == FileEntry.FileResourceType.IMAGE)
                return ".jpeg";
            else if (file_entry.resource_type == FileEntry.FileResourceType.VIDEO)
                return ".mp4";
            return  "bin";
        }

        List<FileEntry> getAllMedia();
        void getThumbnail(FileEntry file, InputStreamListener listener);
        void getMedia(FileEntry file, InputStreamListener listener) throws IllegalAccessException;
    }
}
