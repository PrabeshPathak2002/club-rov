#!/usr/bin/env bash
set -euo pipefail
set +u
source /opt/ros/humble/setup.bash
workspace="${CLUB_ROV_ROS_WS:-$HOME/club-rov-ros2-ws}"
source "$workspace/install/setup.bash"
set -u
export ROS_DOMAIN_ID="${ROS_DOMAIN_ID:-42}"
export ROS_LOCALHOST_ONLY=1
echo 'SIMULATION ONLY: disable QGC joystick, use Empty Pool, start the DVL SITL session first.'
echo 'This explicitly arms SYSID 42 after navigation checks. Ctrl+C stops and disarms the simulation.'
exec "$workspace/mavlink-venv/bin/python" "$workspace/install/club_rov_navigation/lib/club_rov_navigation/sim_mission" --confirm-simulation "$@"
