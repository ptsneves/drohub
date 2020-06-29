package com.drohub.Views;

import android.content.Context;
import android.graphics.*;
import android.graphics.drawable.Drawable;
import android.location.Location;
import android.util.AttributeSet;
import com.drohub.R;
import com.google.android.gms.maps.*;
import com.google.android.gms.maps.model.*;

public class DroHubMapView extends MapView implements OnMapReadyCallback {
    private static final double LN2 = 0.6931471805599453;
    private static final int WORLD_PX_HEIGHT = 256;
    private static final int WORLD_PX_WIDTH = 256;
    private static final int ZOOM_MAX = 21;

    private OnMapReadyCallback _map_ready_callback;
    private GoogleMap _google_map;
    private Boolean _need_redraw;
    private Location _drone_location;
    private Location _user_location;

    private Marker _drone_marker;
    private Marker _user_marker;

    private Paint _paint;
    private float _user_heading;

    public DroHubMapView(Context context, AttributeSet attrs) {
        this(context, attrs, 0);
    }

    public DroHubMapView(Context context, AttributeSet attrs, int defStyleAttr) {
        super(context, attrs, defStyleAttr);
        init();
    }

    public void setUserHeading(float new_heading) {
        _user_heading = new_heading;
        _need_redraw = true;
    }

    private BitmapDescriptor getMarkerIconFromDrawable(Drawable drawable) {
        Canvas canvas = new Canvas();
        Bitmap bitmap = Bitmap.createBitmap(drawable.getIntrinsicWidth(), drawable.getIntrinsicHeight(), Bitmap.Config.ARGB_8888);
        canvas.setBitmap(bitmap);
        drawable.setBounds(0, 0, drawable.getIntrinsicWidth(), drawable.getIntrinsicHeight());
        drawable.draw(canvas);
        return BitmapDescriptorFactory.fromBitmap(bitmap);
    }

    private void setMarkerPosition(Marker marker, Location location) {
        if (marker == null)
            return;
        marker.setPosition(new LatLng(location.getLatitude(), location.getLongitude()));
    }

    public void setUserLocation(Location new_location) {
        _user_location = new_location;
        if (_user_marker != null)
            setMarkerPosition(_user_marker, new_location);


        _need_redraw = true;
        _updateMap();
    }

    public void setDroneLocation(Location new_location) {

        _drone_location = new_location;
        if (_drone_location != null)
            setMarkerPosition(_drone_marker, new_location);

        _need_redraw = true;
        _updateMap();
    }

    @Override
    public void dispatchDraw(Canvas canvas) {
        clipMapToCircle(canvas);
        super.dispatchDraw(canvas);
        canvas.save();
        drawDistanceText(canvas);
        drawMapBorder(canvas);
        canvas.restore();
        _updateMap();
    }

    private void clipMapToCircle(Canvas canvas){
        Path p = new Path();
        p.addCircle(getWidth()/2, getHeight()/2, getWidth()/2, Path.Direction.CW);
        canvas.clipPath(p);
    }

    private void drawMapBorder(Canvas canvas) {
        if (_google_map == null)
            return;

        final float centerX =  canvas.getWidth() / 2.0f;
        final float centerY =  canvas.getHeight() / 2.0f;

        _paint.setColor(Color.BLACK);
        _paint.setAntiAlias(true);
        _paint.setTextSize(50f);
        _paint.setStrokeWidth(4);

        canvas.drawCircle(centerX, centerY, getWidth()/2.0f, _paint);
    }

    private void drawDistanceText(Canvas canvas) {
        if (_google_map == null)
            return;


        if (_drone_location != null && _user_location != null) {
            _paint.setColor(Color.BLACK);
            _paint.setAntiAlias(true);
            _paint.setTextSize(50f);
            _paint.setStrokeWidth(4);


            final float centerX =  canvas.getWidth() / 2.0f;
            final float centerY =  canvas.getHeight() / 2.0f;
            float distance = _drone_location.distanceTo(_user_location);
            _paint.setTextAlign(Paint.Align.CENTER);
            float y_text_position = centerY + getHeight()/2.0f * 0.5f;
            _paint.setStyle(Paint.Style.FILL);
            canvas.drawText(String.format("%.2f m", distance), centerX, y_text_position, _paint);
        }

        _paint.setStyle(Paint.Style.STROKE);
    }

    public int getBoundsZoomLevel(LatLngBounds bounds, int mapWidthPx, int mapHeightPx) {
        LatLng ne = bounds.northeast;
        LatLng sw = bounds.southwest;

        double latFraction = (latRad(ne.latitude) - latRad(sw.latitude)) / Math.PI;

        double lngDiff = ne.longitude - sw.longitude;
        double lngFraction = ((lngDiff < 0) ? (lngDiff + 360) : lngDiff) / 360;

        double latZoom = zoom(mapHeightPx, WORLD_PX_HEIGHT, latFraction);
        double lngZoom = zoom(mapWidthPx, WORLD_PX_WIDTH, lngFraction);

        int result = Math.min((int)latZoom, (int)lngZoom);
        return Math.min(result, ZOOM_MAX);
    }

    private double latRad(double lat) {
        double sin = Math.sin(lat * Math.PI / 180);
        double radX2 = Math.log((1 + sin) / (1 - sin)) / 2;
        return Math.max(Math.min(radX2, Math.PI), -Math.PI) / 2;
    }

    private double zoom(int mapPx, int worldPx, double fraction) {
        return Math.floor(Math.log(mapPx / worldPx / fraction) / LN2);
    }

    private void _updateMap() {
        if ( !_need_redraw || _google_map == null || _drone_location == null || _user_location == null) {
            return;
        }

        LatLngBounds.Builder builder = new LatLngBounds.Builder();
        builder.include(new LatLng(_drone_location.getLatitude(), _drone_location.getLongitude()));
        builder.include(new LatLng(_user_location.getLatitude(), _user_location.getLongitude()));
        LatLngBounds bounds = builder.build();

        CameraPosition cp = new CameraPosition.Builder()
                .target(_user_marker.getPosition())
                .bearing(_user_heading)
                .zoom(getBoundsZoomLevel(bounds, getWidth(), getHeight())-2)
                .build();

        CameraUpdate cu = CameraUpdateFactory.newCameraPosition(cp);
//        CameraUpdate cu = CameraUpdateFactory.newLatLngBounds(bounds, 100);
        _google_map.moveCamera(cu);
        _need_redraw = false;
    }

    @Override
    public void getMapAsync(OnMapReadyCallback callback) {
        _map_ready_callback = callback;
        super.getMapAsync(this);
    }

    @Override
    public void onMapReady(GoogleMap googleMap) {
        if (_google_map != null)
            return;

        _google_map = googleMap;
        _google_map.getUiSettings().setAllGesturesEnabled(false);
        _google_map.setOnCameraMoveListener(() -> invalidate());

        final Drawable drone_drawable = this.getContext().getDrawable(R.drawable.ic_drone_icon);
        _drone_marker = _google_map.addMarker(new MarkerOptions()
            .icon(getMarkerIconFromDrawable(drone_drawable))
            .anchor(0.5f, 0.5f)
            .position(new LatLng(0, 0)));

        final Drawable user_drawable = this.getContext().getDrawable(R.drawable.ic_street_view_solid);
        _user_marker = _google_map.addMarker(new MarkerOptions()
                .icon(getMarkerIconFromDrawable(user_drawable))
                .anchor(0.5f, 0.5f)
                .position(new LatLng(0, 0)));

        if (_user_location != null)
            setMarkerPosition(_user_marker, _user_location);

        if (_drone_location != null)
            setMarkerPosition(_drone_marker, _drone_location);

        if (_map_ready_callback != null) {
            _map_ready_callback.onMapReady(googleMap);
        }
    }

    private void init() {
        setWillNotDraw(false);
        _need_redraw = false;
        _paint = new Paint();
    }

}
