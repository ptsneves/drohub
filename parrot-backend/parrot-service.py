#!/usr/bin/env python3
# -*- coding: UTF-8 -*-
from concurrent import futures
from collections import deque
import logging
import time
import threading
import argparse

import grpc
import DrohubGRPCProto.python.drohub_pb2_grpc as drohub_pb2_grpc
import DrohubGRPCProto.python.drohub_pb2 as drohub_pb2

import olympe
import olympe_deps

from olympe.messages.ardrone3.Piloting import TakeOff, moveBy, Landing, moveTo
from olympe.messages.ardrone3.PilotingState import FlyingStateChanged
from olympe.messages.common.SettingsState import ProductSerialHighChanged, ProductSerialLowChanged
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
        def wrapper(*args):
            with args[0].lock:
                return function(*args)
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
    def __init__(self):
        self.container = deque(maxlen=3)
        self.lk = threading.Lock()
        self.cv_consumer = threading.Condition(self.lk)

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
    def __init__(self):
        super().__init__()

    def dispatchPosition(self, message):
        new_drone_position = drohub_pb2.DronePosition(
                        latitude = message.state()['latitude'],
                        longitude = message.state()['longitude'],
                        altitude = message.state()['altitude'],
                        serial = "d34174f4-285b-46e8-b615-89ce6959b49c",
                        timestamp = int(time.time()))

        logging.debug(new_drone_position)
        super().append(new_drone_position)

    def getLastPosition(self):
        return super().getLastElement()

class BatteryLevelContainer(DroneMessageContainerBase):
    def __init__(self):
        super().__init__()

    def dispatchBatteryLevel(self, message):
        new_battery_level = drohub_pb2.DroneBatteryLevel(
            battery_level_percent=message.state()['percent'],
            serial = "d34174f4-285b-46e8-b615-89ce6959b49c",
            timestamp=int(time.time()))

        logging.debug(new_battery_level)
        super().append(new_battery_level)

class DroneRAII(object):
    def __init__(self, ip, callback_list = []):
        super().__init__()
        self._drone = olympe.Drone(ip, mpp=True, drone_type=olympe_deps.ARSDK_DEVICE_TYPE_ANAFI4K, loglevel=TraceLogger.level.warning, callbacks=callback_list)
        self._drone.connection()
        self._drone(setPilotingSource(source="SkyController")).wait()

    def __del__(self):
        try:
            #For the case the class is even not constructed
            self._drone.disconnection()
        except AttributeError:
            pass

class DroneThreadSafe(DroneRAII):
    def __init__(self, *args):
        super().__init__(*args)
        self._lock = threading.Lock()

    def getDrone(self):
        with self._lock:
            return self._drone

class DronePersistentConnection(DroneThreadSafe):
    def __init__(self, *args):
        super().__init__(*args)
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

class DroneChooser(DronePersistentConnection):
    def __init__(self, drone_type, *args):
        if drone_type == "simulator":
            self._ip = '10.202.0.1'
        elif drone_type == "anafi":
            self._ip = '192.168.53.1'
        else:
            raise Exception("Unknown drone type {} passed.".format(drone_type))
        super().__init__(self._ip, *args)

class DroneRPC(drohub_pb2_grpc.DroneServicer):
    def __init__(self, drone_type):
        self.serial = ParrotSerialNumber()
        self.position_container = PositionContainer()
        self.battery_level_container = BatteryLevelContainer()
        self.drone = DroneChooser(drone_type, [self.cb1])
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

    def getPosition(self, request, context):
        while True:
            yield self.position_container.getLastPosition()

    def getBatteryLevel(self, request, context):
        while True:
            yield self.battery_level_container.getLastElement()

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
