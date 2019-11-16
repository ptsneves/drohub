#!/usr/bin/env python3
# -*- coding: UTF-8 -*-
import base64
import sys
import os
sys.path.append(os.path.dirname(os.path.realpath(__file__)) + '/../RPCInterfaces/python/')
sys.path.append(os.path.dirname('/usr/local/lib/python3.7/dist-packages/thrift'))

from concurrent import futures
from collections import deque
import logging
import time
import functools
import threading
import argparse
import subprocess
import signal
import io
import shutil
import urllib.request
import json
import socket
from contextlib import closing
from drohub import *
from drohub.ttypes import *
import struct
import random
from thrift.protocol.TProtocol import TProtocolException

import thrift
from thrift.transport import TTransport
from thrift.protocol import TBinaryProtocol, TJSONProtocol, TProtocolDecorator
from thrift.server import TServer
import websocket
import queue

import olympe
import olympe_deps

from olympe.messages.ardrone3.Piloting import TakeOff, moveBy, Landing, moveTo, NavigateHome
from olympe.messages.ardrone3.PilotingState import FlyingStateChanged, NavigateHomeStateChanged
from olympe.messages.common.SettingsState import ProductSerialHighChanged, ProductSerialLowChanged
from olympe.messages.common.CommonState import LinkSignalQuality
from olympe.enums.drone_manager import connection_state
from olympe.enums.ardrone3.Piloting import MoveTo_Orientation_mode
from olympe.messages.ardrone3.PilotingSettingsState import MaxTiltChanged
from olympe.messages.skyctrl.CoPiloting import setPilotingSource

from olympe.tools.logger import TraceLogger, DroneLogger, ErrorCodeDrone
_ONE_DAY_IN_SECONDS = 60 * 60 * 24

class ParrotSerialNumber():
    def __init__(self, expected_serial):
        self.serial = ""
        self._MSB = ""
        self._MSB_set = False
        self._LSB = ""
        self._LSB_set = False
        self._expected_serial = expected_serial

    def validateSerial(self):
        if self.serial != self._expected_serial:
            raise Exception("Retrieved serial and expected expected do not match.")

    def setMSB(self, msb):
        if self._MSB_set:
            if msb == self._MSB:
                return
            raise Exception("MSB is already set cannot set it again to a different value.")
        self._MSB = msb
        self._MSB_set = True
        if self._LSB_set:
            self.serial = msb + self._LSB
            

    def setLSB(self, lsb):
        if self._LSB_set:
            if lsb == self._LSB:
                return
            raise Exception("LSB is already set cannot set it again to a different value.")

        self._LSB = lsb
        self._LSB_set = True
        if self._MSB_set:
            self.serial = self._MSB + lsb

    def Get(self):
        if self._MSB_set and self._LSB_set:
            self.validateSerial()
            return self.serial
        else:
            self._expected_serial

class DroneMessageContainerBase():
    def __init__(self, drone_serial):
        self.container = deque(maxlen=2)
        self.lk = threading.Lock()
        self.cv_consumer = threading.Condition(self.lk)
        self.drone_serial = drone_serial

    def append(self, new_drone_position):
        self.container.append(new_drone_position)
        with self.cv_consumer:
            self.cv_consumer.notify_all()

    def getElement(self):
        with self.cv_consumer:
            self.cv_consumer.wait()
            container_copy = self.container
            return container_copy.popleft()

class DroneVideoContainer(DroneMessageContainerBase):
    Vp8_Command = "/usr/bin/ffmpeg -hwaccel vaapi   -vaapi_device /dev/dri/renderD128 -i rtsp://{source_url}/live -r 10 -c:v libvpx \
            -deadline realtime -threads 4 -speed -5 -skip_threshold 60 -vp8flags error_resilient -f rtp {rtp_url}"

    H264_Command = '/usr/bin/ffmpeg -r 10 -threads 4 -hwaccel vaapi -i rtsp://{source_url}/live -vf format=yuv420p \
            -vaapi_device /dev/dri/renderD128 -c:v h264_vaapi -profile:v constrained_baseline -level 3.0 -bf 0 -bsf: v \
            "dump_extra=freq=keyframe" -vf "format=nv12,hwupload" -f rtp {rtp_url}?pkt_size=1300"'

    def __init__(self, drone_serial):
        super().__init__(drone_serial)
        self._processes = {}
        if not self._doesCommandExist("ffmpeg"):
            raise Exception(
                "ffmpeg does not exist. And we need it to relay the video")

    def pollProcess(self, rtp_server_url, context):
        logging.debug("Starting polling process")
        context.add_callback(functools.partial(self._cleanProcess, rtp_server_url))
        while context.is_active():
            new_message = drohub_pb2.DroneVideoState(
                rtp_url=rtp_server_url,
                serial=self.drone_serial.Get(),
                timestamp=int(time.time()))

            if rtp_server_url not in self._processes.keys():
                new_message.state = drohub_pb2.DroneVideoState.State.INVALID_CONDITION
                new_message.human_message = "There is no record of sending video to this url {}".format(rtp_server_url)
                context.cancel()
            elif self._processes[rtp_server_url].poll() == None:
                new_message.state = drohub_pb2.DroneVideoState.State.DIED
                new_message.human_message = "Process is dead. stderr is:\n{}".format(io.TextIOWrapper(
                        self._processes[rtp_server_url].stderr, encoding='utf-8').readlines())
                context.cancel()
                self._cleanProcess(rtp_server_url)
            elif self._processes[rtp_server_url].poll():
                new_message.state = drohub_pb2.DroneVideoState.State.LIVE
                new_message.human_message = "Process for {} is living".format(rtp_server_url)
            else:
                new_message.state = drohub_pb2.DroneVideoState.State.INVALID_CONDITION
                new_message.human_message = "We do not really know what is going on. Its a bug"
                context.cancel()
                self._cleanProcess(rtp_server_url)

            logging.debug(new_message.human_message)
            yield new_message


    def sendVideoTo(self, drone_ip, rtp_server_url, video_type):
        try:
            if rtp_server_url in self._processes.keys():
                raise Exception("Request rtp server url is already in use. Choose another one")

            if video_type == drohub_pb2.DroneSendVideoRequest.VideoType.VP8:
                self._processes[rtp_server_url] = self.runProcess(
                    DroneVideoContainer.Vp8_Command.format(source_url=drone_ip, rtp_url=rtp_server_url))
                logging.debug("Sending VP8 to {}".format(rtp_server_url))
            elif video_type == drohub_pb2.DroneSendVideoRequest.VideoType.H264:
                self._processes[rtp_server_url] = self.runProcess(
                    DroneVideoContainer.H264_Command.format(source_url=drone_ip, rtp_url=rtp_server_url))
                logging.debug("Sending H264 to {}".format(rtp_server_url))
            else:
                raise Exception("Video Type requested is not recognized")
        except Exception as e:
            self._cleanProcess(rtp_server_url)
            raise e

    def _cleanProcess(self, rtp_url):
        if rtp_url in self._processes:
            logging.debug("killing video of process that pumps video to {}".format(rtp_url))
            os.killpg(os.getpgid(self._processes[rtp_url].pid), signal.SIGTERM)
            self._processes[rtp_url].kill()
            del self._processes[rtp_url]

    def _doesCommandExist(self, command):
        return shutil.which(command)

    def runProcess(self, command):
        process = subprocess.Popen(
            command, shell=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, preexec_fn=os.setsid)
        return process

class PositionContainer(DroneMessageContainerBase):
    def __init__(self, drone_serial):
        super().__init__(drone_serial)

    def dispatchPosition(self, message):

        if message.state()['latitude'] == 500.0 or message.state()['longitude'] == 500.0:
            logging.debug("Received invalid position due to lack of position lock (GPS etc)")
            #This means that we do not have a position lock. Lets not send this
            #as this parrot specific.
            return

        new_drone_position = DronePosition(
                        latitude = message.state()['latitude'],
                        longitude = message.state()['longitude'],
                        altitude = message.state()['altitude'],
                        serial = self.drone_serial.Get(),
                        timestamp = int(time.time()))

        logging.debug(new_drone_position)
        super().append(new_drone_position)

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
        new_drone_flying_state = DroneFlyingState(
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
        new_battery_level = DroneBatteryLevel(
            battery_level_percent=message.state()['percent'],
            serial = self.drone_serial.Get(),
            timestamp=int(time.time()))

        logging.debug(new_battery_level)
        super().append(new_battery_level)

class RadioSignalContainer(DroneMessageContainerBase):
    def __init__(self, drone_serial):
        super().__init__(drone_serial)

    def dispatchRadioRSSILevel(self, message):
        new_rssi_level = DroneRadioSignal(
            serial = self.drone_serial.Get(),
            timestamp=int(time.time()),
            rssi = message.state()['rssi']
        )
        logging.debug(new_rssi_level)
        super().append(new_rssi_level)

    def dispatchRadioSignalQuality(self, message):
        new_signal_quality = DroneRadioSignal(
            serial = self.drone_serial.Get(),
            timestamp=int(time.time()),
            signal_quality = message.state()['value']
        )
        logging.debug(new_signal_quality)
        super().append(new_signal_quality)

class FileListContainer(DroneMessageContainerBase):
    def __init__(self, drone_serial):
        super().__init__(drone_serial)

    def getFileList(self):
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



    def dispatchFileList(self, message):
        logging.debug()
        super().append(self.getFileList())

class DroneChooser(object):
    def __init__(self, drone_type):
        self._drone_args_dict = {}
        self._drone_args_dict["loglevel"] = TraceLogger.level.warning
        self._drone_type = drone_type
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

    def getDroneType(self):
        return self._drone_type

class DroneRAII(DroneChooser):
    def __init__(self, drone_type, *args, **kwargs):
        super().__init__(drone_type)
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

    def _checkSocket(self, host, port):
        with closing(socket.socket(socket.AF_INET, socket.SOCK_STREAM)) as sock:
            sock.settimeout(5)
            if sock.connect_ex((host, port)) == 0:
                return True
            else:
                return False

    def _checkDronePage(self):
        if self.getDroneType() == "simulator":
            return self._checkSocket(self._ip, 80)
        elif self.getDroneType() == "anafi":
            return self._checkSocket(self._ip, 180)

        raise Exception("Cannot check drone page for unknown drone type {}".format(self.getDroneType()))

    def checkDroneConnected(self):
        if not self._checkDronePage():
            logging.info("Drone is unreachable through the controller.")
            return False
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
            if self.checkDroneConnected() == False:
                try:
                   self._reconnectDrone()
                except Exception:
                    logging.warning("Reconnection failed. Trying again")

class DroneRPC(object):
    def __init__(self, drone_type, expected_serial):
        self.serial = ParrotSerialNumber(expected_serial)
        self.video_encoder = DroneVideoContainer(self.serial)
        self.position_container = PositionContainer(self.serial)
        self.battery_level_container = BatteryLevelContainer(self.serial)
        self.radio_signal_container = RadioSignalContainer(self.serial)
        self.flying_state_container = FlyingStateContainer(self.serial)
        self.file_list_container = FileListContainer(self.serial)
        self.drone = DronePersistentConnection(drone_type, callbacks = [self.cb1])
        super().__init__()

    def cb1(self, message):
        pass
       
        if message.Full_Name == "Common_SettingsState_ProductSerialHighChanged":
            self.serial.setMSB(message.state()['high'])
        elif message.Full_Name == "Common_SettingsState_ProductSerialLowChanged":
            self.serial.setLSB(message.state()['low'])
        elif message.Full_Name == "Ardrone3_PilotingState_PositionChanged":
            self.position_container.dispatchPosition(message)
        elif message.Full_Name == "Common_CommonState_BatteryStateChanged" or message.Full_Name == "Battery_Alert":
            self.battery_level_container.dispatchBatteryLevel(message)
        elif message.Full_Name == "Common_CommonState_LinkSignalQuality":
            self.radio_signal_container.dispatchRadioSignalQuality(message)
        elif message.Full_Name == "Wifi_Rssi_changed":
            self.radio_signal_container.dispatchRadioRSSILevel(message)
        elif message.Full_Name == "Ardrone3_PilotingState_FlyingStateChanged":
            self.flying_state_container.dispatchFlyingState(message)
        elif message.Full_Name == " Common_CommonState_MassStorageContent":
            self.file_list_container.dispatchFileList(message)

    def sendVideoTo(self):
        self.video_encoder.sendVideoTo(self.drone.getIP(), request.rtp_url, request.video_type)

    def getVideoState(self):
        elements = self.video_encoder.pollProcess(request.rtp_url)
        for element in elements:
            return element

    def getPosition(self):
        logging.warning("Get Position")
        d = self.position_container.getElement()
        logging.warning("Sent Position")
        return d

    def getBatteryLevel(self):
        return self.battery_level_container.getElement()

    def getRadioSignal(self):
        return self.radio_signal_container.getElement()

    def getFlyingState(self):
        return self.flying_state_container.getElement()

    def doTakeoff(self):
        logging.warning("Taking off")
        takeoff = self.drone.getDrone()(
            TakeOff()
            >> FlyingStateChanged(state="hovering", _timeout=5)
        ).wait()
        return DroneReply(
            serial = self.serial.Get(),
            timestamp=int(time.time()),
            result=takeoff.success())

    def doLanding(self):
        logging.warning("Landing")
        landing = self.drone.getDrone()(Landing()
            >> FlyingStateChanged(state="landed")
        ).wait()
        return DroneReply(
            serial = self.serial.Get(),
            timestamp = int(time.time()),
            result=landing.success())

    def doReturnToHome(self):
        logging.warning("Returning to Home")
        landing = self.drone.getDrone()(NavigateHome(start=1)
                                        >> NavigateHomeStateChanged(state='inProgress')
                                        ).wait()
        return DroneReply(
            serial=self.serial.Get(),
            timestamp=int(time.time()),
            result=landing.success())

    def moveToPosition(self):
        go_to_position = self.drone.getDrone()(moveTo(
            request.latitude, request.longitude, request.altitude,  MoveTo_Orientation_mode.HEADING_DURING, request.heading)
        ).wait()
        return DroneReply(
            serial = self.serial.Get(),
            timestamp = int(time.time()),
            result=go_to_position.success())

    def pingService(self):
        logging.debug("ping {}".format(self.serial.Get()))
        return DroneReply(
            serial = self.serial.Get(),
            timestamp=int(time.time()),
            result=self.drone.checkDroneConnected())

    def getFileList(self, request, context):
        self.file_list_container.getFileList()

    def getFileListStream(self, request, context):
        elements = self.file_list_container.getElement()
        for element in elements:
            return element


class TReverseTunnelServerFactory(object):
    """Factory transport that builds framed transports"""

    def __init__(self, transport):
        self.trans = transport
    def getTransport(self, trans):
        return self.trans


class TReverseTunnelServer(TTransport.TTransportBase):
    def __init__(self, transport, acceptable_clients):
        self._transport = transport
        self._connections = queue.Queue(10)

    def listen(self):
        pass

    def accept(self):
        self._connections.put(None)
        return TTransport.TBufferedTransport(self)

    def read(self, sz):
        return self._transport.read(sz)

    def write(self, buf):
        self._transport.write(buf)

    def close(self):
        self._transport.close()

    def flush(self):
        #We assume that once we flush we are done with a message
        self._connections.get()
        self._transport.flush()


class TWebSocketClient(TTransport.TTransportBase):
    def __init__(self, url, expected_serial):
        headers = [
            "User-Agent: AirborneProjets",
            "Content-Type: application/x-thrift",
            "x-device-expected-serial: {expected_serial}".format(expected_serial=expected_serial)]
        self.ws = websocket.create_connection(url, header=headers, enable_multithread=True, skip_utf8_validation=True)
        # websocket.enableTrace(True)
        self._close = False


    def read(self, sz):
        if self._close:
            raise TTransport.TTransportException(type=TTransport.TTransportException.END_OF_FILE,
                                      message='TSocket read 0 bytes')
        r = self.ws.recv()
        logging.debug("Read {}".format(r))
        return r

    def write(self, buf):
        if self._close:
            raise TTransport.TTransportException(type=TTransport.TTransportException.END_OF_FILE,
                                      message='TSocket sent 0 bytes')
        logging.debug("Write {}".format(buf))
        self.ws.send_binary(buf)

    def close(self):
        self._close = True
        self.ws.close(timeout=None)

    def flush(self):
        pass


class TLockedFramedTransportFactory(object):
    """Factory transport that builds framed transports"""

    def getTransport(self, trans):
        framed = TLockedFramedTransport(trans)
        return framed


class TLockedFramedTransport(TTransport.TFramedTransport):
    def __init__(self, trans):
        self.input_m = threading.Lock()
        self.output_m = threading.Lock()
        self.flush_m = threading.Lock()

        super().__init__(trans)

    def read(self, sz):
        self.input_m.acquire()
        try:
            return super().read(sz)
        finally:
            self.input_m.release()

    def write(self, buf):
        # print("Aquiring")
        self.output_m.acquire()
        try:
            super().write(buf)
        finally:
            # print("Releasing")
            self.output_m.release()

    def flush(self):
        self.flush_m.acquire()
        try:
            super().flush()
        finally:
            self.flush_m.release()


class TMessageValidatorProtocolFactory(object):
    """Factory transport that builds framed transports"""
    def __init__(self, protocol_factory, validation_mode, operation_mode):
        self.protocol_factory = protocol_factory
        self._validation_mode = validation_mode

    def getProtocol(self, trans):
        protocol = TMessageValidatorProtocol(self.protocol_factory.getProtocol(trans), self._validation_mode, self._validation_mode)
        return protocol


class TMessageValidatorProtocol(TProtocolDecorator.TProtocolDecorator):
    _MAGIC_NUMBER = 21474347
    class ValidationMode:
        KEEP_READING = 0
        THROW_EXCEPTION = 1
    class OperationMode:
        SEQID_SLAVE = 0
        SEQID_MASTER = 1
    def __init__(self, protocol, validation_mode, operation_mode):
        self._validation_mode = validation_mode
        self._operation_mode = operation_mode
        self.trans = protocol.trans
        self._rand_seq_id = random.randint(-2147483648, 2147483647)

    def _readMagicNumber(self):
        result = 0
        while True:
            result = result << 8 | ord(self.trans.read(1))
            if result == TMessageValidatorProtocol._MAGIC_NUMBER:
                break

    def writeMagicNumber(self):
        buff = struct.pack("!i", TMessageValidatorProtocol._MAGIC_NUMBER)
        self.trans.write(buff)

    def writeMessageBegin(self, name, m_type, seqid):
        if self._operation_mode == TMessageValidatorProtocol.OperationMode.SEQID_MASTER:
            self._rand_seq_id = self._rand_seq_id + 1
            seqid = self._rand_seq_id
        elif self._operation_mode == TMessageValidatorProtocol.OperationMode.SEQID_SLAVE:
            pass
        else:
            raise TProtocolException(
                type=TProtocolException.NOT_IMPLEMENTED, message='Invalid operation mode selected')
        self.writeMagicNumber()
        super(TMessageValidatorProtocol, self).writeMessageBegin(name, m_type, seqid)

    def readMessageBegin(self):
        self._readMagicNumber()
        (name, msg_type, seqid) = super(TMessageValidatorProtocol, self).readMessageBegin()
        if self._operation_mode == TMessageValidatorProtocol.OperationMode.SEQID_MASTER:
            while self._rand_seq_id != seqid:
                if self._validation_mode == TMessageValidatorProtocol.ValidationMode.KEEP_READING:
                    self._readMagicNumber()
                    (name, msg_type, seqid) = super(TMessageValidatorProtocol, self).readMessageBegin()
                elif self._validation_mode == TMessageValidatorProtocol.ValidationMode.THROW_EXCEPTION:
                    raise TProtocolException(type=TProtocolException.BAD_VERSION, message='Received an unexpected seq id')

        return (name, msg_type, seqid)

def serve(drone_type, drohub_url, expected_serial):
    handler = DroneRPC(drone_type, expected_serial)
    transport = None
    while True:
        try:
            processor = Drone.Processor(handler)
            transport = TWebSocketClient(drohub_url, expected_serial)
            transport = TReverseTunnelServer(transport, 10)
            tfactory = TTransport.TFramedTransportFactory()
            pfactory = TMessageValidatorProtocolFactory(TJSONProtocol.TJSONProtocolFactory(),
                TMessageValidatorProtocol.ValidationMode.KEEP_READING, TMessageValidatorProtocol.OperationMode.SEQID_SLAVE)

            server = TServer.TThreadPoolServer(processor, transport, tfactory, pfactory)
            server.serve()
        # except (ConnectionRefusedError, TTransport.TTransportException) as _:
        #     time.sleep(5)
        #     logging.error("Failed to connect to drohub. Retrying")
        #     pass
        except KeyboardInterrupt as _:
            try:
                transport.close()
            except _:
                pass
            break
        # except (Exception, SystemExit) as _:
        #     if transport:
        #         try:
        #             transport.close()
        #         except _:
        #             pass
        #         time.sleep(5)
        #         continue


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Parrot ANAFI service.')

    parser.add_argument('--simulator', dest='drone_type', action='store_const',
                        const='simulator', default='anafi',
                        help='Connect to a simulator. (Default connect to real ANAFI')
    parser.add_argument('--verbose', dest="verbosity", action="store_const",
                        const=logging.DEBUG, default = logging.INFO,
                        help="Whether to print debugging information")
    parser.add_argument('serial', nargs=1, type=str, help='The expected serial number of the drone')
    parser.add_argument('url', nargs=1, type=str, help='The websocket url for the drohub server')
    args = parser.parse_args()
    logging.basicConfig(
        level=args.verbosity, format='%(asctime)-15s %(levelname)s  %(threadName)s %(message)s')
    serve(args.drone_type, args.url[0], args.serial[0])
