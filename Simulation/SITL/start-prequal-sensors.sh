#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
profile="${1:-standard}"
source "$script_dir/session-profile.sh" "$profile"
shift || true
extra=()
# The real Tracker 650 feeds ArduSub directly. The ROS estimator still runs because the simulated
# camera is only published alongside /odometry/local, but nothing forwards it to ArduSub.
if [[ "$profile" == dvl ]]; then extra+=(cerulean_dvl:=true); fi
mkdir -p "$run_dir"
printf '%s\n' "$$" > "$run_dir/ros.pid"
exec bash "$script_dir/../../ROS2/start-ros2.sh" with_mapping:=true with_rtabmap:=false "${extra[@]}" "$@"
