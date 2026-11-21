#!/usr/bin/env bash
set -euo pipefail
set +u
source /opt/ros/humble/setup.bash
workspace="${CLUB_ROV_ROS_WS:-$HOME/club-rov-ros2-ws}"
source "$workspace/install/setup.bash"
set -u
export ROS_DOMAIN_ID="${ROS_DOMAIN_ID:-42}"
export ROS_LOCALHOST_ONLY="${ROS_LOCALHOST_ONLY:-1}"
echo 'Camera calibration only: no MAVLink connection or vehicle commands.'
exec "$workspace/mavlink-venv/bin/python" "$workspace/install/club_rov_navigation/lib/club_rov_navigation/calibrate_camera" "$@"
