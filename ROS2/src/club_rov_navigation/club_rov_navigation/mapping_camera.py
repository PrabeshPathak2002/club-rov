"""Bounded local RGB-D bridge, synchronized to the existing Unity sensor session."""
import math
import socket
from threading import Event, Lock, Thread

from geometry_msgs.msg import TransformStamped
from sensor_msgs.msg import CameraInfo, Image

WIDTH, HEIGHT = 320, 180
PLANE_BYTES = WIDTH * HEIGHT * 4
FRAME = 'mapping_camera_optical'


def read_frame(connection):
    with connection.makefile('rb') as stream:
        fields = stream.readline(256).decode('ascii').split()
        if len(fields) != 6 or fields[0] != 'RGBD1':
            raise ValueError('invalid RGB-D header')
        _, session, stamp, fx, fy, flip = fields
        stamp, fx, fy = int(stamp), float(fx), float(fy)
        if (len(session) != 32 or any(c not in '0123456789abcdef' for c in session)
                or not 0 < stamp < 2**31 * 10**9 or flip not in ('0', '1')
                or not all(math.isfinite(v) and 10 <= v <= 2000 for v in (fx, fy))):
            raise ValueError('invalid RGB-D metadata')
        rgba, depth = stream.read(PLANE_BYTES), stream.read(PLANE_BYTES)
        if len(rgba) != PLANE_BYTES or len(depth) != PLANE_BYTES:
            raise ValueError('truncated RGB-D frame')
    if flip == '1':
        stride = WIDTH * 4
        rgba, depth = (b''.join(data[i:i + stride] for i in range(len(data) - stride, -1, -stride))
                       for data in (rgba, depth))
    rgb = bytearray(WIDTH * HEIGHT * 3)
    for channel in range(3):
        rgb[channel::3] = rgba[channel::4]
    return session, stamp, fx, fy, rgb, depth


class MappingCamera:
    def __init__(self, node):
        self.node = node
        self.rgb = node.create_publisher(Image, '/mapping/rgb/image', 5)
        self.depth = node.create_publisher(Image, '/mapping/depth/image', 5)
        self.info = node.create_publisher(CameraInfo, '/mapping/rgb/camera_info', 5)
        self.server = socket.socket()
        self.server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.server.bind(('127.0.0.1', 9013))
        self.server.listen(1)
        self.server.settimeout(0.25)
        self.lock, self.stop = Lock(), Event()
        self.latest = None
        self.last_stamp = -1
        self.thread = Thread(target=self.receive, daemon=True)
        self.thread.start()
        self.timer = node.create_timer(0.02, self.publish, clock=node.wall_clock)
        node.get_logger().info('RGB-D mapping listening on loopback TCP 9013')

    @staticmethod
    def mount():
        transform = TransformStamped()
        transform.header.frame_id = 'base_link'
        transform.child_frame_id = FRAME
        transform.transform.translation.x = 0.31
        transform.transform.translation.z = 0.04
        q = transform.transform.rotation
        q.x, q.y, q.z, q.w = -0.5, 0.5, -0.5, 0.5
        return transform

    def receive(self):
        while not self.stop.is_set():
            try:
                connection, _ = self.server.accept()
                with connection:
                    connection.settimeout(0.25)
                    frame = read_frame(connection)
                with self.lock:
                    self.latest = frame
            except (OSError, ValueError):
                continue

    def publish(self):
        with self.lock:
            frame = self.latest
        if frame is None:
            return
        session, stamp, fx, fy, rgb, depth = frame
        guard = self.node.guard
        odom = self.node.map_odom
        if session != guard.session or not guard.fresh() or stamp <= self.last_stamp or odom is None:
            return
        odom_stamp = odom.header.stamp.sec * 10**9 + odom.header.stamp.nanosec
        if not stamp <= odom_stamp <= stamp + 500_000_000:
            return
        self.last_stamp = stamp
        info = CameraInfo(width=WIDTH, height=HEIGHT, distortion_model='plumb_bob',
                          d=[0.0] * 5, r=[1., 0., 0., 0., 1., 0., 0., 0., 1.])
        cx, cy = (WIDTH - 1) / 2, (HEIGHT - 1) / 2
        info.k = [fx, 0., cx, 0., fy, cy, 0., 0., 1.]
        info.p = [fx, 0., cx, 0., 0., fy, cy, 0., 0., 0., 1., 0.]
        info.header.frame_id = FRAME
        info.header.stamp.sec, info.header.stamp.nanosec = divmod(stamp, 10**9)
        self.info.publish(info)
        self.rgb.publish(Image(header=info.header, height=HEIGHT, width=WIDTH,
                               encoding='rgb8', step=WIDTH * 3, data=rgb))
        self.depth.publish(Image(header=info.header, height=HEIGHT, width=WIDTH,
                                 encoding='32FC1', step=WIDTH * 4, data=depth))

    def close(self):
        self.stop.set()
        self.server.close()
        self.thread.join(timeout=0.6)
