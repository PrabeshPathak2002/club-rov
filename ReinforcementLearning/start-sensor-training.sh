#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
training_env="${CLUB_ROV_RL_SENSOR_ENV:-$HOME/club-rov-rl-sensor-venv}"
player="$script_dir/../Simulation/ClubROVSim/Builds/SensorTraining/ClubROVSim.exe"
run_id="${CLUB_ROV_RL_RUN_ID:-pool-sensors-$(date +%Y%m%d-%H%M%S)}"
wrapper_dir="$HOME/club-rov-rl-sensor-player"
[[ -x "$training_env/bin/python" ]] || { echo 'Run setup-sensor-training.sh first.'; exit 1; }
[[ -f "$player" ]] || { echo 'Build the sensor-only player in Unity first.'; exit 1; }
mkdir -p "$wrapper_dir"
host="$wrapper_dir/TrainingPlayerHost.exe"
/mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /platform:x64 "/out:$(wslpath -w "$host")" "$(wslpath -w "$script_dir/TrainingPlayerHost.cs")"
chmod +x "$host"
player_cpu_limit=60
cpu_guard_args=()
if [[ "${CLUB_ROV_RL_MAX_PERFORMANCE:-0}" == 1 ]]; then
  player_cpu_limit=100
  cpu_guard_args+=(--no-throttle)
fi
cat > "$wrapper_dir/ClubROVSim.x86_64" <<EOF
#!/usr/bin/env bash
args=()
while ((\$#)); do
  if [[ \$1 == -logFile && \$# -ge 2 ]]; then args+=(-logFile "\$(wslpath -w "\$2")"); shift 2
  else args+=("\$1"); shift; fi
done
exec "$host" $player_cpu_limit "$(wslpath -w "$(realpath "$player")")" "\${args[@]}"
EOF
chmod +x "$wrapper_dir/ClubROVSim.x86_64"
config="$HOME/club-rov-rl-results/$run_id-config.yaml"
config_args=(--output "$config")
[[ "${CLUB_ROV_RL_HOLD_PRACTICE:-0}" != 1 ]] || config_args+=(--hold-practice)
[[ -z "${CLUB_ROV_RL_SENSOR_DEMOS:-}" ]] || config_args+=(--demos "$CLUB_ROV_RL_SENSOR_DEMOS")
[[ -z "${CLUB_ROV_RL_MAX_STEPS:-}" ]] || config_args+=(--max-steps "$CLUB_ROV_RL_MAX_STEPS")
"$training_env/bin/python" "$script_dir/sensor-config.py" "${config_args[@]}"
if [[ "${CLUB_ROV_RL_AUTO_RESUME:-0}" == 1 && -f "$HOME/club-rov-rl-results/$run_id/PoolCourseSensors/checkpoint.pt" ]]; then
  set -- --resume "$@"
fi
export CLUB_ROV_RL_BEHAVIOR=PoolCourseSensors OMP_NUM_THREADS=1 OPENBLAS_NUM_THREADS=1 MKL_NUM_THREADS=1
echo "Sensor RL: ${CLUB_ROV_RL_NUM_ENVS:-4} rendered players, GPU trainer; camera rendering is required."
exec "$training_env/bin/python" "$script_dir/cpu-budget.py" --log "$HOME/club-rov-rl-results/$run_id-cpu.jsonl" "${cpu_guard_args[@]}" -- \
  "$training_env/bin/python" "$script_dir/course-trainer.py" "$config" --run-id "$run_id" --results-dir "$HOME/club-rov-rl-results" \
  --torch-device "${CLUB_ROV_RL_SENSOR_DEVICE:-cuda}" --env "$wrapper_dir/ClubROVSim" --num-envs "${CLUB_ROV_RL_NUM_ENVS:-4}" \
  --timeout-wait 300 "$@" --env-args -batchmode -job-worker-count 1 --rov-rl-sensors
