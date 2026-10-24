#!/usr/bin/env bash
set -euo pipefail
pid_file="$HOME/club-rov-sitl/run/mavproxy.pid"
if [[ -f "$pid_file" ]]; then
    read -r task_pid < "$pid_file"
    if [[ "$task_pid" =~ ^[0-9]+$ && -r "/proc/$task_pid/cmdline" ]]; then
        command_line="$(tr '\0' ' ' < "/proc/$task_pid/cmdline")"
        if [[ "$command_line" == *"club-rov-sitl/venv/bin/mavproxy.py"* ]]; then
            kill -TERM "$task_pid"
        fi
    fi
fi
