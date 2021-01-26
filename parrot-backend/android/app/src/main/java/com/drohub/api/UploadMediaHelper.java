package com.drohub.api;

import androidx.annotation.NonNull;
import com.android.volley.NetworkResponse;
import com.android.volley.Request;
import com.android.volley.toolbox.HttpHeaderParser;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.IInfoDisplay;
import com.drohub.Models.FileEntry;
import org.json.JSONException;
import org.json.JSONObject;

import java.io.IOException;
import java.io.InputStream;
import java.io.UnsupportedEncodingException;

public class UploadMediaHelper {
    public interface Listener {
        void onSuccess();
        void onUploadError(String error);
        boolean onProgress(int percent);
    }

    private final String _RESULT_KEY = "result";
    private final String _SUCCESS_RESULT_VALUE = "ok";
    private final String _ERROR_RESULT_VALUE = "nok";
    private final String _SEND_NEXT_RESULT_VALUE = "send-next";
    private final String _ERROR_KEY = "error";
    private final String _CHUNK_BEGIN_KEY = "begin";
    private final int _MAXIMUM_CHUNK_SIZE_BYTES = 4000000;

    private final APIHelper _api_helper;
    private final boolean _is_preview;
    private FileEntry _file_entry;
    private final Listener _listener;
    private final String _upload_media_url;

    private int _sent_bytes;
    private int _file_size;
    private InputStream _file_stream;

    public UploadMediaHelper(IInfoDisplay display,
                             @NonNull Listener listener,
                             String user_email,
                             String user_auth_token,
                             String upload_media_url,
                             boolean is_preview) {
        _api_helper = new APIHelper(display, user_email, user_auth_token);
        _listener = listener;
        _upload_media_url = upload_media_url;
        _is_preview = is_preview;
        _sent_bytes = 0;
    }

    public void upload(@NonNull FileEntry file_entry) throws IllegalAccessException {
        if (_file_stream != null)
            throw new IllegalAccessError("Upload session underway or finished");

        _file_entry = file_entry;
        if (_is_preview)
            _file_entry.media_provider.getThumbnail(_file_entry, this::uploadPriv);
        else
            _file_entry.media_provider.getMedia(_file_entry, this::uploadPriv);


    }

    private void uploadPriv(InputStream stream) throws IllegalAccessException {
        _file_stream = stream;
        try {
            _file_size = _file_stream.available();
            uploadChunk();
        }
        catch (IOException e) {
            throw new IllegalAccessException(e.getMessage());
        }
    }

    private void uploadChunk() throws IllegalAccessException {
        try {
            int next_chunk_size = Math.min(_MAXIMUM_CHUNK_SIZE_BYTES, _file_size - _sent_bytes);

            if (next_chunk_size == 0)
                throw new IllegalAccessException("No chunk left to upload?");

            byte[] slice = new byte[next_chunk_size];
            _file_stream.read(slice);

            MultiPartRequest multipart_req = new MultiPartRequest(Request.Method.POST,
                    _upload_media_url,
                    APIHelper.getHeaders(_api_helper._user_email,_api_helper._user_auth_token),
                    this::processResponse, error -> {
                if (error == null) {
                    _listener.onUploadError("Unspecified error trying to upload");
                    return;
                }
                if (error.networkResponse != null) {
                    _listener.onUploadError(String.format("Upload failed with error %d",
                            error.networkResponse.statusCode));
                    return;
                }
                if (error.getMessage() != null) {
                    _listener.onUploadError(error.getMessage());
                    return;
                }

                _listener.onUploadError("Unspecified error trying to upload");

            });

            multipart_req.addPart(new MultiPartRequest.FormPart("IsPreview", _is_preview ? "true" : "false"));
            multipart_req.addPart(new MultiPartRequest.FormPart("DeviceSerialNumber", _file_entry.serial_number));
            multipart_req.addPart(new MultiPartRequest.FormPart("UnixCreationTimeMS",
                    Long.toString(_file_entry.creation_time_unix_ms)));

            multipart_req.addPart(new MultiPartRequest.FormPart("AssembledFileSize", Long.toString(_file_size)));
            multipart_req.addPart(new MultiPartRequest.FormPart("RangeStartBytes", Long.toString(_sent_bytes)));

            multipart_req.addPart(new MultiPartRequest.FilePart(
                    "File",
                    IPeripheral.IMediaStoreProvider.getMimeType(_file_entry),
                    _file_entry.resource_id + IPeripheral.IMediaStoreProvider.getExtension(_file_entry),
                    slice
            ));
            _sent_bytes += next_chunk_size;
            multipart_req.setShouldCache(false);
            _api_helper._volley.getRequestQueue().add(multipart_req);
        }
        catch (IOException e) {
            throw new IllegalAccessException(e.getMessage());
        }
    }

    private void processResponse(NetworkResponse network_response) {
        try {
            String json_string = new String(network_response.data, HttpHeaderParser.parseCharset(network_response.headers));
            JSONObject response = new JSONObject(json_string);
            if (response.has(_RESULT_KEY)) {
                if (response.optString(_RESULT_KEY).equals(_SEND_NEXT_RESULT_VALUE)) {
                    _sent_bytes = Integer.parseInt(response.optString(_CHUNK_BEGIN_KEY));
                    boolean should_continue = _listener.onProgress(_sent_bytes / _file_size);
                    if (should_continue)
                        uploadChunk();
                    else
                        _listener.onUploadError("Aborted on the request of the user");
                }
                else if (response.optString(_RESULT_KEY).equals(_SUCCESS_RESULT_VALUE))
                    _listener.onSuccess();
            }
            else if (response.has(_ERROR_KEY)) {
                String error = response.optString(_ERROR_KEY);
                _listener.onUploadError(error);
            }
            else {
                _listener.onUploadError("Error but no description");
            }
        }
        catch (JSONException | UnsupportedEncodingException | IllegalAccessException e) {
            _listener.onUploadError("Unexpected answer uploading file");
        }
    }
}