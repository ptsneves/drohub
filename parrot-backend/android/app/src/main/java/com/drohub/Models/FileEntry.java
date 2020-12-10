package com.drohub.Models;


import com.drohub.Devices.Peripherals.IPeripheral;

public class FileEntry {
    public enum FileResourceType {
        IMAGE,
        VIDEO,
        OTHER
    };

    public final String resource_id;
    public final FileResourceType resource_type;
    public final long creation_time_unix_ms;
    public final String serial_number;
    public final IPeripheral.IMediaStoreProvider media_provider;

    public FileEntry(
            IPeripheral.IMediaStoreProvider media_provider,
            String resource_id,
            FileResourceType resource_type,
            long creation_time_unix_ms,
            String serial_number) {

        this.media_provider = media_provider;
        this.resource_id = resource_id;
        this.resource_type = resource_type;
        this.creation_time_unix_ms = creation_time_unix_ms;
        this.serial_number = serial_number;
    }
}