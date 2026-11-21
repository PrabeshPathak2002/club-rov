#!/usr/bin/env bash
set -euo pipefail
training_env="${CLUB_ROV_RL_SENSOR_ENV:-$HOME/club-rov-rl-sensor-venv}"
python3 -c 'import sys; assert (3,10,1) <= sys.version_info[:3] <= (3,10,12), "ML-Agents 1.1.0 requires Python 3.10.1-3.10.12"'
python3 -m venv "$training_env"
"$training_env/bin/pip" install torch==2.2.2 --index-url https://download.pytorch.org/whl/cu121
"$training_env/bin/pip" install mlagents==1.1.0 numpy==1.23.5 setuptools==80.9.0 onnxruntime==1.19.2
"$training_env/bin/python" -c 'import torch; print("CUDA available:", torch.cuda.is_available()); print(torch.cuda.get_device_name(0) if torch.cuda.is_available() else "CPU fallback")'
