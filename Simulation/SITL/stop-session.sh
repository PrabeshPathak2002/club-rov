#!/usr/bin/env bash
set -euo pipefail
task_root="$HOME/club-rov-sitl"
script_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
source "$script_dir/session-profile.sh" "${1:-standard}"
# Recheck PID identity before escalation; never kill by a broad process-name pattern.
for helper in ros mavproxy ardusub relay; do
    pid_file="$run_dir/$helper.pid"
    [[ -f "$pid_file" ]] || continue
    read -r task_pid < "$pid_file"
    [[ "$task_pid" =~ ^[0-9]+$ ]] || continue
    case "$helper" in
        mavproxy) expected="$task_root/venv/bin/mavproxy.py" ;;
        ardusub) expected="$task_root/ardupilot/build/sitl/bin/ardusub" ;;
        relay) expected='json-relay.py --server' ;;
        ros)
            if [[ "$profile" == external-nav ]]; then launch_args='external_navigation:=true'
            else launch_args='with_mapping:=true with_rtabmap:=false'; fi
            expected="/opt/ros/humble/bin/ros2 launch club_rov_navigation simulation.launch.py $launch_args" ;;
    esac
    matches_helper() {
        [[ -r "/proc/$task_pid/cmdline" ]] || return 1
        command_line="$(tr '\0' ' ' 2>/dev/null < "/proc/$task_pid/cmdline")" || return 1
        [[ "$command_line" == *"$expected"* ]] || return 1
        [[ "$helper" != relay || "$command_line" == *"$task_root/venv/bin/python"* ]]
    }
    if matches_helper; then
        signal=TERM
        attempts=20
        if [[ "$helper" == ros ]]; then signal=INT; attempts=100; fi
        kill -"$signal" "$task_pid" 2>/dev/null || true
        for ((attempt=0; attempt<attempts; attempt++)); do
            matches_helper || break
            sleep 0.05
        done
        if matches_helper; then kill -KILL "$task_pid" 2>/dev/null || true; fi
    fi
    rm -f -- "$pid_file"
done
