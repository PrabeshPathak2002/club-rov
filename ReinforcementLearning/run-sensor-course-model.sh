#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
python="${CLUB_ROV_RL_SENSOR_ENV:-$HOME/club-rov-rl-sensor-venv}/bin/python"
[[ -x "$python" ]] || { echo 'Run setup-sensor-training.sh first.'; exit 1; }
exec "$python" "$script_dir/run-sensor-course-model.py" "$@"
