#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
mkdir -p Logs
unset ROS_DISTRO ROS_VERSION AMENT_PREFIX_PATH COLCON_PREFIX_PATH
exec xvfb-run -a -s '-screen 0 1280x720x24' \
  "${JIDO_PLAYER:-$PWD/Builds/Jido/Jido.x86_64}" -batchmode -force-vulkan \
  -screen-width 1280 -screen-height 720 -logFile "$PWD/Logs/jido-player.log" "$@"
