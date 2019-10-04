#!/usr/bin/env python3
# -*- coding: UTF-8 -*-
import sys
sys.path.append('../')
from concurrent import futures
from collections import deque
import logging
import time
import threading
import argparse
import subprocess
import io
import shutil
import urllib.request
import json

import grpc
import DrohubGRPCProto.python.drohub_pb2_grpc as drohub_pb2_grpc
import DrohubGRPCProto.python.drohub_pb2 as drohub_pb2

import olympe
import olympe_deps

from olympe.messages.ardrone3.Piloting import TakeOff, moveBy, Landing, moveTo
from olympe.messages.ardrone3.PilotingState import FlyingStateChanged
from olympe.messages.common.SettingsState import ProductSerialHighChanged, ProductSerialLowChanged
from olympe.messages.common.CommonState import LinkSignalQuality
from olympe.enums.drone_manager import connection_state
from olympe.enums.ardrone3.Piloting import MoveTo_Orientation_mode
from olympe.messages.ardrone3.PilotingSettingsState import MaxTiltChanged
from olympe.messages.skyctrl.CoPiloting import setPilotingSource

from olympe.tools.logger import TraceLogger, DroneLogger, ErrorCodeDrone
_ONE_DAY_IN_SECONDS = 60 * 60 * 24

class ParrotSerialNumber():
    def __init__(self):
        self.lock = threading.Lock()
        self.serial = ""
        self._MSB = ""
        self._MSB_set = False
        self._LSB = ""
        self._LSB_set = False

    def _useLock(function):
        def wrapper(*args, **kwargs):
            with args[0].lock:
                return function(*args, **kwargs)
        return wrapper

    @_useLock
    def setMSB(self, msb):
        if self._MSB_set:
            if msb == self._MSB:
                return
            raise Exception("MSB is already set cannot set it again to a different value.")
        self._MSB = msb
        self._MSB_set = True
        if self._LSB_set:
            self.serial = msb + self._LSB


    @_useLock
    def setLSB(self, lsb):
        if self._LSB_set:
            if lsb == self._LSB:
                return
            raise Exception("LSB is already set cannot set it again to a different value.")

        self._LSB = lsb
        self._LSB_set = True
        if self._MSB_set:
            self.serial = self._MSB + lsb

    @_useLock
    def Get(self):
        if self._MSB_set and self._LSB_set:
            return self.serial
        raise Exception("Cannot get serial until we have received all the serial message parts.")

class DroneMessageContainerBase():
    def __init__(self, drone_serial):
        self.container = deque(maxlen=3)
        self.lk = threading.Lock()
        self.cv_consumer = threading.Condition(self.lk)
        self.drone_serial = drone_serial

    def append(self, new_drone_position):
        self.container.append(new_drone_position)
        with self.cv_consumer:
            self.cv_consumer.notify_all()

    def getLastElement(self):
        with self.cv_consumer:
            self.cv_consumer.wait()
            container_copy = self.container
            while len((container_copy)) > 0:
                return container_copy.popleft()

class PositionContainer(DroneMessageContainerBase):
    def __init__(selfi, drone_serial):
        super().__init__(drone_serial)

    def dispatchPosition(self, message):

        if message.state()['latitude'] == 500.0 or message.state()['longitude'] == 500.0:
            logging.debug("Received invalid position due to lack of position lock (GPS etc)")
            #This means that we do not have a position lock. Lets not send this
            #as this parrot specific.
            return

        new_drone_position = drohub_pb2.DronePosition(
                        latitude = message.state()['latitude'],
                        longitude = message.state()['longitude'],
                        altitude = message.state()['altitude'],
                        serial = self.drone_serial.Get(),
                        timestamp = int(time.time()))

        logging.debug(new_drone_position)
        super().append(new_drone_position)

    def getLastPosition(self):
        return super().getLastElement()

class FlyingStateContainer(DroneMessageContainerBase):
    def __init__(selfi, drone_serial):
        super().__init__(drone_serial)

    def mapStateToEnum(self, state):
        flying_state_enum = -1
        if str(state) == "FlyingStateChanged_State.landed":
            flying_state_enum = 0
        elif str(state) == "FlyingStateChanged_State.takingoff":
            flying_state_enum = 1
        elif str(state) == "FlyingStateChanged_State.hovering":
            flying_state_enum = 2
        elif str(state) == "FlyingStateChanged_State.flying":
            flying_state_enum = 3
        elif str(state) == "FlyingStateChanged_State.landing":
            flying_state_enum = 4
        elif str(state) == "FlyingStateChanged_State.emergency":
            flying_state_enum = 5
        elif str(state) == "FlyingStateChanged_State.usertakeoff":
            flying_state_enum = 6
        elif str(state) == "FlyingStateChanged_State.motor_ramping":
            flying_state_enum = 7
        elif str(state) == "FlyingStateChanged_State.emergency_landing":
            flying_state_enum = 8
        else:
            raise Exception("Unexpected state {} received".format(state))

        return flying_state_enum

    def dispatchFlyingState(self, message):
        new_drone_flying_state = drohub_pb2.DroneFlyingState(
                        state = self.mapStateToEnum(message.state()['state']),
                        serial = self.drone_serial.Get(),
                        timestamp = int(time.time()))

        logging.debug(new_drone_flying_state)
        super().append(new_drone_flying_state)

    def getLastFlyingState(self):
        return super().getLastElement()

class BatteryLevelContainer(DroneMessageContainerBase):
    def __init__(self, drone_serial):
        super().__init__(drone_serial)

    def dispatchBatteryLevel(self, message):
        new_battery_level = drohub_pb2.DroneBatteryLevel(
            battery_level_percent=message.state()['percent'],
            serial = self.drone_serial.Get(),
            timestamp=int(time.time()))

        logging.debug(new_battery_level)
        super().append(new_battery_level)

class RadioSignalContainer(DroneMessageContainerBase):
    def __init__(self, drone_serial):
        super().__init__(drone_serial)

    def dispatchRadioRSSILevel(self, message):
        new_rssi_level = drohub_pb2.DroneRadioSignal(
            serial = self.drone_serial.Get(),
            timestamp=int(time.time()),
            rssi = message.state()['rssi']
        )
        logging.debug(new_rssi_level)
        super().append(new_rssi_level)

    def dispatchRadioSignalQuality(self, message):
        new_signal_quality = drohub_pb2.DroneRadioSignal(
            serial = self.drone_serial.Get(),
            timestamp=int(time.time()),
            signal_quality = message.state()['value']
        )
        logging.debug(new_signal_quality)
        super().append(new_signal_quality)


class DroneChooser(object):
    def __init__(self, drone_type):
        self._drone_args_dict = {}
        self._drone_args_dict["loglevel"] = TraceLogger.level.warning
        if drone_type == "simulator":
            self._ip = '10.202.0.1'
        elif drone_type == "anafi":
            self._ip = '192.168.53.1'
            self._drone_args_dict["mpp"] = True
            self._drone_args_dict["drone_type"] = olympe_deps.ARSDK_DEVICE_TYPE_ANAFI4K
        else:
            raise Exception("Unknown drone type {} passed.".format(drone_type))

    def getIP(self):
        return self._ip

class DroneVideoEncoder(DroneChooser):
    Vp8_Command = "/usr/bin/ffmpeg -hwaccel vaapi -i rtsp://{source_url}/live -r 10 -c:v libvpx \
            -deadline realtime -threads 4 -speed -5 -skip_threshold 60 -vp8flags error_resilient -f rtp rtp://docker:6004"

    H264_Command = '/usr/bin/ffmpeg -r 10 -threads 4 -hwaccel vaapi -i rtsp://{source_url}/live -vf format=yuv420p \
            -vaapi_device /dev/dri/renderD128 -c:v h264_vaapi -profile:v constrained_baseline -level 3.0 -bf 0 -bsf: v \
            "dump_extra=freq=keyframe" -vf "format=nv12,hwupload" -f rtp rtp://docker:5004?pkt_size=1300"'

    def __init__(self, drone_type, destination_rtp_ip):
        super().__init__(drone_type)
        self._destination_rtp_ip = destination_rtp_ip
        self._rtsp_source_url = self._ip
        if not self._doesCommandExist("ffmpeg"):
            raise Exception("ffmpeg does not exist. And we need it to relay the video")


        if not self.runProcess(DroneVideoEncoder.Vp8_Command.format(source_url=self._ip,
            dest_ip = self._destination_rtp_ip)):

            raise Exception("Could not spawn Vp8 video relayer.")

        if not self.runProcess(DroneVideoEncoder.H264_Command.format(source_url=self._ip,
            dest_ip = self._destination_rtp_ip)):

            raise Exception("Could not spawn H264 video relayer.")


    def _doesCommandExist(self, command):
        return shutil.which(command)

    def runProcess(self, command):
        process = subprocess.Popen(command, shell=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        #process.wait()
        logging.info(command)

        return True
        return {"ret": 0,
            "command": command,
            "stdout": io.TextIOWrapper(process.stdout, encoding='utf-8').readlines(),
            "stderr" : io.TextIOWrapper(process.stderr, encoding='utf-8').readlines()}

class DroneRAII(DroneVideoEncoder):
    def __init__(self, *args, **kwargs):
        super().__init__(*args)
        self._drone = olympe.Drone(self._ip, **{**self._drone_args_dict, **kwargs})
        self._drone.connection()
        self._drone(setPilotingSource(source="SkyController")).wait()

    def __del__(self):
        try:
            #For the case the class is even not constructed
            self._drone.disconnection()
        except AttributeError:
            pass

class DroneThreadSafe(DroneRAII):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._lock = threading.Lock()

    def getDrone(self):
        with self._lock:
            return self._drone

class DronePersistentConnection(DroneThreadSafe):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.stop_keep_alive = True
        threading.Thread(target = self._keepConnectionAlive).start()

    def _checkDroneConnected(self):
        state = self.getDrone().connection_state()
        if state.OK:
            return True
        else:
            return False

    def _reconnectDrone(self):
        logging.info("Trying to connect")
        if not self.getDrone().connection():
            raise Exception("Drone is not connected")

    def _keepConnectionAlive(self):
        self.stop_keep_alive = False
        while (self.stop_keep_alive == False):
            if self._checkDroneConnected() == False:
                try:
                   self._reconnectDrone()
                except Exception:
                    logging.warning("Reconnection failed. Trying again")


class DroneRPC(drohub_pb2_grpc.DroneServicer):
    def __init__(self, drone_type):
        self.serial = ParrotSerialNumber()
        self.position_container = PositionContainer(self.serial)
        self.battery_level_container = BatteryLevelContainer(self.serial)
        self.radio_signal_container = RadioSignalContainer(self.serial)
        self.flying_state_container = FlyingStateContainer(self.serial)
        self.drone = DronePersistentConnection(drone_type, "127.0.0.1", callbacks = [self.cb1])
        super().__init__()

    def cb1(self, message):
        if message.Full_Name == "Ardrone3_PilotingState_PositionChanged":
            self.position_container.dispatchPosition(message)
        elif message.Full_Name == "Common_SettingsState_ProductSerialHighChanged":
            self.serial.setMSB(message.state()['high'])
        elif message.Full_Name == "Common_SettingsState_ProductSerialLowChanged":
            self.serial.setLSB(message.state()['low'])
        elif message.Full_Name == "Common_CommonState_BatteryStateChanged" or message.Full_Name == "Battery_Alert":
            self.battery_level_container.dispatchBatteryLevel(message)
        elif message.Full_Name == "Common_CommonState_LinkSignalQuality":
            self.radio_signal_container.dispatchRadioSignalQuality(message)
        elif message.Full_Name == "Wifi_Rssi_changed":
            self.radio_signal_container.dispatchRadioRSSILevel(message)
        elif message.Full_Name == "Ardrone3_PilotingState_FlyingStateChanged":
            self.flying_state_container.dispatchFlyingState(message)

    def getPosition(self, request, context):
        while True:
            yield self.position_container.getLastPosition()

    def getBatteryLevel(self, request, context):
        while True:
            yield self.battery_level_container.getLastElement()

    def getRadioSignal(self, request, context):
        while True:
            yield self.radio_signal_container.getLastElement()

    def getFlyingState(self, request, context):
        while True:
            yield self.flying_state_container.getLastElement()

    def doTakeoff(self, request, context):
        logging.warning("Taking off")
        takeoff = self.drone.getDrone()(
            TakeOff()
            >> FlyingStateChanged(state="hovering", _timeout=5)
        ).wait()
        return drohub_pb2.DroneReply(message=takeoff.success())

    def doLanding(self, request, context):
        landing = self.drone.getDrone()(Landing()).wait()
        return drohub_pb2.DroneReply(message=landing.success())

    def moveToPosition(self, request, context):
        go_to_position = self.drone.getDrone()(moveTo(
            request.latitude, request.longitude, request.altitude,  MoveTo_Orientation_mode.HEADING_DURING, request.heading)
        ).wait()
        return drohub_pb2.DroneReply(message=go_to_position.success())

    def getFileList(self, request, context):
        resource_list_url = "http://{}:180/api/v1/media/medias".format(self.drone.getIP())
        logging.debug("Trying to access API at {}".format(resource_list_url))
        with urllib.request.urlopen(resource_list_url) as http_request:
            drone_file_list = drohub_pb2.DroneFileList()
            drone_file_list.serial = self.serial.Get()
            drone_file_list.timestamp = int(time.time())


            query_result = json.loads(http_request.read().decode('utf-8'))
            for high_level_resource in query_result:
                for resource in high_level_resource["resources"]:
                    media_type = None
                    if resource["type"] == "VIDEO":
                        media_type = drohub_pb2.FileEntry.ResourceType.VIDEO
                    elif resource["type"] == "IMAGE":
                        media_type = drohub_pb2.FileEntry.ResourceType.IMAGE
                    else:
                        media_type = drohub_pb2.FileEntry.ResourceType.OTHER

                    new_entry = drone_file_list.file_entries.add()
                    new_entry.resource_id = resource["url"]
                    new_entry.resource_type = media_type
                    if "thumbnail" in resource:
                        new_entry.thumbnail_id = resource["thumbnail"]

            return drone_file_list


def serve(drone_type):
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    drohub_pb2_grpc.add_DroneServicer_to_server(DroneRPC(drone_type), server)
    server.add_insecure_port('[::]:50051')
    server.start()
    try:
        while True:
            time.sleep(_ONE_DAY_IN_SECONDS)
    except KeyboardInterrupt:
        server.stop(0)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Parrot ANAFI service.')

    parser.add_argument('--simulator', dest='drone_type', action='store_const',
                        const='simulator', default='anafi',
                        help='Connect to a simulator. (Default connect to real ANAFI')
    parser.add_argument('--verbose', dest="verbosity", action="store_const",
                        const=logging.DEBUG, default = logging.INFO,
                        help="Whether to print debugging information")
    args = parser.parse_args()
    logging.basicConfig(level=args.verbosity, format='%(relativeCreated)6d %(threadName)s %(message)s')

    serve(args.drone_type)
