#!/usr/bin/env bash
set -euo pipefail
# Ubuntu packages: sudo apt-get install build-essential python3-dev python3-venv
# pkg-config libtool libxml2-dev libxslt1-dev zlib1g-dev ccache rsync
task_root="$HOME/club-rov-sitl"
mkdir -p "$task_root"
if [[ ! -d "$task_root/ardupilot/.git" ]]; then
    git clone --depth 1 --branch Sub-4.7.2 https://github.com/ArduPilot/ardupilot.git "$task_root/ardupilot"
fi
cd "$task_root/ardupilot"
[[ "$(git rev-parse HEAD)" == fb773567896014da007bca58358704d4a0d8516f ]] || { echo 'Unexpected ArduPilot revision'; exit 1; }
git submodule update --init --depth 1 modules/waf modules/mavlink modules/DroneCAN/DSDL \
    modules/DroneCAN/dronecan_dsdlc modules/DroneCAN/libcanard modules/DroneCAN/pydronecan modules/littlefs modules/lwip
python3 -m venv "$task_root/bootstrap"
"$task_root/bootstrap/bin/pip" install uv==0.13.5
"$task_root/bootstrap/bin/uv" python install 3.12.15
"$task_root/bootstrap/bin/uv" venv --python 3.12.15 "$task_root/venv" --allow-existing
"$task_root/bootstrap/bin/uv" pip install --python "$task_root/venv/bin/python" \
    future lxml pexpect empy==3.3.4 packaging setuptools pymavlink==2.4.50 MAVProxy==1.8.75
"$task_root/venv/bin/python" waf configure --board sitl --disable-tests
"$task_root/venv/bin/python" waf sub -j 8
