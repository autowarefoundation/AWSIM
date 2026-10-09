#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
UNITY_EDITOR="${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/6000.0.61f1/Editor/Unity}"
mkdir -p Logs
unset ROS_DISTRO ROS_VERSION AMENT_PREFIX_PATH COLCON_PREFIX_PATH
"$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PWD" \
  -buildTarget Linux64 -executeMethod Awsim.Jido.Editor.JidoBatchBuild.BuildLinux \
  -logFile "$PWD/Logs/jido-build.log"
test -x "${JIDO_BUILD_DIR:-Builds/Jido}/Jido.x86_64"
