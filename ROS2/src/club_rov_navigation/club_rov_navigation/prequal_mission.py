"""Explicitly invoked RoboSub prequalification run: loopback SYSID 42 SITL, or the physical
vehicle with --hardware (typed confirmation, failsafe/battery prechecks, depth and stick limits).

Uses only sensors the real vehicle carries: pressure depth, autopilot heading and the
forward camera. ArduSub stays in ALT_HOLD and receives joystick-style MANUAL_CONTROL.
"""
import argparse
import json
import math
import os
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
os.environ['MAVLINK20'] = '1'
import cv2
import numpy as np
from pymavlink import mavutil
import rclpy
from rclpy.node import Node
from rclpy.executors import ExternalShutdownException
from sensor_msgs.msg import CameraInfo, Image
from std_msgs.msg import Empty, String
from .calibrated_view import detector_input, open_view
from .marker_detector import GATE_COLOUR, PI_CAMERA_V2_FLAT_PORT_HFOV, MarkerColour, detect_gate, detect_marker
from .mission_safety import REQUESTED, hardware_problems, limit
from .prequal_plan import Config, Prequal


GUARD = {'MAV_SYSID': 42, 'FRAME_CONFIG': 1, 'SIM_GPS1_ENABLE': 0}
ALT_HOLD, MANUAL = 2, 19
SURFACE_START = .3


class PrequalMission(Node):
    def __init__(self, args):
        super().__init__('prequal_mission')
        self.args = args
        self.hardware = args.hardware
        self.sysid = args.sysid if self.hardware else 42
        self.plan = Prequal(Config(depth=args.depth, cruise=args.cruise, orbit_radius=args.radius,
                                   return_scale=args.return_scale))
        self.colour = MarkerColour(hue_low=args.hue[0], hue_high=args.hue[1])
        self.gate_colour = MarkerColour(args.gate_hue[0], args.gate_hue[1], GATE_COLOUR.saturation_min,
                                        GATE_COLOUR.value_min, GATE_COLOUR.min_pixels)
        self.link = mavutil.mavlink_connection(args.mavlink, source_system=255, source_component=190)
        self.link.target_system, self.link.target_component = self.sysid, 1
        self.params, self.version = {}, False
        self.firmware_major, self.voltage = None, None
        self.heartbeat_at = self.pressure_at = self.attitude_at = self.image_at = 0.0
        self.mode, self.armed = -1, False
        self.depth = self.heading = 0.0
        self.boot_seconds = None
        self.fx = self.cx = None
        self.marker = self.gate = None
        self.marker_at = self.gate_at = self.frame_saved_at = 0.0
        self.phase, self.reason = 'PRECHECK', f'Waiting for disarmed SYSID {self.sysid}, depth, heading and camera'
        self.phase_at = self.last_sample = 0.0
        self.stable_at = None
        self.created_at = self.last_request = self.last_heartbeat = self.last_status = time.monotonic()
        self.mode_requested_at, self.last_mode_request = 0.0, -1
        self.owned = self.done = self.finishing = False
        self.history, self.events = [], []
        self.outcome = ''
        self.video, self.video_at, self.camera_error = None, 0.0, ''
        self.view = open_view(args.calibration, require_water=True)
        if args.video:
            from .prequal_record import VideoSource
            self.video = VideoSource(args.video)
        else:
            self.create_subscription(CameraInfo, '/mapping/rgb/camera_info', self.on_info, 5)
            self.create_subscription(Image, '/mapping/rgb/image', self.on_image, 5)
        self.publisher = self.create_publisher(String, '/mission/status', 10)
        self.create_subscription(Empty, '/mission/cancel', lambda _: self.abort('Cancellation requested'), 10)
        self.create_timer(.1, self.tick)
        target = f'PHYSICAL VEHICLE SYSID {self.sysid} via {args.mavlink}' if self.hardware else 'loopback SITL only'
        self.get_logger().info(f'Prequal mission waiting ({target}). QGC joystick must be DISABLED.')

    def on_info(self, m):
        if m.k[0] > 0:
            self.fx, self.cx = m.k[0], m.k[2]

    def on_image(self, m):
        if self.fx is None or m.encoding != 'rgb8' or m.step != m.width*3:
            return
        self.process(np.frombuffer(m.data, np.uint8).reshape(m.height, m.width, 3), self.fx, self.cx)

    def poll_video(self):
        frame, frame_at = self.video.latest()
        if frame is None or frame_at <= self.video_at:
            return
        self.video_at = frame_at
        try:
            self.process(*detector_input(frame, self.view, hfov=self.args.hfov), frame)
        except ValueError as error:
            self.camera_error = f'{error} (video {frame.shape[1]}x{frame.shape[0]})'

    def process(self, rgb, fx, cx, full=None):
        self.image_at = time.monotonic()
        marker = detect_marker(rgb, fx, cx, self.colour)
        if marker is not None:
            self.marker, self.marker_at = marker, self.image_at
        gate = detect_gate(rgb, fx, cx, self.gate_colour)
        if gate is not None:
            self.gate, self.gate_at = gate, self.image_at
        if self.args.save_frames and self.owned and not self.done and self.image_at-self.frame_saved_at >= 1:
            folder = Path(self.args.save_frames)
            folder.mkdir(parents=True, exist_ok=True)
            name = f'{self.image_at-self.created_at:06.1f}-{self.phase}-m{int(marker is not None)}g{int(gate is not None)}.png'
            cv2.imwrite(str(folder/name), cv2.cvtColor(rgb if full is None else full, cv2.COLOR_RGB2BGR))
            self.frame_saved_at = self.image_at

    def receive(self):
        now = time.monotonic()
        for _ in range(150):
            m = self.link.recv_match(blocking=False)
            if m is None:
                break
            if m.get_srcSystem() != self.sysid or m.get_srcComponent() != 1:
                continue
            kind = m.get_type()
            if kind == 'HEARTBEAT' and m.type == 12 and m.autopilot == 3:
                self.heartbeat_at = now
                self.mode, self.armed = m.custom_mode, bool(m.base_mode & 128)
            elif kind == 'PARAM_VALUE':
                self.params[m.param_id] = m.param_value
            elif kind == 'AUTOPILOT_VERSION':
                self.version = m.flight_sw_version >> 8 == 0x040702
                self.firmware_major = m.flight_sw_version >> 24 & 0xff
            elif kind == 'ATTITUDE' and math.isfinite(m.yaw):
                self.heading, self.boot_seconds, self.attitude_at = m.yaw, m.time_boot_ms/1000, now
            elif kind == 'SYS_STATUS' and m.voltage_battery not in (0, 65535):
                self.voltage = m.voltage_battery/1000
            elif self.hardware and kind == 'VFR_HUD' and math.isfinite(m.alt):
                # ArduSub reports depth below the surface as negative altitude, from its calibrated sensor.
                self.depth, self.pressure_at = max(0, -m.alt), now
            elif not self.hardware and kind == 'SCALED_PRESSURE' and math.isfinite(m.press_abs):
                # Pinned SITL water model: atmospheric 101325 Pa, rho=1000 kg/m3.
                self.depth = max(0, (m.press_abs-1013.25)*100/(1000*9.80665))
                self.pressure_at = now

    def problems(self):
        if not self.hardware:
            return [] if self.version and all(self.params.get(k) == v for k, v in GUARD.items()) else \
                ['expected loopback Sub-4.7.2 SITL with SYSID 42']
        problems = hardware_problems(self.params, self.firmware_major, self.voltage, self.args.min_voltage)
        if not self.owned and self.depth > SURFACE_START:
            problems.append(f'start at the surface (depth {self.depth:.2f} m > {SURFACE_START} m)')
        return problems

    def guarded(self):
        """Identity during the run; the full precheck (battery etc.) only gates the start."""
        fresh = time.monotonic()-self.heartbeat_at < 3
        if self.hardware:
            return fresh and self.firmware_major == 4
        return fresh and not self.problems()

    def stale_sensors(self):
        now = time.monotonic()
        return [name for name, at, limit in (('depth', self.pressure_at, 1), ('heading', self.attitude_at, 1),
                                             ('camera', self.image_at, 1.5)) if now-at >= limit]

    def set_mode(self, mode):
        now = time.monotonic()
        if mode != self.last_mode_request or (self.mode != mode and now-self.mode_requested_at > 1):
            self.link.mav.set_mode_send(self.sysid, 1, mode)
            self.last_mode_request, self.mode_requested_at = mode, now

    def neutral(self):
        self.link.mav.manual_control_send(self.sysid, 0, 0, 500, 0, 0)

    def transition(self, phase, reason):
        self.phase, self.phase_at, self.stable_at, self.reason = phase, time.monotonic(), None, reason
        self.events.append(dict(phase=phase, autopilot_time=self.boot_seconds, reason=reason))
        self.get_logger().info('PREQUAL '+phase+': '+reason)
        self.status()

    def status(self):
        marker = self.marker if time.monotonic()-self.marker_at < .5 else None
        text = json.dumps(dict(phase=self.phase, reason=self.reason, depth=round(self.depth, 2),
            heading_deg=round(math.degrees(self.heading), 1), armed=self.armed, battery_v=self.voltage,
            marker_bearing_deg=None if marker is None else round(math.degrees(marker.bearing), 1),
            marker_distance_m=None if marker is None else round(marker.distance(self.plan.config.marker_diameter), 2),
            circled_deg=round(math.degrees(abs(self.plan.circled))), outcome=self.outcome), separators=(',', ':'))
        self.publisher.publish(String(data=text))
        self.last_status = time.monotonic()

    def abort(self, reason):
        if not self.owned or not self.armed:
            self.finish('failed_precheck' if not self.owned else 'aborted', reason)
        elif self.phase in self.plan_phases():
            self.plan.abort(reason, self.boot_seconds)
        else:
            self.emergency(reason)

    @staticmethod
    def plan_phases():
        return ('DIVE', 'OUTBOUND', 'ORBIT', 'ALIGN', 'TURN', 'RETURN', 'SURFACE')

    def emergency(self, reason):
        """Climb blind with full up stick in depth hold when the mission's own inputs can't be trusted."""
        if not self.armed:
            self.finish('aborted', reason)
        elif self.phase not in ('ABORT_SURFACE', 'DISARM'):
            self.transition('ABORT_SURFACE', reason)

    def finish(self, outcome, reason):
        if self.finishing:
            return
        self.outcome, self.reason, self.finishing = outcome, reason, True
        if self.owned:
            self.neutral()
            self.link.arducopter_disarm()
            self.transition('DISARM', reason)
        else:
            self.save_report()

    def save_report(self):
        folder = Path(os.environ.get('CLUB_ROV_ROS_WS', str(Path.home()/'club-rov-ros2-ws')))/'reports'
        folder.mkdir(parents=True, exist_ok=True)
        path = folder/('prequal-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'.json')
        path.write_text(json.dumps(dict(outcome=self.outcome, reason=self.reason, disarmed=not self.armed,
            hardware=self.hardware, settings=vars(self.args), circled_deg=math.degrees(abs(self.plan.circled)),
            events=self.events, samples=self.history), indent=2))
        self.done = True
        self.status()
        self.get_logger().info(f'PREQUAL RESULT {self.outcome}; disarmed={not self.armed}; {path}')

    def tick(self):
        self.receive()
        if self.video:
            self.poll_video()
        now = time.monotonic()
        if now-self.last_status > 1:
            self.status()
        if self.done:
            return
        if self.heartbeat_at and now-self.last_request > 3 and self.problems():
            self.link.mav.command_long_send(self.sysid,1,512,0,148,0,0,0,0,0,0)
            self.link.mav.request_data_stream_send(self.sysid,1,0,10,1)
            for key in (REQUESTED if self.hardware else GUARD):
                self.link.mav.param_request_read_send(self.sysid,1,key.encode(),-1)
            self.last_request = now
        if self.phase == 'DISARM':
            if not self.armed:
                self.set_mode(MANUAL)
                self.save_report()
            elif now-self.phase_at > 3 or now-self.heartbeat_at > 3:
                self.outcome = 'disarm_unconfirmed'
                self.save_report()
            else:
                self.link.arducopter_disarm()
            return
        if not self.guarded():
            if self.owned:
                self.finish('aborted', 'Vehicle identity or heartbeat lost')
            elif now-self.created_at > 90:
                self.finish('failed_precheck', '; '.join(self.problems()) or 'No heartbeat')
            return
        if now-self.last_heartbeat > 1:
            self.link.mav.heartbeat_send(6,8,0,0,4)
            self.last_heartbeat = now
        if self.owned and now-self.last_sample > .5:
            marker = self.marker if now-self.marker_at < .3 else None
            gate = self.gate if now-self.gate_at < .3 else None
            self.history.append(dict(autopilot_time=self.boot_seconds, phase=self.phase, depth=self.depth,
                heading=self.heading, battery_v=self.voltage,
                marker=None if marker is None else [marker.bearing, marker.angular_width],
                gate=None if gate is None else [gate.bearing, gate.angular_width]))
            self.last_sample = now
        if self.phase == 'PRECHECK':
            stale = self.stale_sensors()
            problems = self.problems() + ([f'no fresh {", ".join(stale)}'] if stale else [])
            if 'camera' in stale and self.camera_error:
                problems.append(self.camera_error)
            if problems:
                self.reason = 'Not ready: '+'; '.join(problems)
            if now-self.created_at > 90:
                self.finish('failed_precheck', self.reason if problems else 'Vehicle armed before the mission started')
            elif not problems and not self.armed:
                self.owned = True
                self.transition('MODE_WAIT', 'Sensors ready; request depth hold')
                self.set_mode(ALT_HOLD)
            return
        if self.phase in ('MODE_WAIT', 'ARM_WAIT'):
            self.neutral()
        if self.phase == 'ABORT_SURFACE':
            self.link.mav.manual_control_send(self.sysid, 0, 0, 1000, 0, 0)
            self.set_mode(ALT_HOLD)
            if now-self.pressure_at < 2 and self.depth < .18:
                self.stable_at = self.stable_at or now
                if now-self.stable_at > 1:
                    self.finish('aborted_surfaced', self.reason)
            else:
                self.stable_at = None
            if now-self.phase_at > 40:
                self.finish('aborted', 'Surface recovery timed out: '+self.reason)
            return
        if now-self.pressure_at > 2 or now-self.attitude_at > 2:
            self.emergency('Depth or heading telemetry lost')
            return
        if self.armed and self.depth > self.args.max_depth:
            self.emergency(f'Depth {self.depth:.2f} m exceeded the {self.args.max_depth:.2f} m limit')
            return
        if self.phase == 'MODE_WAIT':
            self.set_mode(ALT_HOLD)
            if self.mode == ALT_HOLD:
                self.link.arducopter_arm()
                self.transition('ARM_WAIT', 'Depth hold accepted; arming')
            elif now-self.phase_at > 5:
                self.finish('failed', 'Depth hold mode rejected')
            return
        if self.phase == 'ARM_WAIT':
            if self.armed:
                self.transition(self.plan.phase, self.plan.reason)
            elif now-self.phase_at > 5:
                self.finish('failed', 'Arming rejected')
            return
        if not self.armed:
            self.finish('aborted', 'Vehicle disarmed during mission')
            return
        if self.mode != ALT_HOLD and now-self.mode_requested_at > 2:
            self.finish('aborted', 'Operator changed mode; mission relinquished control')
            return
        # Depth and heading hold keep the vehicle safe through a short camera dropout.
        if now-self.image_at > 3:
            self.plan.abort('Camera stream lost', self.boot_seconds)
        if now-self.created_at > self.args.time_limit:
            self.plan.abort('Mission time limit', self.boot_seconds)
        if self.hardware and self.voltage is not None and self.voltage < self.args.abort_voltage:
            self.plan.abort(f'Battery {self.voltage:.2f} V', self.boot_seconds)
        marker = self.marker if now-self.marker_at < .3 else None
        gate = self.gate if now-self.gate_at < .3 else None
        c = self.plan.step(self.boot_seconds, self.depth, self.heading, marker, gate)
        if self.plan.phase != self.phase:
            if self.plan.phase == 'DONE':
                self.finish(self.plan.outcome, self.plan.reason)
                return
            self.transition(self.plan.phase, self.plan.reason)
        s = self.args.max_stick
        self.link.mav.manual_control_send(self.sysid, int(1000*limit(c.surge, s)), int(1000*limit(c.sway, s)),
                                          int(500+500*c.heave), int(1000*limit(c.yaw, s)), 0)

    def destroy_node(self):
        if self.owned and not self.done:
            self.neutral()
            self.link.arducopter_disarm()
        if self.video:
            self.video.close()
        self.link.close()
        return super().destroy_node()


def confirm_hardware(options):
    print(f'\nPHYSICAL VEHICLE RUN: SYSID {options.sysid} via {options.mavlink}, video {options.video}')
    print(f'Camera: {"calibration " + options.calibration if options.calibration else f"UNCALIBRATED estimate, {options.hfov} degree view"}')
    print(f'Target depth {options.depth} m, depth limit {options.max_depth} m, time limit {options.time_limit:g} s, '
          f'stick limit {options.max_stick}, battery start >= {options.min_voltage} V, abort < {options.abort_voltage} V.')
    print('Spotter at the pool, tether tended, QGC joystick DISABLED, QGC open to disarm.')
    print('Ctrl+C at any time stops the thrusters and disarms.')
    return input('Type DIVE to continue: ').strip() == 'DIVE'


def main(args=None):
    parser = argparse.ArgumentParser(description=__doc__)
    target = parser.add_mutually_exclusive_group(required=True)
    target.add_argument('--confirm-simulation', action='store_true')
    target.add_argument('--hardware', action='store_true', help='physical vehicle; needs --sysid, --mavlink and --video')
    parser.add_argument('--sysid', type=int, help='physical vehicle MAVLink system id')
    parser.add_argument('--mavlink', help='MAVLink endpoint, e.g. a MAVProxy/router output (simulation: udpin:127.0.0.1:14554)')
    parser.add_argument('--video', help='OpenCV video source instead of the simulator topic (GStreamer pipeline, file, ...)')
    parser.add_argument('--hfov', type=float, default=PI_CAMERA_V2_FLAT_PORT_HFOV, help='underwater horizontal field of view, degrees')
    parser.add_argument('--calibration', help='underwater calibrate_camera output (YAML/JSON); replaces --hfov and removes lens distortion')
    parser.add_argument('--depth', type=float, default=1.6)
    parser.add_argument('--max-depth', type=float, help='surface immediately below this depth (default: depth + 0.6 m)')
    parser.add_argument('--cruise', type=float, default=.5, help='forward stick, 0-1')
    parser.add_argument('--max-stick', type=float, help='cap on surge/sway/yaw stick (default 1.0 simulation, 0.6 hardware)')
    parser.add_argument('--radius', type=float, default=1.2, help='marker circle radius, metres')
    parser.add_argument('--return-scale', type=float, default=1.2, help='return time / outbound time')
    parser.add_argument('--hue', type=int, nargs=2, default=(0, 15), help='OpenCV marker hue range, 0-180')
    parser.add_argument('--gate-hue', type=int, nargs=2, default=(GATE_COLOUR.hue_low, GATE_COLOUR.hue_high),
                        help='OpenCV gate hue range, 0-180')
    parser.add_argument('--time-limit', type=float, help='seconds (default 480 simulation, 300 hardware)')
    parser.add_argument('--min-voltage', type=float, default=14.0, help='hardware: battery needed to start (4S)')
    parser.add_argument('--abort-voltage', type=float, default=13.2, help='hardware: surface below this battery voltage')
    parser.add_argument('--save-frames', metavar='DIR', help='save one camera frame per second while armed')
    options = parser.parse_args(args)
    if options.hardware and None in (options.sysid, options.mavlink, options.video):
        parser.error('--hardware needs explicit --sysid, --mavlink and --video')
    if not options.hardware:
        if options.mavlink not in (None, 'udpin:127.0.0.1:14554') or options.sysid not in (None, 42):
            parser.error('Simulation runs only use loopback SYSID 42')
        options.mavlink = 'udpin:127.0.0.1:14554'
    if options.calibration and not options.video:
        parser.error('--calibration applies to --video frames (the simulator camera is already an ideal pinhole)')
    try:
        open_view(options.calibration, require_water=True)
    except (OSError, ValueError) as error:
        parser.error(str(error))
    options.max_depth = options.depth+.6 if options.max_depth is None else options.max_depth
    options.max_stick = (.6 if options.hardware else 1.0) if options.max_stick is None else options.max_stick
    options.time_limit = (300 if options.hardware else 480) if options.time_limit is None else options.time_limit
    if not options.depth < options.max_depth or not 0 < options.max_stick <= 1:
        parser.error('--max-depth must exceed --depth and --max-stick must be in (0, 1]')
    Config(depth=options.depth, cruise=options.cruise, orbit_radius=options.radius, return_scale=options.return_scale)
    if options.hardware and not confirm_hardware(options):
        print('Not confirmed; nothing was sent.')
        sys.exit(1)
    rclpy.init()
    node = None
    try:
        node = PrequalMission(options)
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
