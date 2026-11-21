"""Simulated Tracker 650 MAVLink output for the SYSID 42 Sub-4.7.2 simulator.

Imitates the real DVL's path: VISION_POSITION_DELTA (and DISTANCE_SENSOR) straight
to ArduSub, which fuses them in its own EKF. No ROS estimator is involved.
No arming, mode, actuator or parameter-write commands.
"""
import math
import os
import time
os.environ['MAVLINK20'] = '1'
from pymavlink import mavutil
import rclpy
from rclpy.node import Node
from rclpy.executors import ExternalShutdownException
from rclpy.clock import Clock, ClockType
from rclpy.qos import qos_profile_sensor_data
from sensor_msgs.msg import Imu
from club_rov_interfaces.msg import Dvl
from .dvl_delta import DeltaIntegrator

REQUIRED = {'MAV_SYSID': 42, 'FRAME_CONFIG': 1, 'SIM_GPS1_ENABLE': 0,
            'VISO_TYPE': 1, 'EK3_SRC1_POSXY': 6, 'EK3_SRC1_VELXY': 6,
            'EK3_SRC1_POSZ': 1, 'EK3_SRC1_YAW': 1, 'RNGFND1_TYPE': 10}
ORIGIN = (-353632610, 1491652300)


class CeruleanDvl(Node):
    def __init__(self):
        super().__init__('cerulean_dvl')
        self.link = mavutil.mavlink_connection('udpin:127.0.0.1:14553',
            source_system=191, source_component=197, dialect='ardupilotmega')
        self.params = {}
        self.version = False
        self.armed = True
        self.heartbeat_at = self.last_request = self.last_heartbeat = self.last_origin = 0.0
        self.origin_seen = False
        self.fault = ''
        self.sent = 0
        self.reason = ''
        self.integrator = DeltaIntegrator()
        self.create_subscription(Imu, '/imu/data_raw', self.on_imu, qos_profile_sensor_data)
        self.create_subscription(Dvl, '/dvl/data', self.on_dvl, 10)
        clock = Clock(clock_type=ClockType.STEADY_TIME)
        self.create_timer(0.02, self.tick, clock=clock)
        self.get_logger().info('Simulated Tracker 650: loopback SYSID 42 only; no arming or mode commands')

    @staticmethod
    def seconds(header):
        return header.stamp.sec + header.stamp.nanosec*1e-9

    def ready(self):
        return not self.fault and self.version and self.origin_seen and \
            time.monotonic()-self.heartbeat_at < 3 and \
            all(self.params.get(k) == v for k, v in REQUIRED.items())

    def on_imu(self, message):
        v = message.angular_velocity
        self.integrator.add_gyro(self.seconds(message.header), (v.x, v.y, v.z))

    def on_dvl(self, message):
        stamp = self.seconds(message.header)
        v = message.velocity
        valid = message.valid and message.beam_count >= 3
        delta = self.integrator.add_velocity(stamp, (v.x, v.y, v.z), valid)
        if not self.ready():
            return
        if valid and message.altitude > 0:
            self.link.mav.distance_sensor_send(int(stamp*1000) & 0xFFFFFFFF, 50, 5000,
                int(round(message.altitude*100)), mavutil.mavlink.MAV_DISTANCE_SENSOR_ULTRASOUND,
                0, mavutil.mavlink.MAV_SENSOR_ROTATION_PITCH_270, 0)
        if delta is None:
            self.status('Waiting for consecutive DVL bottom-lock samples' if not valid else 'DVL lock reacquired')
            return
        dt, angle, position = delta
        self.link.mav.vision_position_delta_send(int(stamp*1e6), int(dt*1e6), angle, position, 100)
        self.sent += 1
        self.status('Sending VISION_POSITION_DELTA to ArduSub')

    def status(self, reason):
        if reason != self.reason:
            self.get_logger().info(f'{reason}; sent={self.sent}')
            self.reason = reason

    def receive(self, now):
        for _ in range(100):
            m = self.link.recv_match(blocking=False)
            if m is None:
                break
            if m.get_srcSystem() != 42 or m.get_srcComponent() != 1:
                continue
            kind = m.get_type()
            if kind == 'HEARTBEAT':
                if m.type != mavutil.mavlink.MAV_TYPE_SUBMARINE or m.autopilot != 3:
                    self.fault = 'Unexpected SYSID 42 vehicle'
                else:
                    self.heartbeat_at = now
                    self.armed = bool(m.base_mode & 128)
            elif kind == 'AUTOPILOT_VERSION':
                self.version = m.flight_sw_version >> 8 == 0x040702
            elif kind == 'PARAM_VALUE':
                self.params[m.param_id] = m.param_value
            elif kind == 'GPS_GLOBAL_ORIGIN':
                self.origin_seen = abs(m.latitude-ORIGIN[0]) < 10 and abs(m.longitude-ORIGIN[1]) < 10
                if not self.origin_seen:
                    self.fault = 'Unexpected simulator EKF origin; restart the DVL profile'

    def tick(self):
        now = time.monotonic()
        self.receive(now)
        if self.fault:
            self.status(self.fault)
            return
        if now-self.heartbeat_at > 3:
            self.status('No fresh SYSID 42 simulator heartbeat')
            return
        if now-self.last_heartbeat > 1:
            self.link.mav.heartbeat_send(18, 8, 0, 0, 4)
            self.last_heartbeat = now
        params_ok = all(self.params.get(k) == v for k, v in REQUIRED.items())
        if now-self.last_request > 3 and (not self.version or not params_ok):
            self.link.mav.command_long_send(42, 1, 512, 0, 148, 0, 0, 0, 0, 0, 0)
            for name in REQUIRED:
                self.link.mav.param_request_read_send(42, 1, name.encode(), -1)
            self.last_request = now
        if not self.version or not params_ok:
            self.status('Waiting for Sub-4.7.2 DVL profile parameter verification')
            return
        if not self.origin_seen:
            self.status('Waiting for EKF origin (Tracker auto-origin)')
            if not self.armed and now-self.last_origin > 2:
                self.link.mav.set_gps_global_origin_send(42, ORIGIN[0], ORIGIN[1], 0)
                self.link.mav.command_long_send(42, 1, 512, 0, 49, 0, 0, 0, 0, 0, 0)
                self.last_origin = now

    def destroy_node(self):
        self.link.close()
        return super().destroy_node()


def main(args=None):
    rclpy.init(args=args)
    node = None
    try:
        node = CeruleanDvl()
        rclpy.spin(node)
    except (KeyboardInterrupt, ExternalShutdownException):
        pass
    finally:
        if node:
            node.destroy_node()
        rclpy.try_shutdown()


if __name__ == '__main__':
    main()
