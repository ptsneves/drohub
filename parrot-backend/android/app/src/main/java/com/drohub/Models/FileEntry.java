package com.drohub.Models;


import androidx.annotation.Nullable;
import com.drohub.Devices.Peripherals.IPeripheral;

import java.util.List;
import java.util.stream.Stream;

public class FileEntry {
    public enum FileResourceType {
        IMAGE,
        VIDEO,
        OTHER
    }

    public final String resource_id;
    public final String preview_resource_id;
    public final FileResourceType resource_type;
    public final long creation_time_unix_ms;
    public final String serial_number;
    public final IPeripheral.IMediaStoreProvider media_provider;
    public final List<String> tags;

    public FileEntry(
            IPeripheral.IMediaStoreProvider media_provider,
            String resource_id,
            String preview_resource_id,
            FileResourceType resource_type,
            long creation_time_unix_ms,
            String serial_number,
            List<String> tags) {

        this.media_provider = media_provider;
        this.resource_id = resource_id;
        this.preview_resource_id = preview_resource_id;
        this.resource_type = resource_type;
        this.creation_time_unix_ms = creation_time_unix_ms;
        this.serial_number = serial_number;
        this.tags = tags;
    }

    @Override
    public boolean equals(Object obj) {
        if (!(obj instanceof  FileEntry))
            return false;
        FileEntry file_entry_obj = (FileEntry) obj;
        return file_entry_obj.creation_time_unix_ms == creation_time_unix_ms
                && ! file_entry_obj.resource_id.isEmpty()
                && ! resource_id.isEmpty();
    }

    private static Stream<FileEntry> intersectFileEntries(Stream<FileEntry> setA, Stream<FileEntry> setB) {
        return setB.filter(fileB ->
            setA.anyMatch(fileA -> fileA == fileB && !fileA.resource_id.isEmpty())
        );
    }
}