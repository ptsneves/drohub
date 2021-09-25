package com.drohub.api;

import com.android.volley.VolleyError;
import com.drohub.Devices.Peripherals.DroHubWeb.DroHubGalleryMediaStore;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.Models.FileEntry;
import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Iterator;

public class GetSubscriptionMediaInfoHelper extends APIHelper {

    public interface Listener {
        void onError(String error_message);
    }

    final private String _get_subscription_info_url;
    final private String _get_photo_url;
    final private String _get_video_url;
    final private Listener _listener;
    final private IPeripheral.IMediaStoreProvider.ProviderListener _media_store_listener;
    final DroHubGalleryMediaStore _drohub_media_store;
    public GetSubscriptionMediaInfoHelper(
            Listener listener,
            String user_email,
            String user_auth_token,
            String get_subscription_info_url,
            String get_photo_url,
            String get_video_url,
            IPeripheral.IMediaStoreProvider.ProviderListener drohub_media_store_listener) {

        super(user_email, user_auth_token);
        _get_subscription_info_url = get_subscription_info_url;
        _get_photo_url = get_photo_url;
        _get_video_url = get_video_url;
        _listener = listener;
        _media_store_listener = drohub_media_store_listener;

        _drohub_media_store = new DroHubGalleryMediaStore(_get_photo_url, _get_video_url, user_email, user_auth_token);
        _media_store_listener.onNewMediaStore(_drohub_media_store);
    }

    public void get() {
        super.get(_get_subscription_info_url, this::onRemoteData, this::onError, null);
    }

    private void onError(VolleyError error) {
        _listener.onError("Error fetching DroHub media info");
    }

    private void onRemoteData(JSONObject response) {
        ArrayList<FileEntry> media = new ArrayList<>();

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

                        JSONArray json_tags = session.optJSONArray("Tags");
                        ArrayList<String> tags = new ArrayList<>();
                        if (json_tags != null) {
                            for (int tag_index = 0; tag_index < json_tags.length(); tag_index++) {
                                tags.add(json_tags.optString(tag_index));
                            }
                        }

                        if (media_path.endsWith(".jpeg")) {
                            media.add(new FileEntry(_drohub_media_store,
                                    media_path,
                                    media_path,
                                    FileEntry.FileResourceType.IMAGE,
                                    media_info.optLong("CaptureDateTime", 0),
                                    device_serial,
                                    tags
                                    ));
                        }
                        else if (media_path.endsWith(".webm") || media_path.endsWith(".mp4"))
                            media.add(new FileEntry(_drohub_media_store,
                                    media_path,
                                    media_path,
                                    FileEntry.FileResourceType.VIDEO,
                                    media_info.optLong("CaptureDateTime", 0),
                                    device_serial,
                                    tags));
                    }
                }
            }
        }
        _drohub_media_store.setNewMedia(media);
    }
}
