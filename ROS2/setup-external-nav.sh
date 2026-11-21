#!/usr/bin/env bash
# Optional ROS Python 3.10 environment; never use SITL's Python 3.12 for rclpy.
set -euo pipefail
[[ -f /opt/ros/humble/setup.bash ]] || { echo 'Install Humble first.' >&2; exit 1; }
workspace="${CLUB_ROV_ROS_WS:-$HOME/club-rov-ros2-ws}"
python3 -m venv --system-site-packages "$workspace/mavlink-venv"
touch "$workspace/mavlink-venv/COLCON_IGNORE"
"$workspace/mavlink-venv/bin/python" -m pip install pymavlink==2.4.50
