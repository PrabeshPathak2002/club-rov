#!/usr/bin/env python3
"""Read-only live check: sensor topics, estimator output and status; no vehicle commands."""
import json
import math
import time
import argparse
import rclpy
from rclpy.node import Node
from rclpy.qos import qos_profile_sensor_data
from sensor_msgs.msg import Imu
from nav_msgs.msg import Odometry
from club_rov_interfaces.msg import Dvl
from diagnostic_msgs.msg import DiagnosticArray, DiagnosticStatus


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--expect-stale', action='store_true', help='Check watchdog after stopping Unity stream.')
    args = parser.parse_args()
    rclpy.init()
    node = Node('navigation_measurement_check')
    values = {'dvl': [], 'imu': [], 'odom': [], 'status': []}
    subscriptions = [
        node.create_subscription(Dvl, '/dvl/data', lambda m: values['dvl'].append(m), 10),
        node.create_subscription(Imu, '/imu/data_raw', lambda m: values['imu'].append(m), qos_profile_sensor_data),
        node.create_subscription(Odometry, '/odometry/local', lambda m: values['odom'].append(m), 10),
        node.create_subscription(DiagnosticArray, '/diagnostics', lambda m: values['status'].extend(
            s for s in m.status if s.name == 'club_rov/simulated_navigation_sensors'), 10),
    ]
    try:
        end = time.monotonic()+(2 if args.expect_stale else 8)
        while time.monotonic() < end:
            rclpy.spin_once(node, timeout_sec=0.1)
        if args.expect_stale:
            assert values['status'], 'Sensor watchdog diagnostics missing'
            last = values['status'][-1]
            details = {v.key:v.value for v in last.values}
            assert last.level == DiagnosticStatus.WARN and details['stream_fresh'] == 'False'
            assert not values['dvl'] and not values['imu'], 'Stopped stream still published sensor measurements'
            print(json.dumps({'status':'PASS - wall-clock stale watchdog; no replacement sensor measurements',
                              'details':details}, indent=2))
            return
        assert len(values['dvl']) >= 20, 'DVL measurements missing'
        assert len(values['imu']) >= 100, 'IMU measurements missing'
        assert len(values['odom']) >= 30, 'Estimator output missing'
        assert any(m.valid for m in values['dvl']), 'No valid bottom lock'
        assert values['imu'][-1].orientation_covariance[0] == -1, 'Raw IMU contains an orientation estimate'
        for key in ('dvl', 'imu', 'odom'):
            stamps = [m.header.stamp.sec*1_000_000_000+m.header.stamp.nanosec for m in values[key]]
            assert all(b > a for a, b in zip(stamps, stamps[1:])), key+' time is not increasing'
        last = values['odom'][-1]
        p = last.pose.pose.position
        q = last.pose.pose.orientation
        assert last.header.frame_id == 'odom' and last.child_frame_id == 'base_link'
        assert all(math.isfinite(x) for x in [p.x,p.y,p.z,q.x,q.y,q.z,q.w])
        assert abs(sum(x*x for x in [q.x,q.y,q.z,q.w])-1) < 0.01
        assert abs(p.x) < 1 and abs(p.y) < 1, 'Stationary local estimate drifted over a metre'
        assert values['status'] and values['status'][-1].level == DiagnosticStatus.OK, 'Navigation sensor status is not healthy'
        summary = {'counts': {key: len(v) for key,v in values.items()},
                   'position_m': [p.x,p.y,p.z], 'altitude_m': values['dvl'][-1].altitude,
                   'position_variance': [last.pose.covariance[i] for i in (0,7,14)],
                   'status': 'PASS - live Unity measurements and ROS local estimate; no motor commands'}
        print(json.dumps(summary,indent=2))
    finally:
        node.destroy_node()
        rclpy.try_shutdown()


if __name__ == '__main__':
    main()
