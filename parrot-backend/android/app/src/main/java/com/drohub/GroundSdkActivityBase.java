/*
 *     Copyright (C) 2019 Parrot Drones SAS
 *
 *     Redistribution and use in source and binary forms, with or without
 *     modification, are permitted provided that the following conditions
 *     are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in
 *       the documentation and/or other materials provided with the
 *       distribution.
 *     * Neither the name of the Parrot Company nor the names
 *       of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written
 *       permission.
 *
 *     THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 *     "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 *     LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
 *     FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
 *     PARROT COMPANY BE LIABLE FOR ANY DIRECT, INDIRECT,
 *     INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
 *     BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS
 *     OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED
 *     AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 *     OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT
 *     OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF
 *     SUCH DAMAGE.
 *
 */

package com.drohub;

import android.Manifest;
import android.accounts.AccountManager;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.pm.PackageManager;
import android.os.Bundle;
import android.util.Log;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Toast;

import androidx.annotation.NonNull;
import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import androidx.localbroadcastmanager.content.LocalBroadcastManager;

import com.drohub.thrift.ThriftConnection;
import com.google.android.material.snackbar.Snackbar;
import com.parrot.drone.groundsdk.GroundSdk;
import com.parrot.drone.groundsdk.ManagedGroundSdk;
import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.peripheral.VirtualGamepad;
import com.parrot.drone.groundsdk.device.peripheral.gamepad.ButtonsMappableAction;
import com.parrot.drone.groundsdk.facility.AutoConnection;
import com.parrot.drone.sdkcore.ulog.ULog;
import com.parrot.drone.sdkcore.ulog.ULogTag;

import java.io.IOException;
import java.util.HashSet;
import java.util.Set;

/**
 * Base for an activity that uses GroundSdk. Manages GroundSdk lifecycle properly.
 */
public abstract class GroundSdkActivityBase extends AppCompatActivity {
    @NonNull
    public static String withKey(@NonNull String key) {
        return "com.drohub.EXTRA_" + key;
    }

    public static final String EXTRA_DEVICE_UID = withKey("DEVICE_UID");
    public static final String EXTRA_USER_EMAIL = withKey("USER_EMAIL");
    public static final String EXTRA_USER_PASSWORD = withKey("USER_PASSWORD");
    /** Logging tag. */
    private static final ULogTag TAG = new ULogTag("DROHUB");

    /** List of runtime permission we need. */
    private static final String[] PERMISSIONS_NEEDED = new String[] {
            Manifest.permission.ACCESS_NETWORK_STATE,
            Manifest.permission.WRITE_EXTERNAL_STORAGE, /* for ULog Recorder. */
            Manifest.permission.ACCESS_COARSE_LOCATION, /* to access BLE discovery results. */
            Manifest.permission.ACCESS_FINE_LOCATION,   /* for GPS location updates. */
            Manifest.permission.CAMERA, /* For HMD see-through. */
            Manifest.permission.INTERNET,
    };

    /** Code for permission request result handling. */
    private static final int REQUEST_CODE_PERMISSIONS_REQUEST = 1;

    /** Ground SDK interface. */
    private GroundSdk mGroundSdk;
    protected ThriftConnection _thrift_connection;
    protected String user_email;
    protected String password;

    /**
     * Gets GroundSDK interface.
     *
     * @return GroundSDK interface
     */
    @NonNull
    public final GroundSdk getDroneHandle() {
        return mGroundSdk;
    }
    private Drone _drone;

    public void alertBox(String reason_to_finish) {
        AlertDialog alertDialog = new AlertDialog.Builder(this).create();
        alertDialog.setTitle("Alert");
        alertDialog.setMessage(reason_to_finish);
        alertDialog.setButton(AlertDialog.BUTTON_NEUTRAL, "OK",
                (dialog, which) -> {
                    dialog.dismiss();
                    finish();
                });
        alertDialog.show();
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        user_email = getIntent().getStringExtra(EXTRA_USER_EMAIL); //Can be null
        password = getIntent().getStringExtra(EXTRA_USER_PASSWORD); //Can be null


        mGroundSdk = ManagedGroundSdk.obtainSession(this);
        if (mGroundSdk == null) {
            ULog.w(TAG, "Could not obtain ground sdk session");
            return;
        }

        Set<String> permissionsToRequest = new HashSet<>();
        for (String permission : PERMISSIONS_NEEDED) {
            if (ContextCompat.checkSelfPermission(this, permission) != PackageManager.PERMISSION_GRANTED) {
                if (ActivityCompat.shouldShowRequestPermissionRationale(this, permission)) {
                    ULog.w(TAG, "User has not allowed permission " + permission);
                    Toast.makeText(this, "Please allow permission " + permission, Toast.LENGTH_LONG).show();
                    finish();
                    return;
                } else {
                    permissionsToRequest.add(permission);
                }
            }
        }
        if (!permissionsToRequest.isEmpty()) {
            ActivityCompat.requestPermissions(this, permissionsToRequest.toArray(new String[0]),
                    REQUEST_CODE_PERMISSIONS_REQUEST);
        }
    }

    @Override
    protected void onStart() {
        super.onStart();
        LocalBroadcastManager.getInstance(this).registerReceiver(mGamepadEventReceiver, FILTER_GAMEPAD_EVENT);
        mGroundSdk.getFacility(AutoConnection.class, auto_connection -> {
            if (auto_connection == null) {
                ULog.w(TAG, "Auto connectio is null?");
                return;
            }


            if (auto_connection.getStatus() != AutoConnection.Status.STARTED) {
                auto_connection.start();
                ULog.w(TAG, "Started auto connection");
            }

            Drone temp_drone = auto_connection.getDrone();

            if (temp_drone == null)
                return;

            if (auto_connection.getStatus() == AutoConnection.Status.STARTED &&
                    (_drone == null || temp_drone.getUid() != _drone.getUid())) {
                _drone = temp_drone;
                onDroneConnected(_drone);
            }
        });
    }


        protected abstract void onDroneConnected(Drone drone);
        protected abstract void onDroneDisconnected();

    private static final IntentFilter FILTER_GAMEPAD_EVENT = new IntentFilter(
            VirtualGamepad.ACTION_GAMEPAD_APP_EVENT);

    private final BroadcastReceiver mGamepadEventReceiver = new BroadcastReceiver() {

        @Override
        public void onReceive(Context context, Intent intent) {
            int actionOrdinal = intent.getIntExtra(VirtualGamepad.EXTRA_GAMEPAD_APP_EVENT_ACTION, -1);
            if (actionOrdinal != -1) {
                Snackbar.make(getContentView(), "Gamepad app event [action: "
                                                + ButtonsMappableAction.values()[actionOrdinal] + "]",
                        Snackbar.LENGTH_SHORT).show();
            }
        }

        @NonNull
        private View getContentView() {
            return ((ViewGroup) getWindow().getDecorView().findViewById(android.R.id.content)).getChildAt(0);
        }
    };

    @Override
    protected void onStop() {
        LocalBroadcastManager.getInstance(this).unregisterReceiver(mGamepadEventReceiver);
        super.onStop();
    }

    @Override
    public void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        System.out.println("Activity triggerdd");
        _thrift_connection.handleActivityResult(requestCode, resultCode, data);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions,
                                           @NonNull int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        boolean denied = false;
        if (permissions.length == 0) {
            // canceled, finish
            ULog.w(TAG, "User canceled permission(s) request");
            denied = true;
        } else {
            for (int i = 0; i < permissions.length; i++) {
                if (grantResults[i] == PackageManager.PERMISSION_DENIED) {
                    ULog.w(TAG, "User denied permission: " + permissions[i]);
                    denied = true;
                }
            }
        }

        if (denied) {
            ULog.w(TAG, "Finished due to now permissions received");
            finish();
        }
    }
}
