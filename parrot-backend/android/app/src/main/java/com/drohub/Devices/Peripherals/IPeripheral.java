package com.drohub.Devices.Peripherals;

import androidx.annotation.NonNull;
import com.drohub.Janus.PeerConnectionParameters;


public interface IPeripheral<P>{
    public void setPeripheralListener(IPeripheralListener<P> l);
    public void start();
    public interface IPeripheralListener<C> {
        void onChange(@NonNull C c);
        boolean onFirstTimeAvailable(@NonNull C c);
    }
    public interface IVideoCapturerListener<P> {
        boolean onCapturerAvailable(P peripheral, PeerConnectionParameters.CapturerGenerator capturer_generator);
    }

    public interface ICapturerProvider<S> {
        void setCapturerListener(
                int video_width,
                int video_height,
                int video_fps,
                IVideoCapturerListener<S> listener);
    }
}
