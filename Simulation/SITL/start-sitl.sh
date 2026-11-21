#!/usr/bin/env bash
set -euo pipefail
task_root="$HOME/club-rov-sitl"
script_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
windows_ip="${1:-127.0.0.1}"
source "$script_dir/session-profile.sh" "${2:-standard}"
mkdir -p "$run_dir"
cd "$run_dir"
defaults="$task_root/ardupilot/Tools/autotest/default_params/sub.parm,$script_dir/unity.parm"
if [[ "$profile" == external-nav || "$profile" == dvl ]]; then defaults+=",$script_dir/external-nav.parm"; fi
if [[ "$profile" == dvl ]]; then defaults+=",$script_dir/dvl.parm"; fi
printf '%s\n' "$$" > ardusub.pid
exec "$task_root/ardupilot/build/sitl/bin/ardusub" \
  --model "JSON:$windows_ip" --rate 400 --speedup 10 \
  --home=-35.363261,149.165230,0,0 \
  --defaults "$defaults" \
  --serial0=tcp:0
