"""Optional, loopback-only external navigation for the SYSID 42 Sub-4.7.2 simulator.

No arming, mode, actuator or parameter-write commands. Restart the entire sensor
and estimator session after a vehicle reset; never splice missions together.
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
from nav_msgs.msg import Odometry
from diagnostic_msgs.msg import DiagnosticArray, DiagnosticStatus, KeyValue
from club_rov_interfaces.msg import Dvl
from .external_nav_frames import convert, pose_covariance


REQUIRED = {'MAV_SYSID': 42, 'FRAME_CONFIG': 1, 'SIM_GPS1_ENABLE': 0,
            'VISO_TYPE': 1, 'VISO_QUAL_MIN': 50, 'EK3_SRC1_POSXY': 6,
            'EK3_SRC1_VELXY': 6, 'EK3_SRC1_POSZ': 1, 'EK3_SRC1_VELZ': 0,
            'EK3_SRC1_YAW': 1}


class ExternalNav(Node):
    def __init__(self):
        super().__init__('sitl_external_nav')
        # Fixed endpoint and vehicle identity intentionally cannot target hardware.
        self.link = mavutil.mavlink_connection('udpin:127.0.0.1:14553',
            source_system=191, source_component=197, dialect='ardupilotmega')
        self.params = {}
        self.version = False
        self.boot_ms = -1
        self.stream_requested = False
        self.heartbeat_at = self.attitude_at = self.dvl_at = self.odom_at = 0.0
        self.last_request = self.last_heartbeat = self.last_origin = 0.0
        self.heading = None
        self.armed = True
        self.yaw = 0.0
        self.dvl_valid = False
        self.odom = None
        self.sent_stamp = -1
        self.last_stamp = -1
        self.fault = ''
        self.session = ''
        self.diagnostic_at = 0.0
        self.sensor_ok = False
        self.origin_seen = False
        self.sent = 0
        self.last_report = ''
        self.reason = 'Waiting for isolated SITL and healthy ROS sensors'
        self.publisher = self.create_publisher(DiagnosticArray, '/diagnostics', 10)
        self.create_subscription(Odometry, '/odometry/local', self.on_odom, 10)
        self.create_subscription(Dvl, '/dvl/data', self.on_dvl, 10)
        self.create_subscription(DiagnosticArray, '/diagnostics', self.on_diagnostics, 10)
        clock = Clock(clock_type=ClockType.STEADY_TIME)
        self.create_timer(0.02, self.tick, clock=clock)
        self.create_timer(1.0, self.report, clock=clock)
        self.get_logger().info('External navigation: loopback SYSID 42 only; no arming or mode commands')

    def on_odom(self, message):
        stamp = message.header.stamp.sec*1_000_000_000 + message.header.stamp.nanosec
        if stamp < self.last_stamp:
            self.fault = 'Estimator time reset; restart ROS and the simulation session'
        self.last_stamp = stamp
        self.odom, self.odom_at = message, time.monotonic()

    def on_dvl(self, message):
        self.dvl_valid = message.valid and message.beam_count >= 3
        self.dvl_at = time.monotonic()

    def on_diagnostics(self, message):
        for status in message.status:
            if status.name != 'club_rov/simulated_navigation_sensors':
                continue
            values = {v.key: v.value for v in status.values}
            session = values.get('session', '')
            if self.session and session and session != self.session:
                self.fault = 'Sensor session changed; restart ROS and SITL'
            if session:
                self.session = session
            self.sensor_ok = status.level == DiagnosticStatus.OK
            self.diagnostic_at = time.monotonic()

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
            elif kind == 'ATTITUDE':
                if m.time_boot_ms < self.boot_ms:
                    self.fault = 'SITL clock reset; restart the navigation session'
                self.boot_ms = m.time_boot_ms
                self.yaw, self.attitude_at = m.yaw, now
            elif kind == 'GPS_GLOBAL_ORIGIN':
                self.origin_seen = (abs(m.latitude + 353632610) < 10 and
                                    abs(m.longitude - 1491652300) < 10)
                if not self.origin_seen:
                    self.fault = 'Unexpected simulator EKF origin; restart fresh navigation profile'

    def tick(self):
        now = time.monotonic()
        self.receive(now)
        if now-self.heartbeat_at > 3 or self.fault:
            self.reason = self.fault or 'No fresh SYSID 42 simulator heartbeat'
            return
        if now-self.last_heartbeat > 1:
            self.link.mav.heartbeat_send(18, 8, 0, 0, 4)  # onboard controller, not a pilot/GCS heartbeat
            self.last_heartbeat = now
        if now-self.last_request > 3 and (not self.version or any(self.params.get(k) != v for k, v in REQUIRED.items())):
            self.link.mav.command_long_send(42, 1, 512, 0, 148, 0, 0, 0, 0, 0, 0)
            for name in REQUIRED:
                self.link.mav.param_request_read_send(42, 1, name.encode(), -1)
            self.link.mav.request_data_stream_send(42, 1, 10, 10, 1)  # attitude
            self.last_request = now
        if not self.version or any(self.params.get(k) != v for k, v in REQUIRED.items()):
            self.reason = 'Waiting for Sub-4.7.2 external-nav profile parameter verification'
            return
        if not self.stream_requested:
            self.link.mav.command_long_send(42, 1, 511, 0, 0, 100000, 0, 0, 0, 0, 0)
            self.stream_requested = True
        if not self.origin_seen:
            self.reason = 'Waiting for simulated EKF origin'
            if not self.armed and now-self.last_origin > 2:
                self.link.mav.set_gps_global_origin_send(42, -353632610, 1491652300, 0)
                self.link.mav.command_long_send(42, 1, 512, 0, 49, 0, 0, 0, 0, 0, 0)
                self.last_origin = now
            return
        if (not self.sensor_ok or now-self.diagnostic_at > 1.5 or
                not self.dvl_valid or now-self.dvl_at > 0.5 or now-self.odom_at > 0.5):
            self.reason = 'Odometry withheld: stale sensors or no DVL bottom lock'
            return
        m = self.odom
        if m is None or m.header.frame_id != 'odom' or m.child_frame_id != 'base_link':
            self.reason = 'Odometry withheld: expected odom -> base_link'
            return
        p, q, v, a = m.pose.pose.position, m.pose.pose.orientation, m.twist.twist.linear, m.twist.twist.angular
        if self.heading is None:
            if self.armed or now-self.attitude_at > 1 or math.sqrt(v.x*v.x+v.y*v.y+v.z*v.z) > 0.1:
                self.reason = 'Start navigation while disarmed and stationary to align launch heading'
                return
            ros_yaw = math.atan2(2*(q.w*q.z+q.x*q.y), 1-2*(q.y*q.y+q.z*q.z))
            self.heading = self.yaw + ros_yaw
            self.get_logger().info(f'Launch +X aligned at {math.degrees(self.heading):.1f} degrees NED')
        if self.last_stamp == self.sent_stamp:
            return
        try:
            position, attitude, velocity, rates = convert((p.x,p.y,p.z), (q.x,q.y,q.z,q.w),
                (v.x,v.y,v.z), (a.x,a.y,a.z), self.heading)
            covariance = pose_covariance(m.pose.covariance)
        except ValueError as error:
            self.reason = str(error)
            return
        self.link.mav.odometry_send(self.last_stamp//1000, 20, 12,
            *position, attitude, *velocity, *rates, covariance, [float('nan')]*21,
            0, 3, 100)
        self.sent_stamp = self.last_stamp
        self.sent += 1
        self.reason = 'Forwarding healthy DVL/IMU/depth odometry to SITL'

    def report(self):
        status = DiagnosticStatus(name='club_rov/sitl_external_nav', hardware_id='sysid42_simulation')
        status.level = DiagnosticStatus.OK if self.reason.startswith('Forwarding') else DiagnosticStatus.WARN
        status.message = self.reason
        status.values = [KeyValue(key='sent', value=str(self.sent)),
                         KeyValue(key='origin_confirmed', value=str(self.origin_seen))]
        message = DiagnosticArray(status=[status])
        message.header.stamp = self.get_clock().now().to_msg()
        self.publisher.publish(message)
        if self.reason != self.last_report:
            self.get_logger().info(f'{self.reason}; sent={self.sent}')
            self.last_report = self.reason

    def destroy_node(self):
        self.link.close()
        return super().destroy_node()


def main(args=None):
    rclpy.init(args=args)
    node = None
    try:
        node = ExternalNav()
        rclpy.spin(node)
    except (KeyboardInterrupt, ExternalShutdownException):
        pass
    finally:
        if node:
            node.destroy_node()
        rclpy.try_shutdown()


if __name__ == '__main__':
    main()
