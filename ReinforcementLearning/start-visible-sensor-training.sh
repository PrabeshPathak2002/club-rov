#!/usr/bin/env bash
# Standalone trainer window; closing a separate log viewer does not own this job.
set -euo pipefail
: "${CLUB_ROV_RL_RUN_ID:?Set a run ID before starting the visible trainer}"
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
mkdir -p "$HOME/club-rov-rl-results"
console="$HOME/club-rov-rl-results/$CLUB_ROV_RL_RUN_ID-console.log"
touch "$console"
ln -sfn "$console" "$HOME/club-rov-rl-results/active-sensor-training.log"
bash "$script_dir/start-sensor-training.sh" "$@" >> "$console" 2>&1 &
cleanup() {
  trap - EXIT INT TERM HUP
  local active
  active="$(jobs -pr)"
  [[ -z "$active" ]] || kill -TERM $active 2>/dev/null || true
  wait || true
}
trap cleanup EXIT
trap 'exit 130' INT TERM HUP
trainer_pid=$!
tail --pid="$trainer_pid" -n 30 -F "$console" &
set +e
wait "$trainer_pid"
result=$?
exit "$result"
