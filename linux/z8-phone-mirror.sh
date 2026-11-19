#!/usr/bin/env bash
set -euo pipefail
command -v adb >/dev/null || { echo "Install adb first (Android platform tools)."; exit 1; }
command -v scrcpy >/dev/null || { echo "Install scrcpy first."; exit 1; }
echo "Connect Android by USB, enable USB debugging and approve the prompt."
adb devices
echo "Starting wireless phone mirroring. Keep both devices on the same trusted Wi-Fi."
scrcpy --tcpip
