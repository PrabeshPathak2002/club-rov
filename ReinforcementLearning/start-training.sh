#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
training_env="${CLUB_ROV_RL_ENV:-$HOME/club-rov-rl-venv}"
[[ -x "$training_env/bin/mlagents-learn" ]] || { echo "Run setup-training.sh first."; exit 1; }
run_id="pool-course-$(date +%Y%m%d-%H%M%S)"
echo "In Unity, stop Play, choose ROV > Reinforcement Learning > Enable first-course training, then press Play."
exec "$training_env/bin/python" "$script_dir/course-trainer.py" "$script_dir/pool-course.yaml" --run-id "$run_id" --results-dir "$HOME/club-rov-rl-results" --torch-device cpu "$@"
