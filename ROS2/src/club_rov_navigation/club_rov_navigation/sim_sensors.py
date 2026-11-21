"""Publish Unity measurements. This node has no MAVLink command or motor interface."""
import math
import json
import time
import rclpy
from rclpy.node import Node
from rclpy.executors import ExternalShutdownException
from rclpy.clock import Clock, ClockType
from rclpy.qos import qos_profile_sensor_data
from geometry_msgs.msg import PoseWithCovarianceStamped, TwistWithCovarianceStamped, TransformStamped
from sensor_msgs.msg import Imu
from nav_msgs.msg import Odometry
from rosgraph_msgs.msg import Clock as ClockMessage
from diagnostic_msgs.msg import DiagnosticArray, DiagnosticStatus, KeyValue
from tf2_ros import StaticTransformBroadcaster
from club_rov_interfaces.msg import Dvl
from .protocol import decode, compensate_lever_arm, SequenceGuard, DEFAULT_OFFSET
from .transport import Receiver
from .external_nav_frames import pose_covariance
from .mapping_camera import MappingCamera


def assign_vector(target, values):
    target.x, target.y, target.z = values


def diagonal(size, variance):
    values = [0.0]*(size*size)
    for i in range(size):
        values[i*size+i] = float(variance)
    return values


class SimSensors(Node):
    def __init__(self):
        super().__init__('sim_sensors')
        self.declare_parameter('tcp_port', 9011)
        self.declare_parameter('publish_ground_truth', False)
        self.declare_parameter('with_mapping', False)
        self.declare_parameter('dvl_offset', list(DEFAULT_OFFSET))
        self.offset = list(self.get_parameter('dvl_offset').value)
        if len(self.offset) != 3 or not all(math.isfinite(x) for x in self.offset):
            raise ValueError('dvl_offset must contain three finite metres')
        self.publish_truth = self.get_parameter('publish_ground_truth').value
        self.receiver = Receiver(self.get_parameter('tcp_port').value)
        self.guard = SequenceGuard()
        self.last_problem = 'Waiting for Unity measurements'
        self.invalid_packets = 0
        self.last_dvl_valid = False
        self.last_dvl_at = None
        self.map_odom = None
        self.map_odom_at = 0.0
        self.clock_pub = self.create_publisher(ClockMessage, '/clock', 10)
        self.imu_pub = self.create_publisher(Imu, '/imu/data_raw', qos_profile_sensor_data)
        self.depth_pub = self.create_publisher(PoseWithCovarianceStamped, '/depth/pose', 10)
        self.dvl_pub = self.create_publisher(Dvl, '/dvl/data', 10)
        self.velocity_pub = self.create_publisher(TwistWithCovarianceStamped, '/dvl/velocity_body', 10)
        self.diagnostics_pub = self.create_publisher(DiagnosticArray, '/diagnostics', 10)
        self.truth_pub = self.create_publisher(Odometry, '/simulation/ground_truth/odometry', 10) if self.publish_truth else None
        self.create_subscription(Odometry, '/odometry/local', self.on_map_odometry, 10)
        self.tf = StaticTransformBroadcaster(self)
        self.wall_clock = Clock(clock_type=ClockType.STEADY_TIME)
        self.mapping = MappingCamera(self) if self.get_parameter('with_mapping').value else None
        self.publish_mounts()
        # Steady time is essential: /clock does not exist before the first sensor packet.
        wall_clock = self.wall_clock
        self.poll_timer = self.create_timer(0.005, self.poll, clock=wall_clock)
        self.status_timer = self.create_timer(0.5, self.publish_status, clock=wall_clock)
        self.map_timer = self.create_timer(0.1, self.return_map, clock=wall_clock)
        self.get_logger().info('Listening on loopback TCP 9011; simulated sensors only; no vehicle commands')

    def publish_mounts(self):
        transforms = []
        for frame, position in [('dvl_link', self.offset), ('imu_link', [0.0, 0.0, 0.0])]:
            t = TransformStamped()
            t.header.frame_id = 'base_link'
            t.child_frame_id = frame
            assign_vector(t.transform.translation, position)
            t.transform.rotation.w = 1.0
            transforms.append(t)
        if self.mapping is not None:
            transforms.append(self.mapping.mount())
        self.tf.sendTransform(transforms)

    def on_map_odometry(self, message):
        self.map_odom, self.map_odom_at = message, time.monotonic()

    def return_map(self):
        m = self.map_odom
        if m is None or not self.guard.session:
            return
        now = time.monotonic()
        p, q = m.pose.pose.position, m.pose.pose.orientation
        if not all(math.isfinite(v) for v in (p.x, p.y, q.x, q.y, q.z, q.w)):
            return
        valid = (self.guard.fresh() and now-self.map_odom_at < 0.5 and
                 self.last_dvl_at is not None and now-self.last_dvl_at < 0.5 and self.last_dvl_valid)
        reason = 'live' if valid else 'DVL lost or sensors stale'
        try:
            pose_covariance(m.pose.covariance)
        except ValueError:
            valid, reason = False, 'Position uncertainty too high'
        if m.header.frame_id != 'odom' or m.child_frame_id != 'base_link':
            valid, reason = False, 'Unexpected odometry frame'
        packet = dict(schema=1, kind='dvl_track', session=self.guard.session,
            stamp_ns=m.header.stamp.sec*1_000_000_000+m.header.stamp.nanosec,
            x=p.x, y=p.y, yaw=math.atan2(2*(q.w*q.z+q.x*q.y), 1-2*(q.y*q.y+q.z*q.z)),
            valid=bool(valid), reason=reason)
        self.receiver.send_latest(json.dumps(packet, allow_nan=False, separators=(',', ':')).encode())

    def poll(self):
        for raw in self.receiver.poll():
            try:
                p = decode(raw, self.offset)
                self.guard.accept(p)
            except (ValueError, TypeError, UnicodeError, OverflowError) as error:
                self.invalid_packets += 1
                self.last_problem = str(error)
                continue
            self.publish_measurement(p)

    def publish_measurement(self, p):
        clock = ClockMessage()
        clock.clock.sec, clock.clock.nanosec = divmod(p['stamp_ns'], 1_000_000_000)
        stamp = clock.clock
        self.clock_pub.publish(clock)
        imu = Imu()
        imu.header.stamp = stamp
        imu.header.frame_id = 'imu_link'
        imu.orientation_covariance[0] = -1.0  # Raw IMU does not know attitude.
        assign_vector(imu.angular_velocity, p['gyro'])
        assign_vector(imu.linear_acceleration, p['accel'])
        imu.angular_velocity_covariance = diagonal(3, p['gyro_variance'])
        imu.linear_acceleration_covariance = diagonal(3, p['accel_variance'])
        self.imu_pub.publish(imu)
        depth = PoseWithCovarianceStamped()
        depth.header.stamp = stamp
        depth.header.frame_id = 'odom'
        depth.pose.pose.position.z = -p['depth']
        depth.pose.pose.orientation.w = 1.0
        depth.pose.covariance = diagonal(6, 1e6)
        depth.pose.covariance[14] = p['depth_variance']
        self.depth_pub.publish(depth)
        if p['has_dvl']:
            self.last_dvl_valid = p['valid']
            self.last_dvl_at = time.monotonic()
            self.last_problem = p['reason']
            dvl = Dvl()
            dvl.header.stamp = stamp
            dvl.header.frame_id = 'dvl_link'
            assign_vector(dvl.velocity, p['velocity'])
            dvl.velocity_covariance = diagonal(3, p['velocity_variance'])
            dvl.valid = p['valid']
            dvl.beam_count = p['beams']
            dvl.altitude = p['altitude']
            dvl.status = p['reason']
            self.dvl_pub.publish(dvl)
            # Invalid DVL reports are not published to the EKF velocity input.
            if p['valid']:
                twist = TwistWithCovarianceStamped()
                twist.header.stamp = stamp
                twist.header.frame_id = 'base_link'
                assign_vector(twist.twist.twist.linear, compensate_lever_arm(p['velocity'], p['gyro'], self.offset))
                twist.twist.covariance = diagonal(6, 1e6)
                # Conservative isotropic bound for uncertainty in omega cross r.
                variance = p['velocity_variance'] + p['gyro_variance']*sum(x*x for x in self.offset)
                for index in (0, 7, 14):
                    twist.twist.covariance[index] = variance
                self.velocity_pub.publish(twist)
        if self.truth_pub and p.get('truth'):
            truth = p['truth']
            message = Odometry()
            message.header.stamp = stamp
            message.header.frame_id = 'odom'
            message.child_frame_id = 'base_link'
            assign_vector(message.pose.pose.position, truth['position'])
            q = message.pose.pose.orientation
            q.x, q.y, q.z, q.w = truth['orientation']
            assign_vector(message.twist.twist.linear, truth['velocity'])
            assign_vector(message.twist.twist.angular, truth['angular_velocity'])
            # Deliberately no TF publication: the estimator owns odom -> base_link.
            self.truth_pub.publish(message)

    def publish_status(self):
        fresh = self.guard.fresh()
        dvl_fresh = self.last_dvl_at is not None and time.monotonic()-self.last_dvl_at < 0.5
        status = DiagnosticStatus()
        status.name = 'club_rov/simulated_navigation_sensors'
        status.hardware_id = 'unity_simulation'
        status.level = DiagnosticStatus.OK if fresh and dvl_fresh and self.last_dvl_valid else DiagnosticStatus.WARN
        status.message = self.last_problem if fresh else 'Sensor stream stale or stopped'
        status.values = [KeyValue(key='session', value=self.guard.session or ''),
                         KeyValue(key='stream_fresh', value=str(fresh)),
                         KeyValue(key='dvl_valid', value=str(fresh and dvl_fresh and self.last_dvl_valid)),
                         KeyValue(key='last_detail', value=self.last_problem),
                         KeyValue(key='rejected_packets', value=str(self.invalid_packets))]
        message = DiagnosticArray()
        message.header.stamp = self.get_clock().now().to_msg()
        message.status = [status]
        self.diagnostics_pub.publish(message)

    def destroy_node(self):
        if self.mapping is not None:
            self.mapping.close()
        self.receiver.close()
        return super().destroy_node()


def main(args=None):
    rclpy.init(args=args)
    node = None
    try:
        node = SimSensors()
        rclpy.spin(node)
    except (KeyboardInterrupt, ExternalShutdownException):
        pass
    finally:
        if node:
            node.destroy_node()
        rclpy.try_shutdown()
