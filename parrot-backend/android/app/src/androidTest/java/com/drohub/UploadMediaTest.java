package com.drohub;

import android.content.Context;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;
import com.drohub.Models.FileEntry;
import com.drohub.api.APIHelper;
import com.drohub.api.UploadMediaHelper;
import com.drohub.mock.InfoDisplayMock;
import com.drohub.mock.MediaStoreMock;
import org.junit.Assert;
import org.junit.Test;
import org.junit.runner.RunWith;

import java.io.IOException;
import java.net.URI;
import java.net.URISyntaxException;
import java.util.Stack;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

@RunWith(AndroidJUnit4.class)
public class UploadMediaTest {
    private enum ValidateState {
        UPLOAD_SUCCESSFUL,
        UPLOAD_HALTED,
        ERROR,
    }

    private class ValidationStatus {
        public final ValidateState state;
        public final String error;

        private ValidationStatus(ValidateState state, String error) {
            this.state = state;
            this.error = error;
        }
    }

    final private Context app_context;
    final private Context test_context;
    final private String resource_url;
    final private String serial_number;
    final private String user_name;
    final private String user_auth_token;
    final private long file_time_utc;

    public UploadMediaTest() throws URISyntaxException {
        app_context = InstrumentationRegistry.getInstrumentation().getTargetContext();
        test_context = InstrumentationRegistry.getInstrumentation().getContext();
        resource_url = DroHubHelper.getURL(app_context, R.string.upload_media_url);
        user_name = InstrumentationRegistry.getArguments().getString("UserName");
        user_auth_token = InstrumentationRegistry.getArguments().getString("Token");
        serial_number = InstrumentationRegistry.getArguments().getString("SerialNumber");
        file_time_utc = Long.parseLong(InstrumentationRegistry.getArguments().getString("FileTime"));
        APIHelper.ignoreSSLCerts();

        if (user_name == null
                || resource_url == null
                || user_auth_token == null
                || serial_number == null
                || file_time_utc == -1)
            throw new IllegalStateException("One of the test parameters was not passed");

    }

    @Test
    public void UrlIsCorrectTest() throws URISyntaxException {
        String validate_token_url =
                InstrumentationRegistry.getArguments().getString("UploadMediaURL");

        URI uri_in_app = new URI(resource_url);
        URI uri_from_test = new URI(validate_token_url);
        Assert.assertEquals(uri_from_test.getPath(), uri_in_app.getPath());
    }

    private CompletableFuture<ValidationStatus> validateTest(FileEntry file_entry, boolean is_preview, boolean halt_on_progress) throws IOException, IllegalAccessException {
        final InfoDisplayMock t = new InfoDisplayMock(100);

        CompletableFuture<ValidationStatus> result_future = new CompletableFuture<>();
        Stack<Integer> progress_states = new Stack();
        UploadMediaHelper helper = new UploadMediaHelper(t,
                new UploadMediaHelper.Listener() {
                    @Override
                    public void onSuccess() {
                        result_future.complete(new ValidationStatus(ValidateState.UPLOAD_SUCCESSFUL, ""));
                    }

                    @Override
                    public void onUploadError(String error) {
                        if (halt_on_progress)
                            result_future.complete(new ValidationStatus(ValidateState.UPLOAD_HALTED, ""));
                        else {
                            result_future.complete(new ValidationStatus(ValidateState.ERROR, error));
                        }
                    }

                    @Override
                    public boolean onProgress(int percent) {
                        progress_states.push(percent);
                        return !halt_on_progress;
                    }
                },
                user_name,
                user_auth_token,
                resource_url,
                file_entry,
                is_preview);

        helper.upload();

        return result_future;
    }

    @Test
    public void NormalUseTest() throws InterruptedException, ExecutionException, TimeoutException, IOException, IllegalAccessException {
        MediaStoreMock media_store = new MediaStoreMock(
            test_context.getAssets().open("video.mp4"),
                "video.mp4",
                "video/mp4",
                FileEntry.FileResourceType.VIDEO,
                file_time_utc,
                serial_number);

        CompletableFuture<ValidationStatus> haltable_future =
                validateTest(media_store.getFileEntry(), true, true);

        ValidationStatus r = haltable_future.get(1000000, TimeUnit.MILLISECONDS);
        Assert.assertEquals(ValidateState.UPLOAD_HALTED, r.state);

        CompletableFuture<ValidationStatus> resume_future =
                validateTest(media_store.getFileEntry(), true, false);

        ValidationStatus r1 = resume_future.get(10000000, TimeUnit.MILLISECONDS);
        Assert.assertEquals("", r1.error);
        Assert.assertEquals(ValidateState.UPLOAD_SUCCESSFUL, r1.state);
    }
}