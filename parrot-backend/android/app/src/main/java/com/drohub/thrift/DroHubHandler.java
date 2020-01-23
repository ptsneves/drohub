package com.drohub.thrift;

import org.apache.thrift.*;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.ArrayBlockingQueue;
import com.drohub.thift.gen.*;
import java.util.Date;

public class DroHubHandler implements Drone.Iface {
    private String serial_number;
    private BlockingQueue<DroneReply> drone_reply;

    public DroHubHandler(String serial) {
        serial_number = serial;
        drone_reply = new ArrayBlockingQueue<DroneReply>(2);
    }

    @Override
    public DroneReply pingService() throws TException {
        Date date = new Date();
        return new DroneReply(true, serial_number, date.getTime());
    }

    @Override
    public DroneVideoStateResult getVideoState(DroneSendVideoRequest request) throws TException {
        if (request.video_type == VideoType.VP8)
            return new DroneVideoStateResult(DroneVideoState.LIVE, request.rtp_url, serial_number, (new Date()).getTime());
        return new DroneVideoStateResult(DroneVideoState.DIED, request.rtp_url, serial_number, (new Date()).getTime());
    }

    @Override
    public DroneVideoStateResult sendVideoTo(DroneSendVideoRequest request) throws TException {
        return null;
    }

    @Override
    public DroneReply doTakeoff() throws TException {
        return null;
    }

    @Override
    public DroneReply doLanding() throws TException {
        return null;
    }

    @Override
    public DroneReply doReturnToHome() throws TException {
        return null;
    }

    @Override
    public DronePosition getPosition() throws TException {
        return null;
    }

    @Override
    public DroneBatteryLevel getBatteryLevel() throws TException {
        return null;
    }

    @Override
    public DroneFlyingState getFlyingState() throws TException {
        return null;
    }

    @Override
    public DroneRadioSignal getRadioSignal() throws TException {
        return null;
    }

    @Override
    public DroneReply moveToPosition(DroneRequestPosition request) throws TException {
        return null;
    }

    @Override
    public DroneFileList getFileList() throws TException {
        return null;
    }

    @Override
    public DroneReply recordVideo(DroneRecordVideoRequest request) throws TException {
        return null;
    }

    @Override
    public DroneReply takePicture(DroneTakePictureRequest request) throws TException {
        return null;
    }
}