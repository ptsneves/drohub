package com.drohub;

import com.drohub.api.ValidateTokenHelper;
import com.drohub.mock.InfoDisplayMock;

import android.content.Context;

import androidx.test.platform.app.InstrumentationRegistry;
import androidx.test.ext.junit.runners.AndroidJUnit4;

import org.junit.Assert;
import org.junit.Test;
import org.junit.runner.RunWith;

import java.net.URI;
import java.net.URISyntaxException;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

@RunWith(AndroidJUnit4.class)
public class ValidateTokenTest {
    private enum ValidateState {
        VALID_TOKEN,
        INVALID_VERSION,
        ERROR,
    }

    private Context app_context;

    public ValidateTokenTest() {
        app_context = InstrumentationRegistry.getInstrumentation().getTargetContext();
    }

    @Test
    public void ValidateTokenUrlIsCorrectTest() throws URISyntaxException {
        String validate_token_url =
                InstrumentationRegistry.getArguments().getString("ValidateTokenUrl");

        String url_in_app = DroHubHelper.getURL(app_context, R.string.validate_token_url);
        URI uri_in_app = new URI(url_in_app);
        URI uri_from_test = new URI(validate_token_url);
        Assert.assertEquals(uri_from_test.getPath(), uri_in_app.getPath());
    }

    private static CompletableFuture<ValidateState> validateTest(String url, Double version) throws URISyntaxException {
        final InfoDisplayMock t = new InfoDisplayMock(100);
        final String user_name = InstrumentationRegistry.getArguments().getString("UserName");
        final String user_auth_token = InstrumentationRegistry.getArguments().getString("Token");
        if (user_name == null || user_auth_token == null )
            throw new IllegalStateException("UserName or Token env variables not set, but required.");


        CompletableFuture<ValidateState> result_future = new CompletableFuture<>();
        ValidateTokenHelper helper = new ValidateTokenHelper(t,
                new ValidateTokenHelper.Listener() {
                    @Override
                    public void onValidToken() {
                        result_future.complete(ValidateState.VALID_TOKEN);
                    }

                    @Override
                    public void onInvalidVersion() {
                        result_future.complete(ValidateState.INVALID_VERSION);
                    }

                    @Override
                    public void onValidateTokenError(String error) {
                        result_future.complete(ValidateState.ERROR);
                    }
                },
                url,
                user_name,
                user_auth_token,
                version
        );
        helper.validateToken();
        return result_future;
    }

    @Test
    public void NormalUseTest() throws InterruptedException, ExecutionException, TimeoutException, URISyntaxException {
        CompletableFuture<ValidateState> result_future =
                validateTest(DroHubHelper.getURL(app_context, R.string.validate_token_url),
                Double.parseDouble(app_context.getString(R.string.rpc_api_version))
        );

        ValidateState r = result_future.get(10000, TimeUnit.MILLISECONDS);
        Assert.assertEquals(ValidateState.VALID_TOKEN,r);
    }

    @Test
    public void WrongURLTest() throws InterruptedException, ExecutionException, TimeoutException, URISyntaxException {
        CompletableFuture<ValidateState> result_future =
                validateTest(DroHubHelper.getURL(app_context, R.string.validate_token_url)+ "thrash",
                        Double.parseDouble(app_context.getString(R.string.rpc_api_version))
                );

        ValidateState r = result_future.get(1000, TimeUnit.MILLISECONDS);
        Assert.assertEquals(ValidateState.ERROR, r);
    }

    @Test
    public void InvalidURLTest() throws InterruptedException, ExecutionException, TimeoutException, URISyntaxException {
        CompletableFuture<ValidateState> result_future =
                validateTest("thrash",
                        Double.parseDouble(app_context.getString(R.string.rpc_api_version))
                );

        ValidateState r = result_future.get(1000, TimeUnit.MILLISECONDS);
        Assert.assertEquals(ValidateState.ERROR, r);
    }

    @Test
    public void InvalidVersionTest0() throws InterruptedException, ExecutionException, TimeoutException, URISyntaxException {
        CompletableFuture<ValidateState> result_future =
                validateTest(DroHubHelper.getURL(app_context, R.string.validate_token_url),
                0.0
        );

        ValidateState r = result_future.get(1000, TimeUnit.MILLISECONDS);
        Assert.assertEquals(ValidateState.INVALID_VERSION,r);
    }

    @Test
    public void NegativeVersionTest() throws InterruptedException, ExecutionException, TimeoutException, URISyntaxException {
        CompletableFuture<ValidateState> result_future =
                validateTest(DroHubHelper.getURL(app_context, R.string.validate_token_url),
                        -0.0
                );

        ValidateState r = result_future.get(1000, TimeUnit.MILLISECONDS);
        Assert.assertEquals(ValidateState.INVALID_VERSION,r);
    }
}
