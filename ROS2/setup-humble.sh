#!/usr/bin/env bash
# Run with sudo in Ubuntu 22.04; intended for a dedicated development environment.
set -euo pipefail
source /etc/os-release
[[ "$ID" == ubuntu && "$VERSION_ID" == 22.04 ]] || { echo 'ROS 2 Humble setup requires Ubuntu 22.04.' >&2; exit 1; }
[[ "$EUID" == 0 ]] || { echo 'Run this setup script with sudo.' >&2; exit 1; }
export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get upgrade -y
apt-get install -y curl ca-certificates locales software-properties-common python3
locale-gen en_US.UTF-8
add-apt-repository -y universe
if ! dpkg-query -W ros2-apt-source >/dev/null 2>&1; then
    version=$(curl -fsSL https://api.github.com/repos/ros-infrastructure/ros-apt-source/releases/latest | python3 -c 'import json,sys; print(json.load(sys.stdin)["tag_name"])')
    curl -fL "https://github.com/ros-infrastructure/ros-apt-source/releases/download/${version}/ros2-apt-source_${version}.jammy_all.deb" -o /tmp/club-rov-ros2-apt-source.deb
    dpkg -i /tmp/club-rov-ros2-apt-source.deb
fi
apt-get update
apt-get install -y build-essential ros-humble-ros-base ros-humble-robot-localization ros-humble-imu-filter-madgwick python3-colcon-common-extensions python3-pytest python3-venv
echo 'ROS 2 Humble dependencies installed. Source /opt/ros/humble/setup.bash before building.'
