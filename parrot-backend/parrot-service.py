#!/usr/bin/env python3
# -*- coding: UTF-8 -*-
from concurrent import futures
import logging
import time

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
            raise Exception("Drone is not connected")
    return wrapper

class DroneRPC(drohub_pb2_grpc.DroneServicer):
    def __init__(self):
        super().__init__()
        while True:
            self.drone = olympe.Drone("10.202.0.1", callbacks = [self.cb1])
            print("Connected")
            if self.drone.connection() == True:
                break

    def cb1(self, message):
        if message.Full_Name == "Ardrone3_PilotingState_PositionChanged":
            print(message.Full_Name, message.state())
            print()

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
