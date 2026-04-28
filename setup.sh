#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VENV_DIR="${PROJECT_ROOT}/.venv"

choose_python() {
    if command -v python3 >/dev/null 2>&1; then
        command -v python3
        return 0
    fi

    echo "[ERROR] python3 not found." >&2
    return 1
}

PYTHON_BIN="$(choose_python)"

echo "[INFO] Project root: ${PROJECT_ROOT}"
echo "[INFO] Python: ${PYTHON_BIN}"
"${PYTHON_BIN}" --version

if [[ ! -d "${VENV_DIR}" ]]; then
    echo "[INFO] Creating virtual environment at ${VENV_DIR}"
    "${PYTHON_BIN}" -m venv "${VENV_DIR}"
else
    echo "[INFO] Reusing existing virtual environment at ${VENV_DIR}"
fi

source "${VENV_DIR}/bin/activate"

echo "[INFO] Upgrading pip/setuptools/wheel"
python -m pip install --upgrade pip setuptools wheel

echo "[INFO] Installing Python packages"
if ! python -m pip install -r "${PROJECT_ROOT}/requirements.txt"; then
    echo "[ERROR] Failed to install requirements." >&2
    echo "[HINT] On some WSL Ubuntu setups, mediapipe may not provide a wheel for the active Python version." >&2
    echo "[HINT] If this machine only has Python 3.12, install Python 3.10 or 3.11 and rerun setup." >&2
    exit 1
fi

echo "[INFO] Running environment check"
python "${PROJECT_ROOT}/check_env.py"

echo "[INFO] Setup completed"
