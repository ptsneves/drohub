package com.drohub.api;

import com.android.volley.VolleyError;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.IInfoDisplay;
import com.drohub.Models.FileEntry;
import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;

public class GetSubscriptionMediaInfoHelper extends APIHelper {
    public interface Listener {
        void onError(String error_message);
    }

    public static class DroHubGalleryMediaStore implements IPeripheral.IMediaStoreProvider {
        private List<FileEntry> _media = new ArrayList<>();
        private IPeripheral.OnNewMediaListener _media_listener;

        private void setNewMedia(List<FileEntry> new_media) {
            _media = new_media;
            _media_listener.onChange(_media);
        }

        @Override
        public void setNewMediaListener(IPeripheral.OnNewMediaListener listener) {
            _media_listener = listener;
        }

        @Override
        public List<FileEntry> getAllMedia() {
            return _media;
        }

        @Override
        public void getThumbnail(FileEntry file, InputStreamListener listener) throws IllegalAccessException {
            throw new IllegalAccessException("Not implemented");
        }

        @Override
        public void getMedia(FileEntry file, InputStreamListener listener) throws IllegalAccessException {
            throw new IllegalAccessException("Not implemented");
        }
    }

    final private String _url;
    final private Listener _listener;
    final private IPeripheral.IMediaStoreProvider.ProviderListener _media_store_listener;

    public GetSubscriptionMediaInfoHelper(
            IInfoDisplay display,
            Listener listener,
            String user_email,
            String user_auth_token,
            String url,
            IPeripheral.IMediaStoreProvider.ProviderListener media_store_listener) {

        super(display, user_email, user_auth_token);
        _url = url;
        _listener = listener;
        _media_store_listener = media_store_listener;
    }

    public void get() {
        super.get(_url, this::onRemoteData, this::onError, null);
    }

    private void onError(VolleyError error) {
        _listener.onError("Error fetching DroHub media info");
    }

    private void onRemoteData(JSONObject response) {
        ArrayList<FileEntry> media = new ArrayList<>();
        DroHubGalleryMediaStore media_store = new DroHubGalleryMediaStore();
        _media_store_listener.onNewMediaStore(media_store);

        JSONObject result = response.optJSONObject("result");
        if (result == null) {
            _listener.onError("Unexpected gallery data");
            return;
        }

        Iterator<String> timestamp_keys = result.keys();
        while (timestamp_keys.hasNext()) {
            String day_timestamp_key = timestamp_keys.next();
            JSONObject device_connections = result.optJSONObject(day_timestamp_key);
            if (device_connections == null)
                continue;
            Iterator<String> device_connections_keys = device_connections.keys();

            while (device_connections_keys.hasNext()){
                String device_connection_timestamp = device_connections_keys.next();
                JSONObject session = device_connections.optJSONObject(device_connection_timestamp);
                if (session != null) {
                    JSONArray session_medias = session.optJSONArray("SessionMedia");
                    if (session_medias == null)
                        return;

                    String device_serial = session.optString("DeviceSerial");
                    if (device_serial.isEmpty())
                        continue;

                    int size = session_medias.length();
                    for (int i = 0; i < size; i++) {
                        JSONObject media_info = session_medias.optJSONObject(i);
                        if (media_info == null)
                            continue;

                        String media_path = media_info.optString("MediaPath");
                        String preview_media_path = media_info.optString("PreviewMediaPath");

                        JSONArray json_tags = session.optJSONArray("Tags");
                        ArrayList<String> tags = new ArrayList<>();
                        if (json_tags != null) {
                            for (int tag_index = 0; tag_index < json_tags.length(); tag_index++) {
                                tags.add(json_tags.optString(tag_index));
                            }
                        }

                        if (preview_media_path.endsWith(".jpeg")) {
                            media.add(new FileEntry(media_store,
                                    media_path,
                                    preview_media_path,
                                    FileEntry.FileResourceType.IMAGE,
                                    media_info.optLong("CaptureDateTime", 0),
                                    device_serial,
                                    tags
                                    ));
                        }
                        else if (preview_media_path.endsWith(".webm") || preview_media_path.endsWith(".mp4"))
                            media.add(new FileEntry(media_store,
                                    media_path,
                                    preview_media_path,
                                    FileEntry.FileResourceType.VIDEO,
                                    media_info.optLong("CaptureDateTime", 0),
                                    device_serial,
                                    tags));
                    }
                }
            }
        }
        media_store.setNewMedia(media);
    }
}
