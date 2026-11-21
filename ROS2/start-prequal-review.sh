#!/usr/bin/env bash
set -euo pipefail
set +u
source /opt/ros/humble/setup.bash
workspace="${CLUB_ROV_ROS_WS:-$HOME/club-rov-ros2-ws}"
source "$workspace/install/setup.bash"
set -u
exec "$workspace/mavlink-venv/bin/python" "$workspace/install/club_rov_navigation/lib/club_rov_navigation/prequal_review" "$@"
