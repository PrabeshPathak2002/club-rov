#!/usr/bin/env bash
set -euo pipefail
set +u # Humble's generated environment scripts read unset optional variables.
source /opt/ros/humble/setup.bash
source "${CLUB_ROV_ROS_WS:-$HOME/club-rov-ros2-ws}/install/setup.bash"
set -u
export ROS_DOMAIN_ID="${ROS_DOMAIN_ID:-42}"
export ROS_LOCALHOST_ONLY=1
exec ros2 launch club_rov_navigation simulation.launch.py "$@"
