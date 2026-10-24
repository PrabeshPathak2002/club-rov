#!/usr/bin/env bash
set -euo pipefail
task_root="$HOME/club-rov-sitl"
script_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
windows_ip="${1:-127.0.0.1}"
mkdir -p "$task_root/run"
cd "$task_root/run"
"$task_root/ardupilot/build/sitl/bin/ardusub" \
  --model "JSON:$windows_ip" --rate 400 --speedup 10 \
  --home=-35.363261,149.165230,0,0 \
  --defaults "$task_root/ardupilot/Tools/autotest/default_params/sub.parm,$script_dir/unity.parm" \
  --serial0=tcp:0
