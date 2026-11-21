#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
player="$script_dir/../Simulation/ClubROVSim/Builds/SensorTraining/ClubROVSim.exe"
out="${CLUB_ROV_RL_SENSOR_DEMOS:-$HOME/club-rov-rl-sensor-demos}"
episodes="${CLUB_ROV_RL_DEMO_EPISODES:-100}"
workers="${CLUB_ROV_RL_DEMO_WORKERS:-4}"
[[ "$episodes" =~ ^[0-9]+$ && "$workers" =~ ^[0-9]+$ ]] || { echo 'Use integer episode/worker counts.'; exit 1; }
(( workers >= 1 && workers <= 8 && episodes >= workers )) || { echo 'Use 1..8 workers and at least one episode per worker.'; exit 1; }
[[ -f "$player" ]] || { echo 'Build the sensor-only player in Unity first.'; exit 1; }
mkdir -p "$out"
[[ -z "$(ls -A "$out")" ]] || { echo "Output folder is not empty: $out"; exit 1; }
wrapper_dir="$HOME/club-rov-rl-sensor-player"
mkdir -p "$wrapper_dir"
host="$wrapper_dir/TrainingPlayerHost.exe"
/mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /platform:x64 "/out:$(wslpath -w "$host")" "$(wslpath -w "$script_dir/TrainingPlayerHost.cs")"
chmod +x "$host"
echo "Recording sensor observations + expert labels ($episodes episodes, $workers workers). This is NOT student-policy evaluation."
pids=()
cleanup() { local active; active="$(jobs -pr)"; [[ -z "$active" ]] || kill $active 2>/dev/null || true; }
trap cleanup EXIT
trap 'exit 130' INT TERM
for ((worker=0; worker<workers; worker++)); do
  folder="$out/worker-$worker"
  mkdir -p "$folder"
  count=$((episodes/workers + (worker < episodes%workers)))
  "$host" 60 "$(wslpath -w "$(realpath "$player")")" -batchmode -job-worker-count 1 --rov-sensor-demo \
    --rov-demo-output "$(wslpath -w "$folder")" --rov-demo-episodes "$count" --rov-demo-seed "$((731+worker))" \
    -logFile "$(wslpath -w "$folder/player.log")" > "$folder/console.log" 2>&1 &
  pids+=("$!")
done
for ((worker=0; worker<workers; worker++)); do
  wait "${pids[$worker]}"
  folder="$out/worker-$worker"
  [[ -f "$folder/teacher-summary.json" && -f "$folder/SensorCourse.demo" ]] || { echo "Worker $worker failed; inspect $folder/player.log"; exit 1; }
  cp "$folder/SensorCourse.demo" "$out/SensorCourse-$worker.demo"
done
printf '%s\n' '{"action_semantics":"previous_action","unity_mlagents":"4.2.0","vector_observation_size":16}' > "$out/raw-recording.json"
"${CLUB_ROV_RL_SENSOR_ENV:-$HOME/club-rov-rl-sensor-venv}/bin/python" "$script_dir/align-sensor-demos.py" --input "$out" --output "$out/aligned"
echo "Finished. Use aligned demonstrations in $out/aligned; raw recordings and teacher summaries retained separately."
