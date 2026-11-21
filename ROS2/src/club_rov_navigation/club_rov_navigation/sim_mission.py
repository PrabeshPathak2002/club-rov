"""Explicitly invoked autonomous test for loopback SYSID 42 SITL only.

Horizontal goals use ROS odometry, never Unity truth. Depth uses simulated
water pressure. No mission starts automatically with the sensor launch.
"""
import argparse
import json
import math
import os
import sys
from pathlib import Path
import time
from datetime import datetime, timezone
os.environ['MAVLINK20'] = '1'
from pymavlink import mavutil
import rclpy
from rclpy.node import Node
from rclpy.parameter import Parameter
from rclpy.clock import Clock, ClockType
from rclpy.executors import ExternalShutdownException
from nav_msgs.msg import Odometry
from std_msgs.msg import String, Empty
from diagnostic_msgs.msg import DiagnosticArray, DiagnosticStatus
from club_rov_interfaces.msg import Dvl
from .external_nav_frames import pose_covariance
from .mission_plan import command, goals, reached


GUARD = {'MAV_SYSID': 42, 'FRAME_CONFIG': 1, 'SIM_GPS1_ENABLE': 0,
         'VISO_TYPE': 1, 'EK3_SRC1_POSXY': 6, 'EK3_SRC1_VELXY': 6}


class SimMission(Node):
    def __init__(self, args):
        super().__init__('sim_mission', parameter_overrides=[Parameter('use_sim_time', value=True)])
        self.args = args
        self.link = mavutil.mavlink_connection('udpin:127.0.0.1:14554', source_system=255, source_component=190)
        self.params = {}
        self.version = False
        self.heartbeat_at = self.pressure_at = self.odom_at = self.dvl_at = self.nav_at = 0.0
        self.mode, self.armed, self.ekf_flags = -1, False, 0
        self.depth = 0.0
        self.odom = None
        self.dvl_valid = self.nav_forwarding = False
        self.session = None
        self.session_fault = False
        self.last_stamp = -1
        self.clock_fault = False
        self.phase = 'PRECHECK'
        self.phase_at = self.recovery_at = self.last_sample = 0.0
        self.stable_at = None
        self.created_at = self.last_request = self.last_heartbeat = self.last_status = time.monotonic()
        self.mode_requested_at = 0.0
        self.last_mode_request = -1
        self.owned = self.done = self.finishing = False
        self.plan, self.index, self.initial = [], 0, None
        self.resume_phase = None
        self.history, self.events = [], []
        self.outcome = ''
        self.reason = 'Waiting for disarmed SYSID 42, fresh DVL/odometry and EKF position'
        self.report_path = None
        self.publisher = self.create_publisher(String, '/mission/status', 10)
        self.create_subscription(Odometry, '/odometry/local', self.on_odom, 10)
        self.create_subscription(Dvl, '/dvl/data', self.on_dvl, 10)
        self.create_subscription(DiagnosticArray, '/diagnostics', self.on_diagnostics, 10)
        self.create_subscription(Empty, '/mission/cancel', lambda _: self.abort('Cancellation requested'), 10)
        self.create_timer(.1, self.tick, clock=Clock(clock_type=ClockType.STEADY_TIME))
        self.get_logger().info('SITL mission waiting. QGC joystick must be DISABLED; no hardware endpoints accepted.')

    def sim_time(self):
        return self.get_clock().now().nanoseconds/1e9

    def on_odom(self, m):
        stamp = m.header.stamp.sec*1_000_000_000+m.header.stamp.nanosec
        if self.last_stamp >= 0 and stamp < self.last_stamp:
            self.clock_fault = True
        self.last_stamp = stamp
        self.odom, self.odom_at = m, time.monotonic()

    def on_dvl(self, m):
        self.dvl_valid, self.dvl_at = m.valid and m.beam_count >= 3, time.monotonic()

    def on_diagnostics(self, m):
        for s in m.status:
            if s.name == 'club_rov/sitl_external_nav':
                self.nav_forwarding = s.level == DiagnosticStatus.OK
                self.nav_at = time.monotonic()
            if s.name == 'club_rov/simulated_navigation_sensors':
                session = next((v.value for v in s.values if v.key == 'session'), '')
                if self.session and session and session != self.session:
                    self.session_fault = True
                if session:
                    self.session = session

    def receive(self):
        now = time.monotonic()
        for _ in range(150):
            m = self.link.recv_match(blocking=False)
            if m is None:
                break
            if m.get_srcSystem() != 42 or m.get_srcComponent() != 1:
                continue
            kind = m.get_type()
            if kind == 'HEARTBEAT' and m.type == 12 and m.autopilot == 3:
                self.heartbeat_at = now
                self.mode, self.armed = m.custom_mode, bool(m.base_mode & 128)
            elif kind == 'PARAM_VALUE':
                self.params[m.param_id] = m.param_value
            elif kind == 'AUTOPILOT_VERSION':
                self.version = m.flight_sw_version >> 8 == 0x040702
            elif kind == 'EKF_STATUS_REPORT':
                self.ekf_flags = m.flags
            elif kind == 'SCALED_PRESSURE' and math.isfinite(m.press_abs):
                # Pinned SITL water model: atmospheric 101325 Pa, rho=1000 kg/m3.
                self.depth = max(0, (m.press_abs-1013.25)*100/(1000*9.80665))
                self.pressure_at = now

    def guarded(self):
        return (self.version and time.monotonic()-self.heartbeat_at < 3 and
                all(self.params.get(k) == v for k, v in GUARD.items()))

    def healthy(self):
        now = time.monotonic()
        if (self.odom is None or now-self.odom_at > .5 or now-self.dvl_at > .5 or not self.dvl_valid or
                now-self.nav_at > 1.5 or not self.nav_forwarding or not self.ekf_flags & 16 or self.ekf_flags & 128):
            return False
        if self.odom.header.frame_id != 'odom' or self.odom.child_frame_id != 'base_link':
            return False
        p, q = self.odom.pose.pose.position, self.odom.pose.pose.orientation
        velocity = self.odom.twist.twist.linear
        if not all(math.isfinite(v) for v in (p.x,p.y,p.z,q.x,q.y,q.z,q.w,velocity.x,velocity.y,velocity.z)):
            return False
        try:
            pose_covariance(self.odom.pose.covariance)
        except ValueError:
            return False
        return not self.clock_fault and not self.session_fault

    def state(self):
        p, q, v = self.odom.pose.pose.position, self.odom.pose.pose.orientation, self.odom.twist.twist.linear
        yaw = math.atan2(2*(q.w*q.z+q.x*q.y), 1-2*(q.y*q.y+q.z*q.z))
        return p.x, p.y, yaw, self.depth, math.hypot(v.x,v.y)

    def set_mode(self, mode):
        now = time.monotonic()
        if mode != self.last_mode_request or (self.mode != mode and now-self.mode_requested_at > 1):
            self.link.mav.set_mode_send(42, 1, mode)
            self.last_mode_request, self.mode_requested_at = mode, now

    def velocity(self, values=(0,0,0,0)):
        # Ignore position, acceleration and yaw angle; use body velocity + yaw rate.
        self.link.mav.set_position_target_local_ned_send(int(self.sim_time()*1000) & 0xffffffff,
            42, 1, 12, 1479, 0,0,0, values[0],values[1],values[2], 0,0,0, 0,values[3])

    def transition(self, phase, reason):
        self.phase, self.phase_at, self.stable_at = phase, self.sim_time(), None
        self.reason = reason
        self.events.append(dict(phase=phase, sim_time=self.phase_at, reason=reason))
        self.get_logger().info('MISSION '+phase+': '+reason)
        self.status()

    def status(self):
        text = json.dumps(dict(phase=self.phase, reason=self.reason, sim_time=self.sim_time(),
            depth=self.depth, armed=self.armed, navigation_valid=self.healthy()), separators=(',', ':'))
        self.publisher.publish(String(data=text))
        self.last_status = time.monotonic()

    def abort(self, reason):
        if not self.owned or not self.armed:
            self.finish('failed_precheck', reason)
        elif self.phase not in ('ABORT_SURFACE', 'DISARM'):
            self.velocity()
            self.transition('ABORT_SURFACE', reason)
            self.set_mode(9)

    def finish(self, outcome, reason):
        if self.finishing:
            return
        self.outcome, self.reason, self.finishing = outcome, reason, True
        if self.owned:
            self.velocity()
            self.link.arducopter_disarm()
            self.transition('DISARM', reason)
        else:
            self.save_report()

    def save_report(self):
        folder = Path(os.environ.get('CLUB_ROV_ROS_WS', str(Path.home()/'club-rov-ros2-ws')))/'reports'
        folder.mkdir(parents=True, exist_ok=True)
        self.report_path = folder/('mission-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'.json')
        self.report_path.write_text(json.dumps(dict(outcome=self.outcome, reason=self.reason,
            disarmed=not self.armed, distance_m=self.args.distance, target_depth_m=self.args.depth,
            initial=self.initial, final=self.state() if self.odom else None,
            events=self.events, samples=self.history), indent=2))
        self.done = True
        self.get_logger().info(f'MISSION RESULT {self.outcome}; disarmed={not self.armed}; {self.report_path}')

    def tick(self):
        self.receive()
        now, sim = time.monotonic(), self.sim_time()
        if now-self.last_status > 1:
            self.status()
        if self.done:
            return
        if self.heartbeat_at and now-self.last_request > 3 and not self.guarded():
            self.link.mav.command_long_send(42,1,512,0,148,0,0,0,0,0,0)
            self.link.mav.request_data_stream_send(42,1,0,10,1)
            self.link.mav.command_long_send(42,1,511,0,29,100000,0,0,0,0,0)
            for key in GUARD:
                self.link.mav.param_request_read_send(42,1,key.encode(),-1)
            self.last_request = now
        if self.phase == 'DISARM':
            if not self.armed:
                self.set_mode(19)
                self.save_report()
            elif sim-self.phase_at > 3 or now-self.heartbeat_at > 3:
                self.outcome = 'disarm_unconfirmed'
                self.save_report()
            else:
                self.link.arducopter_disarm()
            return
        if not self.guarded():
            if self.owned:
                self.finish('aborted', 'Simulator identity or heartbeat lost')
            elif now-self.created_at > 90:
                self.finish('failed_precheck', 'Expected isolated Sub-4.7.2 external-navigation session')
            return
        if now-self.last_heartbeat > 1:
            self.link.mav.heartbeat_send(6,8,0,0,4)
            self.last_heartbeat = now
        if self.owned:
            self.link.mav.manual_control_send(42,0,0,500,0,0)
            if sim-self.last_sample > .5 and self.odom:
                self.history.append(dict(sim_time=sim, phase=self.phase, state=self.state(), navigation_valid=self.healthy()))
                self.last_sample = sim
        if self.phase == 'PRECHECK':
            if now-self.created_at > 90:
                self.finish('failed_precheck', 'Navigation not ready; restart the entire simulation after outages')
            elif not self.armed and self.healthy() and now-self.pressure_at < 2.0:
                x,y,yaw,depth,speed = self.state()
                if speed > .08:
                    return
                self.initial = self.state()
                self.plan = goals(x,y,yaw,self.args.distance,self.args.depth)
                self.owned = True
                self.transition('GUIDED_WAIT', 'Navigation healthy; request Guided mode')
                self.set_mode(4)
            return
        if now-self.pressure_at > 2.0 or self.clock_fault or self.session_fault:
            self.finish('aborted', 'Depth, simulation clock or sensor session invalid')
            return
        if self.phase == 'GUIDED_WAIT':
            if not self.healthy():
                self.finish('failed', 'Navigation lost before arming')
                return
            self.set_mode(4)
            if self.mode == 4:
                self.link.arducopter_arm()
                self.transition('ARM_WAIT', 'Guided accepted; arming simulation')
            elif sim-self.phase_at > 5:
                self.finish('failed', 'Guided mode rejected')
            return
        if self.phase == 'ARM_WAIT':
            if self.armed:
                self.transition(self.plan[0][0], 'Dive to mission depth')
            elif sim-self.phase_at > 5:
                self.finish('failed', 'Arming rejected')
            return
        if self.phase == 'ABORT_SURFACE':
            self.set_mode(9)
            if self.depth < .18:
                if self.stable_at is None:
                    self.stable_at = sim
                elif sim-self.stable_at > 1:
                    self.finish('aborted_surfaced', self.reason)
            else:
                self.stable_at = None
            if sim-self.phase_at > 30:
                self.finish('aborted', 'Surface recovery timed out: '+self.reason)
            return
        if self.phase == 'NAV_HOLD':
            if sim-self.recovery_at > 2:
                self.abort('DVL/navigation loss exceeded 2 simulation seconds')
            elif self.healthy():
                if self.stable_at is None:
                    self.stable_at = sim
                elif sim-self.stable_at > .5:
                    self.set_mode(4)
                    if self.mode == 4:
                        self.transition(self.resume_phase, 'Navigation recovered; resume original goal')
            else:
                self.stable_at = None
                self.set_mode(2)
            return
        if not self.armed:
            self.finish('aborted', 'Vehicle disarmed during mission')
            return
        if not self.healthy():
            self.resume_phase, self.recovery_at = self.phase, sim
            self.velocity()
            self.transition('NAV_HOLD', 'Navigation invalid: stop horizontal mission and hold depth')
            self.set_mode(2)
            return
        if self.mode != 4 and now-self.mode_requested_at > 2:
            self.finish('aborted', 'Operator changed mode; mission relinquished control')
            return
        x,y,yaw,depth,speed = self.state()
        if depth > 2.6 or math.hypot(x-self.initial[0],y-self.initial[1]) > self.args.distance+2:
            self.abort('Simulation depth/geofence limit exceeded')
            return
        if sim-self.phase_at > 60 or now-self.created_at > 600:
            self.abort('Mission progress timeout')
            return
        target = self.plan[self.index][1]
        self.velocity(command(x,y,yaw,depth,target))
        if reached(x,y,yaw,depth,speed,target):
            if self.stable_at is None:
                self.stable_at = sim
            elif sim-self.stable_at > 1.5:
                self.index += 1
                if self.index == len(self.plan):
                    self.finish('completed', 'Returned to launch XY and surfaced')
                else:
                    self.transition(self.plan[self.index][0], 'Proceed to next mission goal')
        else:
            self.stable_at = None

    def destroy_node(self):
        if self.owned and not self.done:
            self.velocity()
            self.link.arducopter_disarm()
        self.link.close()
        return super().destroy_node()


def main(args=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--confirm-simulation', action='store_true', required=True)
    parser.add_argument('--distance', type=float, default=4.0)
    parser.add_argument('--depth', type=float, default=1.8)
    options = parser.parse_args(args)
    goals(0,0,0,options.distance,options.depth)
    rclpy.init()
    node = None
    try:
        node = SimMission(options)
        while rclpy.ok() and not node.done:
            rclpy.spin_once(node, timeout_sec=.1)
    except (KeyboardInterrupt, ExternalShutdownException):
        pass
    finally:
        if node:
            node.destroy_node()
        rclpy.try_shutdown()
    if node is None or node.outcome != 'completed':
        sys.exit(1)
