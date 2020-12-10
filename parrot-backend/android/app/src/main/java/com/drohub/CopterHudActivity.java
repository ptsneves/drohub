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

import android.content.Context;
import android.content.res.ColorStateList;
import android.graphics.Bitmap;
import android.location.Location;
import android.media.AudioManager;
import android.os.Bundle;
import android.os.Handler;
import android.util.Log;
import android.view.View;
import android.view.animation.AlphaAnimation;
import android.view.animation.Animation;
import android.view.animation.LinearInterpolator;
import android.widget.Button;
import android.widget.ImageView;
import android.widget.TextView;
import androidx.annotation.NonNull;
import com.drohub.Devices.Peripherals.IPeripheral;
import com.drohub.Devices.Peripherals.Parrot.ParrotGimbal;
import com.drohub.Janus.PeerConnectionParameters;
import com.drohub.Devices.Peripherals.Parrot.ParrotMainCamera;
import com.drohub.Devices.Peripherals.Parrot.ParrotMediaStore;
import com.drohub.Devices.Peripherals.Parrot.ParrotStreamServer;
import com.drohub.Models.FileEntry;
import com.drohub.Views.DroHubMapView;
import com.drohub.Views.ErrorTextView;
import com.drohub.Views.TimerView;
import com.drohub.Views.ToggleButtonExView;
import com.drohub.api.UploadMediaHelper;
import com.drohub.thrift.DroHubHandler;
import com.drohub.thrift.ThriftConnection;
import com.parrot.drone.groundsdk.device.Drone;
import com.parrot.drone.groundsdk.device.RemoteControl;
import com.parrot.drone.groundsdk.device.instrument.*;
import com.parrot.drone.groundsdk.device.pilotingitf.Activable;
import com.parrot.drone.groundsdk.device.pilotingitf.ManualCopterPilotingItf;
import com.parrot.drone.groundsdk.device.pilotingitf.ManualCopterPilotingItf.SmartTakeOffLandAction;
import com.parrot.drone.groundsdk.device.pilotingitf.ReturnHomePilotingItf;
import com.parrot.drone.groundsdk.facility.AutoConnection;
import com.parrot.drone.groundsdk.facility.UserLocation;
import org.jetbrains.annotations.NotNull;
import org.webrtc.SurfaceViewRenderer;

import java.io.IOException;
import java.net.URISyntaxException;

import static com.drohub.DroHubHelper.*;
import static java.lang.Double.NaN;


/** Activity to pilot a copter. */
public class CopterHudActivity extends GroundSdkHelperActivity {

    private static final String TAG = "CopterHudActivity";
    private static final String MAP_VIEW_BUNDLE_KEY = "MapViewBundleKey";

    private Button _takeoff_land_btn;

    private RemoteControl _remote_control;

    private Button _return_to_home_btn;

    DroHubMapView map_view;

    private Drone _drone;

    private ManualCopterPilotingItf mPilotingItf;

    private ReturnHomePilotingItf mReturnHomeItf;

    public SurfaceViewRenderer mStreamView;

    private  DroHubHandler _drohub_handler;
    private AudioManager _audio_manager;

    private ParrotMainCamera _main_camera;
    private ParrotMediaStore _media_store;
    private ParrotGimbal _gimbal;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_copter_hud);

        map_view = findViewById(R.id.drohub_map);
        map_view.onCreate(null);
        map_view.getMapAsync(null);
        
        String drone_uid = getIntent().getStringExtra(DRONE_UID);
        String rc_uid = getIntent().getStringExtra(RC_UID);
        String user_email = getIntent().getStringExtra(EXTRA_USER_EMAIL);
        String auth_token = getIntent().getStringExtra(EXTRA_USER_AUTH_TOKEN);
        if (drone_uid == null || rc_uid == null || user_email == null || auth_token == null) {
            Log.e(TAG, "Device uid, user or auth_token were not passed in intent");
            finish();
            return;
        }

        _drone = getParrotSDKHandle().getDrone(drone_uid);
        if (_drone == null) {
            finish();
            return;
        }

        _remote_control = getParrotSDKHandle().getRemoteControl(rc_uid);
        if (_remote_control == null) {
            finish();
            return;
        }


        mStreamView = findViewById(R.id.video_view); // do not init it here, let whoever draws on it do that!
        _takeoff_land_btn = findViewById(R.id.take_off_land_btn);
        _return_to_home_btn = findViewById(R.id.return_home_btn);

        _drone.getPilotingItf(ManualCopterPilotingItf.class, pilotingItf -> {
            if (pilotingItf == null) {
                finish();
                return;
            }

            mPilotingItf = pilotingItf;
            SmartTakeOffLandAction btnAction = mPilotingItf.getSmartTakeOffLandAction();
            int resId;
            switch (btnAction) {
                case TAKE_OFF:
                    resId = R.drawable.ic_flight_takeoff;
                    break;
                case THROWN_TAKE_OFF:
                    resId = R.drawable.ic_flight_thrown_takeoff;
                    break;
                case LAND:
                case NONE:
                default:
                    resId = R.drawable.ic_flight_land;
                    break;
            }
//            mTakeOffLandBtn.setImageResource(resId);
            _takeoff_land_btn.setEnabled(btnAction != SmartTakeOffLandAction.NONE);
        });

        _drone.getPilotingItf(ReturnHomePilotingItf.class, pilotingItf -> {
            mReturnHomeItf = pilotingItf;
            Activable.State state =
                    mReturnHomeItf == null ? Activable.State.UNAVAILABLE : mReturnHomeItf.getState();
            _return_to_home_btn.setEnabled(state != Activable.State.UNAVAILABLE);
            _return_to_home_btn.setActivated(state == Activable.State.ACTIVE);
        });


        _drone.getInstrument(FlyingIndicators.class, flying_mode -> {
            final TextView text_view = findViewById(R.id.info_flight_mode_text);
            if (flying_mode != null) {
                switch (flying_mode.getState()) {
                    case EMERGENCY:
                    case EMERGENCY_LANDING:
                        text_view.setText("Emergency Landing");
                        break;
                    case LANDED:
                        switch (flying_mode.getLandedState()) {
                            case INITIALIZING:
                            case MOTOR_RAMPING:
                                text_view.setText("Motor Ramping Up");
                                break;
                            case IDLE:
                                text_view.setText("Idle");
                                break;
                            case WAITING_USER_ACTION:
                                text_view.setText("Waiting User Action");
                                break;
                            case NONE:
                            default:
                                text_view.setText("Unknown Flight Mode");
                                break;
                        }
                        break;
                    case FLYING:
                        switch (flying_mode.getFlyingState()) {
                            case TAKING_OFF:
                                text_view.setText("Flight Mode");
                                break;
                            case LANDING:
                                text_view.setText("Landing");
                                break;
                            case WAITING:
                                text_view.setText("Waiting");
                                break;
                            case FLYING:
                                text_view.setText("Flying");
                                break;
                            case NONE:
                            default:
                                text_view.setText("Unknown Flight Mode");
                                break;
                        }
                        break;
                    default:
                        text_view.setText("Unknown Flight Mode");
                        break;
                }
            }
        });


        _drone.getInstrument(Gps.class, gps -> {
            if (gps == null)
                return;

            final TextView satellite_count_text_view = findViewById(R.id.satellite_count_text);
            final ImageView satellite_icon = findViewById(R.id.info_satellite);

            satellite_count_text_view.setText(String.format("%d", gps.getSatelliteCount()));
            if (gps.getSatelliteCount() != 0)
                satellite_icon.setImageTintList(ColorStateList.valueOf(getColor(R.color.white)));
            map_view.setDroneLocation(gps.lastKnownLocation());
            final TextView height_text = findViewById(R.id.height_text);

            Location last_known_location = gps.lastKnownLocation();
            if (last_known_location != null)
                height_text.setText(String.format("%.1fm", last_known_location.getAltitude()));
        });


        getParrotSDKHandle().getFacility(AutoConnection.class, remote_control -> {
            if (remote_control == null || remote_control.getRemoteControl() == null)
                return;
           remote_control.getRemoteControl().getState();
        });

        getParrotSDKHandle().getFacility(UserLocation.class, user_location -> {
            if (user_location == null)
                return;
            if (user_location.lastKnownLocation() != null)
                map_view.setUserLocation(user_location.lastKnownLocation());
        });


        _remote_control.getInstrument(Compass.class, compass -> {
            if (compass == null)
                return;

            map_view.setUserHeading((float)compass.getHeading());
        });

        _drone.getInstrument(Radio.class, radio -> {
            if (radio == null)
                return;
            final ImageView remote_signal_icon = findViewById(R.id.remote_signal_icon);
            switch(radio.getLinkSignalQuality()){
                case -1:
                case 0:
                case 1:
                case 2:
                    remote_signal_icon.setImageDrawable(getDrawable(R.drawable.ic_control_signal_bad));
                    break;
                case 3:
                    remote_signal_icon.setImageDrawable(getDrawable(R.drawable.ic_control_signal_medium));
                    break;
                case 4:
                    remote_signal_icon.setImageDrawable(getDrawable(R.drawable.ic_control_signal_good));
                    break;
                default:
                    return;
            }
            if (radio.isLinkPerturbed())
                remote_signal_icon.setImageTintList(ColorStateList.valueOf(getColor(R.color.danger)));

        });

        _remote_control.getInstrument(BatteryInfo.class, battery_info -> {
           if (battery_info == null)
               return;

           final ImageView rc_battery_level_icon = findViewById(R.id.remote_battery_icon);
           int bat_level = battery_info.getBatteryLevel();

           if (bat_level > 66)
               rc_battery_level_icon.setImageDrawable(getDrawable(R.drawable.ic_video_info_control_battery_full));
           else if (bat_level > 33)
               rc_battery_level_icon.setImageDrawable(getDrawable(R.drawable.ic_video_info_control_battery_halffull));
           else
               rc_battery_level_icon.setImageDrawable(getDrawable(R.drawable.ic_video_info_control_battery_empty));
        });

        _drone.getInstrument(BatteryInfo.class, battery_info -> {
            if (battery_info == null)
                return;

            final ImageView battery_level_icon =findViewById(R.id.drone_battery_icon);
            final TextView battery_level_text = findViewById(R.id.drone_battery_level_text);

            int bat_level = battery_info.getBatteryLevel();
            battery_level_text.setText(getString(R.string.battery_level_format, bat_level));

            if (bat_level > 75)
                battery_level_icon.setImageDrawable(getDrawable(R.drawable.ic_battery_full));
            else if (bat_level > 50)
                battery_level_icon.setImageDrawable(getDrawable(R.drawable.ic_battery_half_full));
            else if (bat_level > 25)
                battery_level_icon.setImageDrawable(getDrawable(R.drawable.ic_battery_half_empty));
            else if (battery_info.getBatteryLevel() > 0)
                battery_level_icon.setImageDrawable(getDrawable(R.drawable.ic_info_battery_empty));
        });

        _takeoff_land_btn.setOnClickListener(v -> mPilotingItf.smartTakeOffLand());

        _return_to_home_btn.setOnClickListener(v -> {
            if (mReturnHomeItf != null) {
                Activable.State state = mReturnHomeItf.getState();
                if (state == Activable.State.ACTIVE) {
                    mReturnHomeItf.deactivate();
                } else if (state == Activable.State.IDLE) {
                    mReturnHomeItf.activate();
                }
            }
        });

        setupMainCameraComponents();
        setupMediaStoreComponents(user_email, auth_token);
        setupLiveVideo(user_email, auth_token);
    }


    @Override
    protected void onStart() {
        super.onStart();
        map_view.onStart();
    }

    @Override
    protected void onResume() {
        super.onResume();
        map_view.onResume();
    }

    @Override
    protected void onStop() {
        super.onStop();
        map_view.onStop();
    }
    @Override
    protected void onPause() {
        map_view.onPause();
        super.onPause();
    }

    @Override
    public void onLowMemory() {
        super.onLowMemory();
        map_view.onLowMemory();
    }

    @Override
    public void onBackPressed() {
        finishAndRemoveTask();
        System.exit(0);
    }

    @Override
    public void onSaveInstanceState(Bundle outState) {
        super.onSaveInstanceState(outState);

        Bundle mapViewBundle = outState.getBundle(MAP_VIEW_BUNDLE_KEY);
        if (mapViewBundle == null) {
            mapViewBundle = new Bundle();
            outState.putBundle(MAP_VIEW_BUNDLE_KEY, mapViewBundle);
        }

        map_view.onSaveInstanceState(mapViewBundle);
    }

    @Override
    protected void onDroneConnected(Drone drone, RemoteControl rc) {
        Log.w("COPTER", "Connected Drone UID " + _drone.getUid());
    }

    @Override
    protected void onDroneDisconnected() {
        Log.w("COPTER", "Connected Drone UID " + _drone.getUid());
    }

    @Override
    protected void onDestroy() {
        map_view.onStop();
        if (_drohub_handler != null)
            _drohub_handler.onStop();
        if (_thrift_connection != null) {
            _thrift_connection.onStop();
            _thrift_connection = null;
        }
        Log.w(TAG, "Stopping activity");
        super.onDestroy();
    }

    public void setupMainCameraComponents() {
        final ErrorTextView e_v = findViewById(R.id.info_warnings_errors);
        final String StartupErrorMessage = "Main camera not yet initialized";
        e_v.getInfoDisplay().add(StartupErrorMessage);

        _main_camera = new ParrotMainCamera(_drone);

        _main_camera.setPeripheralListener(new IPeripheral.IPeripheralListener<ParrotMainCamera>() {
            @Override
            public void onChange(@NonNull ParrotMainCamera o) {}

            @Override
            public boolean onFirstTimeAvailable(@NonNull ParrotMainCamera o) {
                e_v.getInfoDisplay().remove(StartupErrorMessage);
                setupRecordingButton();
                setupTriggerPictureButton(e_v);
                setupMultiTouchGestures();
                return true;
            }
        });
        _main_camera.setZoomLevelListener(new ParrotMainCamera.ZoomLevelListener() {
            private double _last_value = NaN;
            private String ZoomLevelFormat = "Zoom %.2f";
            @Override
            public void onChange(double new_zoom_level) {
                if (_last_value != NaN)
                    e_v.getInfoDisplay().remove(String.format(ZoomLevelFormat, _last_value));
                e_v.getInfoDisplay().addTemporarily(String.format(ZoomLevelFormat, new_zoom_level), 1500);
            }
        });
        _main_camera.start();
    }

    private void setupTriggerPictureButton(ErrorTextView e_v) {
        Button _pic_trigger_btn = findViewById(R.id.pic_trigger_btn);
        _pic_trigger_btn.setOnClickListener(v -> {
            if (_main_camera.triggerPhotoPicture(true)) {
                Animation blink_animation = new AlphaAnimation(1, 0.0f); // Change alpha from fully visible to invisible
                blink_animation.setDuration(200); // duration
                blink_animation.setInterpolator(new LinearInterpolator()); // do not alter animation rate
                v.startAnimation(blink_animation);
            }
            else {
                e_v.getInfoDisplay().addTemporarily("Error triggering a picture", 5000);
            }
        });

    }

    private void setupRecordingButton() {
        ToggleButtonExView _video_record_btn = findViewById(R.id.video_record_btn);
        Animation blink_animation = new AlphaAnimation(1, 0.0f); // Change alpha from fully visible to invisible
        _video_record_btn.setActiveListener(v -> {
            if (_main_camera.recordVideo(true)) {
                blink_animation.setDuration(1000); // duration
                blink_animation.setInterpolator(new LinearInterpolator()); // do not alter animation rate
                blink_animation.setRepeatCount(-1); // Repeat animation infinitely
                blink_animation.setRepeatMode(Animation.REVERSE);
                v.startAnimation(blink_animation);
                return true;
            }
            return false;
        });
        _video_record_btn.setInactiveListener(v -> {
            if (_main_camera.recordVideo(false)) {
                blink_animation.cancel();
                return true;
            }
            return false;
        });
    }

    private void setupZoomGesture(MultiTouchGestures mtg) {
        final ErrorTextView e_v = findViewById(R.id.info_warnings_errors);

        final String ErrorMessage = "Zoom is not available";
        final Handler retry_handler = new Handler(getMainLooper());

        e_v.getInfoDisplay().remove(ErrorMessage);
        mtg.setOnScaleListener(scale_factor -> {
            ParrotMainCamera.ZoomResult zr = _main_camera.setZoomRelative(scale_factor);
            if (zr == ParrotMainCamera.ZoomResult.BAD) {
                e_v.getInfoDisplay().add(ErrorMessage);
                retry_handler.postDelayed(() -> setupZoomGesture(mtg), 1000);
                return false;
            }
            else if (zr == ParrotMainCamera.ZoomResult.TOO_FAST)
                return false;
            return true;
        });
    }

    private void setupGimbalPitchGesture(MultiTouchGestures mtg, View scrollable_view) {
        final ErrorTextView e_v = findViewById(R.id.info_warnings_errors);
        final String ErrorMessage = "Gimbal pitch control is not available";
        e_v.getInfoDisplay().add(ErrorMessage);

        _gimbal = new ParrotGimbal(_drone);
        _gimbal.setPeripheralListener(new IPeripheral.IPeripheralListener<ParrotGimbal>() {
            @Override
            public void onChange(@NonNull @NotNull ParrotGimbal parrot_gimbal) {

            }

            @Override
            public boolean onFirstTimeAvailable(@NonNull @NotNull ParrotGimbal parrot_gimbal) {
                if (!parrot_gimbal.supportsPitch())
                    return false;

                mtg.setOnScrollListener( (dx, dy) -> {
                    float adim_dy = -dy/ Math.abs(scrollable_view.getHeight());
                    return parrot_gimbal.setAttitudeRelative( adim_dy*100.0f, 0.0f, 0.0f);
                });

                e_v.getInfoDisplay().remove(ErrorMessage);
                return true;
            }
        });

        _gimbal.setPitchListener(new ParrotGimbal.AttitudeListener() {
            private double old_value = NaN;
            private String PitchFormat = "Camera Pitch: %.1f deg";
            @Override
            public void onChange(double degrees) {
                if (degrees == old_value)
                    return;
                e_v.getInfoDisplay().remove(String.format(PitchFormat, old_value));
                old_value = degrees;
                e_v.getInfoDisplay().addTemporarily(String.format(PitchFormat, degrees), 1500);
            }
        });
        _gimbal.start();
    }

    private void setupMultiTouchGestures() {
        SurfaceViewRenderer v = findViewById(R.id.video_view);

        MultiTouchGestures mtg = new MultiTouchGestures(this);
        v.setOnTouchListener(mtg);
        v.post(() -> setupZoomGesture(mtg));
        v.post(() -> setupGimbalPitchGesture(mtg, v));
    }


    private void setupMediaStoreComponents(String user_email, String token) {
        _media_store = new ParrotMediaStore(_drone, 50, Bitmap.CompressFormat.JPEG);
        final ErrorTextView e_v = findViewById(R.id.info_warnings_errors);
        final String startup_warning = "Media store not available";
        e_v.getInfoDisplay().add(startup_warning);

        _media_store.setNewPhotosListener(new_photos -> {
            e_v.getInfoDisplay().addTemporarily(String.format("New photos: %d", new_photos.size()), 5000);
            for (FileEntry item : new_photos) {
                try {
                    UploadMediaHelper upload_media_helper = new UploadMediaHelper(e_v.getInfoDisplay(),
                        new UploadMediaHelper.Listener()
                                {
                                    @Override
                                    public void onSuccess() {
                                        e_v.getInfoDisplay().addTemporarily("Thumbnail uploaded", 5000);
                                    }

                                    @Override
                                    public void onUploadError(String error) {
                                        e_v.getInfoDisplay().addTemporarily(error, 5000);
                                    }

                                    @Override
                                    public boolean onProgress(int percent) {
                                        return true;
                                    }

                                },
                        user_email,
                        token,
                        DroHubHelper.getURL(getApplicationContext(), R.string.upload_media_url),
                        item,
                        true);

                    upload_media_helper.upload();
                }
                catch (IllegalAccessException | URISyntaxException e) {
                    e_v.getInfoDisplay().addTemporarily("Failed to upload photo thumbnail", 5000);
                }
            }
        });

        _media_store.setNewVideosListener(new_videos -> {
            e_v.getInfoDisplay().addTemporarily(String.format("New Videos: %d", new_videos.size()), 5000);
        });

        _media_store.setPeripheralListener(new IPeripheral.IPeripheralListener<ParrotMediaStore>() {
            @Override
            public void onChange(@NonNull @NotNull ParrotMediaStore parrotMediaStore) {}

            @Override
            public boolean onFirstTimeAvailable(@NonNull @NotNull ParrotMediaStore parrotMediaStore) {
                e_v.getInfoDisplay().remove(startup_warning);
                return true;
            }
        });
        _media_store.start();
    }

    private void setupLiveVideo(String user_email, String auth_token) {
        CopterHudActivity activity = this;
        ParrotStreamServer stream_server = new ParrotStreamServer(_drone);
        stream_server.setCapturerListener(640, 480, 20, (peripheral, capturer_generator) -> {
            final String CONNECTION_ERROR = "DROHUB rejected our connection";
            String[] res_turn_urls = activity.getResources().getStringArray(R.array.ice_servers);
            PeerConnectionParameters peerConnectionParameters = new PeerConnectionParameters(
                    mStreamView,
                    null,
                    null,
                    null,
                    res_turn_urls,
                    getString(R.string.janus_websocket_uri),
                    activity,
                    "VP9",
                    20480000,
                    capturer_generator,
                    128000,
                    "opus",
                    1.0f/10.0f,
                    2000);

            _audio_manager = (AudioManager)activity.getSystemService(Context.AUDIO_SERVICE);
            final ErrorTextView e_v = findViewById(R.id.info_warnings_errors);

            if (_audio_manager != null) {
                _audio_manager.setMode(AudioManager.MODE_IN_CALL);
                _audio_manager.setSpeakerphoneOn(true);
            }
            else
                e_v.getInfoDisplay().add("Could not setup audio error");


            _drohub_handler = new DroHubHandler(
                    _drone.getUid(),
                    peerConnectionParameters,
                    activity,
                    e_v.getInfoDisplay()
            );

            setupTimer();
            setupMuteMicrophoneButton(_drohub_handler);

            _thrift_connection = new ThriftConnection();
            try {
                _thrift_connection.onStart(_drone.getUid(),
                        getString(R.string.drohub_ws_url),
                        _drohub_handler, user_email, auth_token);
            } catch (InterruptedException e) {
                e_v.getInfoDisplay().add(CONNECTION_ERROR);
                return false;
            }
            Log.w("COPTER", "Started thrift connection to " + getString(R.string.drohub_ws_url));
            e_v.getInfoDisplay().remove(CONNECTION_ERROR);
            return true;
        });
        stream_server.start();
    }

    private void setupTimer() {
        TimerView timer_view = findViewById(R.id.info_flight_time);
        timer_view.startTimer();
    }

    private void setupMuteMicrophoneButton(DroHubHandler drohub_handler) {
        ToggleButtonExView mute_button = findViewById(R.id.mute_btn);
        mute_button.setActiveListener(v -> {
            boolean r = drohub_handler.setMicrophoneMute(true);
            if (r)
                mute_button.setText("Unmute");
            return r;
        });

        mute_button.setInactiveListener(v ->{
            boolean r = drohub_handler.setMicrophoneMute(false);
            if(r)
                mute_button.setText("Mute");
            return r;
        });
    }

}
