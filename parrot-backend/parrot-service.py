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
from olympe.messages.ardrone3.Piloting import TakeOff, moveBy, Landing
from olympe.messages.ardrone3.PilotingState import FlyingStateChanged
from olympe.enums.drone_manager import connection_state
from olympe.messages.ardrone3.PilotingSettingsState import MaxTiltChanged
_ONE_DAY_IN_SECONDS = 60 * 60 * 24

def check_drone_connected(f):
    def wrapper(*args):
        state = args[0].drone.connection_state()
        if state.OK:
            return f(*args)
        else:
            print("Trying to connect")
            if not args[0].drone.connection():
                raise Exception("Drone is not connected")
    return wrapper

class DroneRPC(drohub_pb2_grpc.DroneServicer):
    def __init__(self):
        super().__init__()
        self.positions = deque(maxlen=3)
        self.lk_positions = threading.Lock()
        self.cv_positions_consumer = threading.Condition(self.lk_positions)
        self.drone = olympe.Drone("10.202.0.1", callbacks = [self.cb1])

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

    @check_drone_connected
    def getPosition(self, request, context):
        while True:
            with self.cv_positions_consumer:
                self.cv_positions_consumer.wait()
                positions_copy = self.positions
                while len((positions_copy)) > 0:
                    yield positions_copy.popleft()

    @check_drone_connected
    def doTakeoff(self, request, context):
        print("Taking off")
        takeoff = self.drone(
            TakeOff()
            >> FlyingStateChanged(state="hovering", _timeout=5)
        ).wait()
        return drohub_pb2.DroneReply(message=takeoff.success())

    @check_drone_connected
    def doLanding(self, request, context):
        landing = self.drone(Landing()).wait()
        return drohub_pb2.DroneReply(message=landing.success())

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
