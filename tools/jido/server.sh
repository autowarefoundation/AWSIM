#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
project="$PWD"
bridge="${JIDO_BRIDGE_DIR:-/media/ladybug/Dataset1/jido-testes/jido_AWSIM_bridge}"
core_bins="${JIDO_CORE_BIN_DIR:-/media/ladybug/Dataset1/jido-testes/jido_core/build-linux}"
profile="${JIDO_PROFILE:-lexus}"
case "$profile" in
  shuttle) source "$project/tools/jido/profiles/shuttle.env" ;;
  lexus) ;;
  *) echo "Unknown JIDO_PROFILE: $profile" >&2; exit 2 ;;
esac
default_player="$project/Builds/Jido/Jido.x86_64"
if [[ "$profile" == shuttle ]]; then default_player="$project/Builds/JidoShuttle/Jido.x86_64"; fi
player="${JIDO_PLAYER:-$default_player}"
lidar_height="${JIDO_LIDAR_HEIGHT_M:-1.608}"
vehicle_width="${JIDO_VEHICLE_WIDTH_M:-2.0}"

case "${1:-status}" in
  start)
    test -x "$player"
    test -x "$bridge/.venv/bin/jido-awsim-bridge"
    test -x "$core_bins/cluster_node"
    mkdir -p "$HOME/.config/systemd/user"
    cat > "$HOME/.config/systemd/user/jido-awsim.service" <<EOF
[Unit]
Description=JIDO AWSIM test track
StartLimitIntervalSec=60
StartLimitBurst=3

[Service]
Type=simple
WorkingDirectory=$project
Environment=JIDO_PLAYER=$player
ExecStart=/usr/bin/bash $project/tools/jido/run.sh
Restart=on-failure
RestartSec=5
KillMode=control-group
TimeoutStopSec=15
EOF
    cat > "$HOME/.config/systemd/user/jido-awsim-bridge.service" <<EOF
[Unit]
Description=JIDO AWSIM UDP to Kafka bridge
StartLimitIntervalSec=60
StartLimitBurst=3

[Service]
Type=simple
WorkingDirectory=$bridge
ExecStart=$bridge/.venv/bin/jido-awsim-bridge --kafka 127.0.0.1:9092
Restart=on-failure
RestartSec=5
KillSignal=SIGINT
TimeoutStopSec=15
EOF
    cat > "$HOME/.config/systemd/user/jido-awsim-cluster.service" <<EOF
[Unit]
Description=JIDO AWSIM LiDAR clustering (existing Core binary)
StartLimitIntervalSec=60
StartLimitBurst=3

[Service]
Type=simple
WorkingDirectory=$project
Environment=JIDO_TOPIC_PREFIX=sil.
Environment=JIDO_LIDAR_HEIGHT_M=$lidar_height
Environment=JIDO_VEHICLE_WIDTH_M=$vehicle_width
Environment=JIDO_LIDAR_PISO_M=${JIDO_LIDAR_PISO_M:-0.10}
Environment=JIDO_LIDAR_TETO_M=${JIDO_LIDAR_TETO_M:-2.0}
ExecStart=$core_bins/cluster_node 127.0.0.1:9092 jido-awsim-cluster
Restart=on-failure
RestartSec=5
TimeoutStopSec=15
EOF
    systemctl --user daemon-reload
    systemctl --user start jido-awsim-bridge.service jido-awsim-cluster.service jido-awsim.service
    ;;
  stop)
    systemctl --user stop jido-awsim.service jido-awsim-bridge.service
    if systemctl --user cat jido-awsim-cluster.service >/dev/null 2>&1; then
      systemctl --user stop jido-awsim-cluster.service
    fi
    ;;
  status)
    systemctl --user --no-pager status jido-awsim.service jido-awsim-bridge.service jido-awsim-cluster.service
    ;;
  logs)
    journalctl --user -u jido-awsim-bridge.service -n 30 --no-pager
    journalctl --user -u jido-awsim-cluster.service -n 15 --no-pager
    tail -30 "$project/Logs/jido-player.log"
    ;;
  *)
    echo "Usage: $0 {start|stop|status|logs}" >&2
    exit 2
    ;;
esac
