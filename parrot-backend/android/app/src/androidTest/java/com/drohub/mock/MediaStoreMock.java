package com.drohub.mock;

import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.Models.FileEntry;

import java.io.IOException;
import java.io.InputStream;
import java.util.ArrayList;
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
        _file_entry = new FileEntry(this, file_name, file_name, type, time_millis_utc, serial, new ArrayList<>());
    }

    @Override
    public void setNewMediaListener(IPeripheral.OnNewMediaListener listener) {
        throw new RuntimeException("Ups");
    }

    @Override
    public List<FileEntry> getAllMedia() {
        if (_file_entry.resource_type == FileEntry.FileResourceType.VIDEO)
            return Collections.singletonList(_file_entry);
        else
            return Collections.emptyList();
    }

    @Override
    public void getThumbnail(FileEntry file, InputStreamListener l) {
        try {
            _input_file.reset();
            l.onAvailable(file, _input_file);
        }
        catch (IOException | IllegalAccessException e) {
            l.onError(file, e);
        }
    }

    @Override
    public void getMedia(FileEntry file, InputStreamListener l) throws IllegalAccessException {
        try {
            _input_file.reset();
            l.onAvailable(file, _input_file);
        }
        catch (IOException e) {
            throw new IllegalAccessException(e.getMessage());
        }
    }

    public FileEntry getFileEntry() {
        return _file_entry;
    }
}