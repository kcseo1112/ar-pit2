#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VENV_DIR="${PROJECT_ROOT}/.venv"

if [[ ! -d "${VENV_DIR}" ]]; then
    echo "[INFO] .venv not found. Running setup first."
    "${PROJECT_ROOT}/setup.sh"
fi

source "${VENV_DIR}/bin/activate"

python "${PROJECT_ROOT}/check_env.py"
python "${PROJECT_ROOT}/mp_pose_sender.py"
