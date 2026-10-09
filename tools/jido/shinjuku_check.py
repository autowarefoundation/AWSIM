"""Check the Shinjuku player through the real UDP/Kafka bridge; no Core control."""
import json
import math
from pathlib import Path
import time
from confluent_kafka import Consumer, Producer


def main():
    root = Path(__file__).resolve().parents[2]
    topics = {"pose": "sil.jidocab.fusion.localization",
              "imu": "sil.jidocab.sensor.imu",
              "cloud": "sil.jidocab.lidar.pointcloud.full",
              "obstacles": "sil.jidocab.lidar.obstacles"}
    consumer = Consumer({"bootstrap.servers": "127.0.0.1:9092",
                         "group.id": f"jido-shinjuku-check-{time.time_ns()}",
                         "auto.offset.reset": "latest", "enable.auto.commit": False})
    consumer.subscribe(list(topics.values()))
    producer = Producer({"bootstrap.servers": "127.0.0.1:9092"})
    latest = {}
    counts = {}
    result = {"passed": False}

    def poll():
        message = consumer.poll(0.05)
        if message is None or message.error():
            return
        value = json.loads(message.value())
        if time.time() - value.get("timestamp", 0) > 1.0:
            return
        name = next(k for k, topic in topics.items() if topic == message.topic())
        latest[name] = value
        counts[name] = counts.get(name, 0) + 1

    def phase(seconds, throttle=0.0, send=True):
        end = time.monotonic() + seconds
        next_command = 0.0
        while time.monotonic() < end:
            if send and time.monotonic() >= next_command:
                command = dict(throttle=throttle, on=bool(throttle),
                               brake=0.0 if throttle else 1.0, reverse=False)
                producer.produce("sil.jidocab.actuator.motor", key="ego-01", value=json.dumps(command).encode())
                producer.produce("sil.jidocab.actuator.steering", key="ego-01", value=b'{"angle":0.0}')
                producer.flush(1)
                next_command = time.monotonic() + 0.05
            poll()
        return latest["pose"]

    try:
        end = time.monotonic() + 180
        while len(latest) < 4 and time.monotonic() < end:
            poll()
        if len(latest) < 4:
            raise RuntimeError(f"Missing telemetry: {set(topics) - set(latest)}")
        initial = phase(3)
        ref = initial["enu_ref"]
        assert abs(ref["lat"] - 35.6880484485) < 0.00001
        assert abs(ref["lon"] - 139.6920938786) < 0.00001
        moved = phase(3, throttle=0.2)
        stopped = phase(4, send=False)
        travel = math.dist([initial["position"][k] for k in ("x", "y")],
                           [moved["position"][k] for k in ("x", "y")])
        speed = math.hypot(stopped["velocity"]["vx"], stopped["velocity"]["vy"])
        cloud = latest["cloud"]
        point_count = cloud["num_points"]
        result.update(telemetry_counts=counts, initial_pose=initial,
                      moved_pose=moved, stopped_pose=stopped,
                      travel_m=travel, timeout_speed_mps=speed,
                      cloud_points=point_count, cloud_keys=list(cloud),
                      obstacle_payload=latest["obstacles"])
        assert point_count > 100, "No useful real LiDAR cloud"
        assert travel > 0.25, "Vehicle did not move on Shinjuku road"
        assert speed < 0.1, "Vehicle did not stop after command timeout"
        assert abs(stopped["position"]["z"] - initial["position"]["z"]) < 1, "Vehicle fell through road"
        result["passed"] = True
    except Exception as exc:
        result["error"] = str(exc)
        raise
    finally:
        producer.produce("sil.jidocab.actuator.motor", key="ego-01", value=b'{"throttle":0.0,"on":false,"brake":1.0}')
        producer.flush(2)
        consumer.close()
        (root / "Logs/jido-shinjuku-check.json").write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps(result, indent=2), flush=True)


if __name__ == "__main__":
    main()
