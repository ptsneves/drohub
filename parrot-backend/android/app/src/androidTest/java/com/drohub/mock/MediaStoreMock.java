package com.drohub.mock;

import android.graphics.Bitmap;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.Models.FileEntry;
import com.google.android.gms.common.util.IOUtils;
import kotlin.NotImplementedError;

import java.io.IOException;
import java.io.InputStream;
import java.util.Collections;
import java.util.List;

public class MediaStoreMock implements IPeripheral.IMediaStoreProvider {
    InputStream _input_file;
    String _mime_type;
    FileEntry _file_entry;

    public MediaStoreMock(InputStream input_file, String file_name, String mime_type, FileEntry.FileResourceType type,
                          long time_millis_utc, String serial) {
        _input_file = input_file;
        _input_file.mark(0);
        _mime_type = mime_type;
        _file_entry = new FileEntry(this, file_name, type, time_millis_utc, serial);
    }

    @Override
    public void setNewPhotosListener(IPeripheral.OnNewPhotoListener l) {
        throw new NotImplementedError("Ups");
    }

    @Override
    public void setNewVideosListener(IPeripheral.OnNewVideosListener l) {
        throw new NotImplementedError("Ups");
    }

    @Override
    public String getMimeType(FileEntry file_entry) {
        if (file_entry != _file_entry)
            throw new RuntimeException("Passed file entry does not belong to mocked one");
        return _mime_type;
    }

    @Override
    public String getExtension(FileEntry file_entry) {
        return ".mp4";
    }

    @Override
    public List<FileEntry> getPhotos() {
        if (_file_entry.resource_type == FileEntry.FileResourceType.IMAGE)
            return Collections.singletonList(_file_entry);
        else
            return Collections.emptyList();
    }

    @Override
    public List<FileEntry> getVideos() {
        if (_file_entry.resource_type == FileEntry.FileResourceType.VIDEO)
            return Collections.singletonList(_file_entry);
        else
            return Collections.emptyList();
    }

    @Override
    public void getThumbnail(FileEntry file, InputStreamListener l) throws IllegalAccessException {
        try {
            _input_file.reset();
            l.onAvailable(_input_file);
        }
        catch (IOException e) {
            throw new IllegalAccessException(e.getMessage());
        }
    }

    @Override
    public void getMedia(FileEntry file, InputStreamListener l) throws IllegalAccessException {
        try {
            _input_file.reset();
            l.onAvailable(_input_file);
        }
        catch (IOException e) {
            throw new IllegalAccessException(e.getMessage());
        }
    }

    public FileEntry getFileEntry() {
        return _file_entry;
    }
}