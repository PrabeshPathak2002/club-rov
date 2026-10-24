#!/usr/bin/env bash
set -euo pipefail
task_root="$HOME/club-rov-sitl"
windows_ip="${1:-127.0.0.1}"
cd "$task_root/run"
printf '%s\n' "$$" > mavproxy.pid
exec "$task_root/venv/bin/mavproxy.py" --master=tcp:127.0.0.1:5760 \
  --out="udp:$windows_ip:14550" --out=udp:127.0.0.1:14551 \
  --out=tcpin:127.0.0.1:5765 \
  --aircraft=UnityROV --source-system=255 --non-interactive
