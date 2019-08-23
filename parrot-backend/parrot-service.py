#!/usr/bin/env python3
# -*- coding: UTF-8 -*-
from concurrent import futures
from collections import deque
import logging
import time
import threading

import grpc
import DrohubGRPCProto.python.drohub_pb2_grpc as drohub_pb2_grpc
import DrohubGRPCProto.python.drohub_pb2 as drohub_pb2

import olympe
from olympe.messages.ardrone3.Piloting import TakeOff, moveBy, Landing, moveTo
from olympe.messages.ardrone3.PilotingState import FlyingStateChanged
from olympe.messages.common.SettingsState import ProductSerialHighChanged, ProductSerialLowChanged
from olympe.enums.drone_manager import connection_state
from olympe.enums.ardrone3.Piloting import MoveTo_Orientation_mode
from olympe.messages.ardrone3.PilotingSettingsState import MaxTiltChanged
_ONE_DAY_IN_SECONDS = 60 * 60 * 24

class ParrotSerialNumber():
    SERIAL_MAXIMUM_NUMBER = 0xFFFFFFFFFFFFFFFFFF
    def __init__(self):
        self.lock = threading.Lock()
        self.serial = 0x0
        self._MSB = 0x0
        self._MSB_set = False
        self._LSB = 0x0
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

        self.serial = self.serial | (msb <<ParrotSerialNumber.SERIAL_MAXIMUM_NUMBER)
        self._MSB = msb
        self._MSB_set = True

    @_useLock
    def setLSB(self, lsb):
        if self._LSB_set:
            if lsb == self._LSB:
                return
            raise Exception("LSB is already set cannot set it again to a different value.")

        self.serial = self.serial | lsb
        self._LSB = lsb
        self._LSB_set = True

    @_useLock
    def Get(self):
        if self._MSB_set and self._LSB_set:
            return self.serial
        raise Exception("Cannot get serial until we have received all the serial message parts.")


class DroneRPC(drohub_pb2_grpc.DroneServicer):
    def __init__(self):
        super().__init__()
        self.positions = deque(maxlen=3)
        self.serial = ParrotSerialNumber()
        self.lk_positions = threading.Lock()
        self.cv_positions_consumer = threading.Condition(self.lk_positions)
        self.drone = olympe.Drone("10.202.0.1", callbacks = [self.cb1])
        self.drone.connection()

    def _checkDroneConnected(f):
        def wrapper(*args):
            state = args[0].drone.connection_state()
            if state.OK:
                return f(*args)
            else:
                print("Trying to connect")
                if not args[0].drone.connection():
                    raise Exception("Drone is not connected")
        return wrapper

    def dispatchPosition(self, message):
        new_drone_position = drohub_pb2.DronePosition(
                        latitude = message.state()['latitude'],
                        longitude = message.state()['longitude'],
                        altitude = message.state()['altitude'],
                        serial = "d34174f4-285b-46e8-b615-89ce6959b49c",
                        timestamp = int(time.time()))


        self.positions.append(new_drone_position)
        with self.cv_positions_consumer:
            self.cv_positions_consumer.notify_all()

    def cb1(self, message):
        if message.Full_Name == "Ardrone3_PilotingState_PositionChanged":
            self.dispatchPosition(message)
        elif message.Full_Name == "Common_SettingsState_ProductSerialHighChanged":
            self.serial.setMSB(int(message.state()['high']))
        elif message.Full_Name == "Common_SettingsState_ProductSerialLowChanged":
            self.serial.setLSB(int(message.state()['low']))

    @_checkDroneConnected
    def getPosition(self, request, context):
        while True:
            with self.cv_positions_consumer:
                self.cv_positions_consumer.wait()
                positions_copy = self.positions
                while len((positions_copy)) > 0:
                    yield positions_copy.popleft()

    @_checkDroneConnected
    def doTakeoff(self, request, context):
        print("Taking off")
        takeoff = self.drone(
            TakeOff()
            >> FlyingStateChanged(state="hovering", _timeout=5)
        ).wait()
        return drohub_pb2.DroneReply(message=takeoff.success())

    @_checkDroneConnected
    def doLanding(self, request, context):
        landing = self.drone(Landing()).wait()
        return drohub_pb2.DroneReply(message=landing.success())

    @_checkDroneConnected
    def moveToPosition(self, request, context):
        print(str(request))
        go_to_position = self.drone(moveTo(
            request.latitude, request.longitude, request.altitude,  MoveTo_Orientation_mode.HEADING_DURING, request.heading)
        ).wait()
        return drohub_pb2.DroneReply(message=go_to_position.success())

    def __del__(self):
        self.drone.disconnection()
def serve():
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    drohub_pb2_grpc.add_DroneServicer_to_server(DroneRPC(), server)
    server.add_insecure_port('[::]:50051')
    server.start()
    try:
        while True:
            time.sleep(_ONE_DAY_IN_SECONDS)
    except KeyboardInterrupt:
        server.stop(0)


if __name__ == '__main__':
    #logging.basicConfig()

    serve()
