#!/usr/bin/env bash
set -euo pipefail
task_root="$HOME/club-rov-sitl"
script_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
source "$script_dir/session-profile.sh" "${1:-standard}"
mkdir -p "$run_dir"
cd "$run_dir"
printf '%s\n' "$$" > relay.pid
exec "$task_root/venv/bin/python" "$script_dir/json-relay.py" --server
