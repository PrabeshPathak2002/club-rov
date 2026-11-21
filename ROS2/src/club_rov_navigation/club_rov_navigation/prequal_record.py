"""Record camera footage with depth, heading and detections for tuning the prequal detectors.

Read-only: it only listens to MAVLink and never sends anything, so it cannot arm, change modes
or move the vehicle. Drive the ROV by hand (QGC joystick) while it records.

Output folder: frames/NNNNNN.jpg plus log.jsonl, one line per saved frame. Review a recording
with `prequal_review <folder>`.
"""
import argparse
import json
import math
import os
import sys
import threading
import time
from datetime import datetime, timezone
from pathlib import Path
os.environ['MAVLINK20'] = '1'
import cv2
import numpy as np
from .calibrated_view import detector_input, open_view
from .marker_detector import (GATE_COLOUR, PI_CAMERA_V2_FLAT_PORT_HFOV, MarkerColour, detect_gate, detect_marker,
                              focal_length)


class Telemetry:
    """Latest vehicle state from a MAVLink stream; never transmits."""

    def __init__(self, url, sysid=None):
        from pymavlink import mavutil
        self.link = mavutil.mavlink_connection(url, source_system=255, source_component=191)
        self.sysid = sysid
        self.depth = self.heading = self.pressure = self.pressure_ext = None
        self.armed, self.mode, self.heartbeat_at = None, None, 0.0
        self.depth_at = self.heading_at = 0.0

    def receive(self):
        now = time.monotonic()
        for _ in range(200):
            m = self.link.recv_match(blocking=False)
            if m is None:
                return
            if self.sysid is not None and m.get_srcSystem() != self.sysid:
                continue
            kind = m.get_type()
            if kind == 'HEARTBEAT' and m.autopilot == 3:
                self.armed, self.mode, self.heartbeat_at = bool(m.base_mode & 128), m.custom_mode, now
            elif kind == 'ATTITUDE' and math.isfinite(m.yaw):
                self.heading, self.heading_at = m.yaw, now
            elif kind == 'VFR_HUD' and math.isfinite(m.alt):
                # ArduSub reports depth as negative altitude from its calibrated pressure sensor.
                self.depth, self.depth_at = -m.alt, now
            elif kind == 'SCALED_PRESSURE':
                self.pressure = m.press_abs
            elif kind == 'SCALED_PRESSURE2':
                self.pressure_ext = m.press_abs

    def fresh(self, value_at):
        return time.monotonic()-value_at < 2

    def summary(self):
        return dict(depth_m=self.depth if self.fresh(self.depth_at) else None,
                    heading_deg=math.degrees(self.heading) if self.fresh(self.heading_at) else None,
                    pressure_hpa=self.pressure, pressure_ext_hpa=self.pressure_ext,
                    armed=self.armed, mode=self.mode, heartbeat=self.fresh(self.heartbeat_at))


class VideoSource:
    """Background reader for an OpenCV source (device index, file, RTSP or GStreamer pipeline)."""

    def __init__(self, source):
        backend = cv2.CAP_GSTREAMER if '!' in source else cv2.CAP_ANY
        self.capture = cv2.VideoCapture(int(source) if source.isdigit() else source, backend)
        if not self.capture.isOpened():
            raise RuntimeError(f'Could not open video source {source!r}')
        # Files decode faster than real time; pace them at their own frame rate.
        fps = self.capture.get(cv2.CAP_PROP_FPS) if '!' not in source and '://' not in source and Path(source).is_file() else 0
        self.period = 1/fps if fps and fps > 0 else 0
        self.frame, self.frame_at, self.running = None, 0.0, True
        self.lock = threading.Lock()
        threading.Thread(target=self.read, daemon=True).start()

    def read(self):
        while self.running:
            ok, bgr = self.capture.read()
            if not ok:
                time.sleep(.05)
                continue
            with self.lock:
                self.frame, self.frame_at = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB), time.monotonic()
            if self.period:
                time.sleep(self.period)

    def latest(self):
        with self.lock:
            return self.frame, self.frame_at

    def close(self):
        self.running = False
        self.capture.release()


class TopicSource:
    """Latest frame and intrinsics from ROS 2 Image/CameraInfo topics."""

    def __init__(self, image_topic, info_topic):
        import rclpy
        from sensor_msgs.msg import CameraInfo, Image
        rclpy.init()
        self.rclpy = rclpy
        self.node = rclpy.create_node('prequal_record')
        self.frame, self.frame_at, self.fx, self.cx = None, 0.0, None, None
        self.node.create_subscription(Image, image_topic, self.on_image, 5)
        self.node.create_subscription(CameraInfo, info_topic, self.on_info, 5)

    def on_info(self, m):
        if m.k[0] > 0:
            self.fx, self.cx = m.k[0], m.k[2]

    def on_image(self, m):
        if m.encoding not in ('rgb8', 'bgr8') or m.step != m.width*3:
            return
        image = np.frombuffer(m.data, np.uint8).reshape(m.height, m.width, 3)
        self.frame = image if m.encoding == 'rgb8' else cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
        self.frame_at = time.monotonic()

    def latest(self):
        self.rclpy.spin_once(self.node, timeout_sec=0)
        return self.frame, self.frame_at

    def close(self):
        self.node.destroy_node()
        self.rclpy.try_shutdown()


def main(args=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--video', help='OpenCV source instead of ROS: device index, file, rtsp://... or a GStreamer pipeline')
    parser.add_argument('--image-topic', default='/mapping/rgb/image')
    parser.add_argument('--info-topic', default='/mapping/rgb/camera_info')
    parser.add_argument('--hfov', type=float, default=PI_CAMERA_V2_FLAT_PORT_HFOV,
                        help='horizontal field of view in degrees, used when no camera_info is available '
                             '(default: Pi Camera V2.1 behind a flat port, underwater)')
    parser.add_argument('--mavlink', default='udpin:127.0.0.1:14554',
                        help="MAVLink stream to listen to, or 'none' to record camera only")
    parser.add_argument('--sysid', type=int, help='only use this MAVLink system id')
    parser.add_argument('--rate', type=float, default=2.0, help='frames saved per second')
    parser.add_argument('--duration', type=float, help='stop after this many seconds')
    parser.add_argument('--out', help='output folder (default: reports/recording-<UTC time> in the ROS workspace)')
    parser.add_argument('--hue', type=int, nargs=2, default=(0, 15), help='marker hue range, OpenCV 0-180')
    parser.add_argument('--gate-hue', type=int, nargs=2, default=(GATE_COLOUR.hue_low, GATE_COLOUR.hue_high))
    parser.add_argument('--calibration', help='calibrate_camera output; replaces --hfov and removes lens distortion '
                                              'for live detections (saved frames stay raw)')
    options = parser.parse_args(args)
    if not options.rate > 0:
        parser.error('--rate must be positive')
    focal_length(320, options.hfov)
    try:
        view = open_view(options.calibration)
    except (OSError, ValueError) as error:
        parser.error(str(error))

    workspace = Path(os.environ.get('CLUB_ROV_ROS_WS', str(Path.home()/'club-rov-ros2-ws')))
    folder = Path(options.out) if options.out else \
        workspace/'reports'/('recording-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ'))
    (folder/'frames').mkdir(parents=True, exist_ok=True)
    marker_colour = MarkerColour(hue_low=options.hue[0], hue_high=options.hue[1])
    gate_colour = MarkerColour(options.gate_hue[0], options.gate_hue[1], GATE_COLOUR.saturation_min,
                               GATE_COLOUR.value_min, GATE_COLOUR.min_pixels)
    (folder/'settings.json').write_text(json.dumps(vars(options), indent=2))

    telemetry = None if options.mavlink == 'none' else Telemetry(options.mavlink, options.sysid)
    source = VideoSource(options.video) if options.video else TopicSource(options.image_topic, options.info_topic)
    print(f'Recording to {folder} (read-only; Ctrl+C to stop).', flush=True)
    started = last_saved = last_status = time.monotonic()
    count, last_frame_at = 0, 0.0
    try:
        with open(folder/'log.jsonl', 'a') as log:
            while options.duration is None or time.monotonic()-started < options.duration:
                if telemetry:
                    telemetry.receive()
                frame, frame_at = source.latest()
                now = time.monotonic()
                if frame is not None and frame_at != last_frame_at and now-last_saved >= 1/options.rate:
                    if view:
                        fx, cx = view.full_frame(frame)
                    else:
                        fx = getattr(source, 'fx', None) or focal_length(frame.shape[1], options.hfov)
                        cx = getattr(source, 'cx', None)
                    small, small_fx, small_cx = detector_input(frame, view, fx, cx)
                    marker = detect_marker(small, small_fx, small_cx, marker_colour)
                    gate = detect_gate(small, small_fx, small_cx, gate_colour)
                    name = f'{count:06d}.jpg'
                    cv2.imwrite(str(folder/'frames'/name), cv2.cvtColor(frame, cv2.COLOR_RGB2BGR),
                                [cv2.IMWRITE_JPEG_QUALITY, 92])
                    entry = dict(t=round(now-started, 3), file=name, width=frame.shape[1], fx=fx, cx=cx,
                                 marker=None if marker is None else [marker.bearing, marker.angular_width],
                                 gate=None if gate is None else [gate.bearing, gate.angular_width])
                    if telemetry:
                        entry.update(telemetry.summary())
                    log.write(json.dumps(entry)+'\n')
                    log.flush()
                    count, last_saved, last_frame_at = count+1, now, frame_at
                if now-last_status >= 5:
                    state = telemetry.summary() if telemetry else {}
                    depth, heading = state.get('depth_m'), state.get('heading_deg')
                    print(f'{count} frames | camera {"ok" if now-frame_at < 2 else "WAITING"} | '
                          f'depth {"-" if depth is None else f"{depth:.2f} m"} | '
                          f'heading {"-" if heading is None else f"{heading:.0f}°"}', flush=True)
                    last_status = now
                time.sleep(.01)
    except KeyboardInterrupt:
        pass
    finally:
        source.close()
    print(f'Saved {count} frames to {folder}', flush=True)
    sys.exit(0 if count else 1)
