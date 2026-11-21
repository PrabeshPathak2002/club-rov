#!/usr/bin/env bash
set -euo pipefail
source /etc/os-release
[[ "$ID" == ubuntu && "$VERSION_ID" == 22.04 ]] || { echo 'Use Ubuntu 22.04 for the Humble workspace.' >&2; exit 1; }
[[ -f /opt/ros/humble/setup.bash ]] || { echo 'Install ROS 2 Humble first.' >&2; exit 1; }
set +u # Humble's generated environment scripts read unset optional variables.
source /opt/ros/humble/setup.bash
set -u
script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
workspace="${CLUB_ROV_ROS_WS:-$HOME/club-rov-ros2-ws}"
mkdir -p "$workspace"
if [[ -e "$workspace/src" && ! -L "$workspace/src" ]]; then
    echo "Refusing to replace an existing source directory: $workspace/src" >&2
    exit 1
fi
if [[ -L "$workspace/src" && "$(readlink -f "$workspace/src")" != "$(readlink -f "$script_dir/src")" ]]; then
    echo 'Workspace src points at another checkout; choose CLUB_ROV_ROS_WS.' >&2
    exit 1
fi
[[ -L "$workspace/src" ]] || ln -s "$script_dir/src" "$workspace/src"
cd "$workspace"
colcon build --symlink-install --packages-up-to club_rov_navigation
colcon test --packages-select club_rov_navigation --event-handlers console_direct+
colcon test-result --verbose
