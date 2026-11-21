#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
training_env="${CLUB_ROV_RL_ENV:-$HOME/club-rov-rl-venv}"
player="$script_dir/../Simulation/ClubROVSim/Builds/Training/ClubROVSim.exe"
num_envs="${CLUB_ROV_RL_NUM_ENVS:-8}"
[[ -x "$training_env/bin/mlagents-learn" ]] || { echo "Run setup-training.sh first."; exit 1; }
[[ -f "$player" ]] || { echo "Build the player first: Unity > ROV > Reinforcement Learning > Build parallel training player."; exit 1; }

# mlagents-learn on Linux only launches <name>.x86_64, so a wrapper starts the Windows player through WSL interop.
wrapper_dir="$HOME/club-rov-rl-player"
mkdir -p "$wrapper_dir"
host="$wrapper_dir/TrainingPlayerHost.exe"
compiler=/mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe
"$compiler" /nologo /target:exe /platform:x64 "/out:$(wslpath -w "$host")" "$(wslpath -w "$script_dir/TrainingPlayerHost.cs")"
chmod +x "$host"
cat > "$wrapper_dir/ClubROVSim.x86_64" <<EOF
#!/usr/bin/env bash
args=()
while ((\$#)); do
  if [[ \$1 == -logFile && \$# -ge 2 ]]; then args+=(-logFile "\$(wslpath -w "\$2")"); shift 2
  else args+=("\$1"); shift; fi
done
exec "$host" 60 "$(wslpath -w "$(realpath "$player")")" "\${args[@]}"
EOF
chmod +x "$wrapper_dir/ClubROVSim.x86_64"

run_id="${CLUB_ROV_RL_RUN_ID:-pool-course-$(date +%Y%m%d-%H%M%S)}"
echo "Starting $num_envs headless simulator copies. Unity Editor Play is not needed."
echo "CPU samples: $HOME/club-rov-rl-results/$run_id-cpu.jsonl"
export OMP_NUM_THREADS=1 OPENBLAS_NUM_THREADS=1 MKL_NUM_THREADS=1
exec "$training_env/bin/python" "$script_dir/cpu-budget.py" --log "$HOME/club-rov-rl-results/$run_id-cpu.jsonl" -- \
  "$training_env/bin/python" "$script_dir/course-trainer.py" "$script_dir/pool-course.yaml" --run-id "$run_id" --results-dir "$HOME/club-rov-rl-results" --torch-device cpu \
  --env "$wrapper_dir/ClubROVSim" --num-envs "$num_envs" --no-graphics "$@" --env-args -job-worker-count 1
