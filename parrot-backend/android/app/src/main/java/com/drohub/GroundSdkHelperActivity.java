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
import com.parrot.drone.groundsdk.device.RemoteControl;
import com.parrot.drone.groundsdk.device.peripheral.VirtualGamepad;
import com.parrot.drone.groundsdk.device.peripheral.gamepad.ButtonsMappableAction;
import com.parrot.drone.groundsdk.facility.AutoConnection;
import com.parrot.drone.sdkcore.ulog.ULog;

import java.util.HashSet;
import java.util.Set;

import static com.drohub.DroHubHelper.EXTRA_USER_AUTH_TOKEN;

/**
 * Base for an activity that uses GroundSdk. Manages GroundSdk lifecycle properly.
 */
public abstract class GroundSdkHelperActivity extends AppCompatActivity {
    /** List of runtime permission we need. */
    private static final String[] PERMISSIONS_NEEDED = new String[] {
            Manifest.permission.ACCESS_NETWORK_STATE,
            Manifest.permission.WRITE_EXTERNAL_STORAGE, /* for ULog Recorder. */
            Manifest.permission.CAMERA, /* For HMD see-through. */
            Manifest.permission.INTERNET,
            Manifest.permission.ACCESS_FINE_LOCATION,
            Manifest.permission.RECORD_AUDIO,
    };

    /** Code for permission request result handling. */
    private static final int REQUEST_CODE_PERMISSIONS_REQUEST = 1;

    private static final String TAG = "GroundSDKHelperActivity";
    /** Ground SDK interface. */
    private GroundSdk mGroundSdk;
    protected ThriftConnection _thrift_connection;
    protected String _user_auth_token;
    protected String _user_email;

    /**
     * Gets GroundSDK interface.
     *
     * @return GroundSDK interface
     */
    @NonNull
    public final GroundSdk getParrotSDKHandle() {
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

        _user_auth_token = getIntent().getStringExtra(EXTRA_USER_AUTH_TOKEN); //Can be null

        mGroundSdk = ManagedGroundSdk.obtainSession(this);
        if (mGroundSdk == null) {
            throw new NullPointerException("Could not obtain ground sdk session");
        }

        Set<String> permissionsToRequest = new HashSet<>();
        for (String permission : PERMISSIONS_NEEDED) {
            if (ContextCompat.checkSelfPermission(this, permission) != PackageManager.PERMISSION_GRANTED) {
                if (ActivityCompat.shouldShowRequestPermissionRationale(this, permission)) {
                    Log.w(TAG, "User has not allowed permission " + permission);
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
        mGroundSdk.getFacility(AutoConnection.class, auto_connection -> {
            if (auto_connection == null) {
                Log.w(TAG, "Auto connectio is null?");
                return;
            }

            if (auto_connection.getStatus() != AutoConnection.Status.STARTED) {
                auto_connection.start();
                Log.w(TAG, "Started auto connection");
            }

            Drone temp_drone = auto_connection.getDrone();

            if (temp_drone == null)
                return;

            if (auto_connection.getStatus() == AutoConnection.Status.STARTED &&
                    (_drone == null || temp_drone.getUid() != _drone.getUid())) {
                _drone = temp_drone;
                onDroneConnected(_drone, auto_connection.getRemoteControl());
            }
        });
    }


    protected abstract void onDroneConnected(Drone drone, RemoteControl rc);
    protected abstract void onDroneDisconnected();


    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions,
                                           @NonNull int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        boolean denied = false;
        if (permissions.length == 0) {
            // canceled, finish
            Log.w(TAG, "User canceled permission(s) request");
            denied = true;
        } else {
            for (int i = 0; i < permissions.length; i++) {
                if (grantResults[i] == PackageManager.PERMISSION_DENIED) {
                    Log.w(TAG, "User denied permission: " + permissions[i]);
                    denied = true;
                }
            }
        }

        if (denied) {
            Log.w(TAG, "Finished due to new permissions received");
            finish();
        }
    }
}
