#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
source "$script_dir/session-profile.sh" external-nav
mkdir -p "$run_dir"
printf '%s\n' "$$" > "$run_dir/ros.pid"
exec bash "$script_dir/../../ROS2/start-ros2.sh" external_navigation:=true "$@"
