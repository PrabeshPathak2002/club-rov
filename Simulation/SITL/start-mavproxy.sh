#!/usr/bin/env bash
set -euo pipefail
task_root="$HOME/club-rov-sitl"
windows_ip="${1:-127.0.0.1}"
script_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
source "$script_dir/session-profile.sh" "${2:-standard}"
extra_outputs=()
if [[ "$profile" == external-nav || "$profile" == dvl ]]; then extra_outputs+=(--out=udp:127.0.0.1:14553); fi
cd "$run_dir"
printf '%s\n' "$$" > mavproxy.pid
exec "$task_root/venv/bin/mavproxy.py" --master=tcp:127.0.0.1:5760 \
  --out="udp:$windows_ip:14550" --out=udp:127.0.0.1:14551 \
  --out=tcpin:127.0.0.1:5765 \
  --out=udp:127.0.0.1:14552 --out=udp:127.0.0.1:14554 \
  "${extra_outputs[@]}" --aircraft=UnityROV --source-system=255 --non-interactive
