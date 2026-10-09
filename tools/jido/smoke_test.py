"""Exercise the Unity player locally, without Kafka or Core commands."""

import json
import math
import os
from pathlib import Path
import signal
import socket
import subprocess
import time


def main():
    root = Path(__file__).resolve().parents[2]
    receiver = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    receiver.bind(("127.0.0.1", 25010))
    receiver.settimeout(0.02)
    sender = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    counts = {}
    latest = None
    player = subprocess.Popen(["bash", str(root / "tools/jido/run.sh")],
                              start_new_session=True)
    result = {"passed": False}

    def sample():
        nonlocal latest
        if player.poll() is not None:
            raise RuntimeError(f"Unity exited with code {player.returncode}")
        try:
            raw, _ = receiver.recvfrom(65535)
        except socket.timeout:
            return
        packet = json.loads(raw)
        kind = packet.get("kind")
        counts[kind] = counts.get(kind, 0) + 1
        if kind == "localizacao":
            latest = packet

    def phase(seconds, throttle=0.0, steering=0.0, send=True):
        until = time.monotonic() + seconds
        next_command = 0.0
        while time.monotonic() < until:
            now = time.monotonic()
            if send and now >= next_command:
                command = dict(kind="control", throttle=throttle,
                               brake=1.0 if throttle == 0 else 0.0,
                               steering_deg=steering, reverse=False)
                sender.sendto(json.dumps(command).encode(), ("127.0.0.1", 25011))
                next_command = now + 0.02
            sample()
        if latest is None:
            raise RuntimeError("No localization received from Unity")
        return latest["payload"]

    def speed(pose):
        v = pose["velocity"]
        return math.hypot(v["vx"], v["vy"])

    try:
        deadline = time.monotonic() + 120
        while latest is None and time.monotonic() < deadline:
            sample()
        if latest is None:
            raise RuntimeError("Unity produced no telemetry within 120 seconds")
        initial = phase(2)
        print("Telemetry received; testing acceleration", flush=True)
        straight = phase(4, throttle=0.4)
        print("Testing positive-left steering", flush=True)
        turned = phase(3, throttle=0.2, steering=12)
        print("Withholding commands to test the Unity timeout", flush=True)
        stopped = phase(4, send=False)
        travel = math.dist([initial["position"][k] for k in ("x", "y")],
                           [straight["position"][k] for k in ("x", "y")])
        yaw_delta = turned["orientation"]["yaw"] - straight["orientation"]["yaw"]
        yaw_delta = math.atan2(math.sin(yaw_delta), math.cos(yaw_delta))
        result.update(telemetry_counts=counts, straight_distance_m=travel,
                      straight_speed_mps=speed(straight), left_yaw_delta_rad=yaw_delta,
                      timeout_final_speed_mps=speed(stopped), final_pose=stopped)
        assert counts.get("imu", 0) > 0, "No IMU telemetry"
        assert travel > 0.5 and speed(straight) > 0.3, "Vehicle did not accelerate"
        assert yaw_delta > 0.02, "Positive steering did not turn left"
        assert speed(stopped) < 0.1, "Vehicle did not stop after command timeout"
        result["passed"] = True
    except Exception as exc:
        result["error"] = str(exc)
        raise
    finally:
        sender.close()
        receiver.close()
        if player.poll() is None:
            os.killpg(player.pid, signal.SIGTERM)
            try:
                player.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(player.pid, signal.SIGKILL)
                player.wait()
        path = root / "Logs/jido-smoke.json"
        path.parent.mkdir(exist_ok=True)
        path.write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps(result, indent=2), flush=True)


if __name__ == "__main__":
    main()
