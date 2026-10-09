"""Check recent VLP16 clouds and the existing cluster node's output."""
import json
import math
from pathlib import Path
import time

from confluent_kafka import Consumer


def main():
    root = Path(__file__).resolve().parents[2]
    cloud = 'sil.jidocab.lidar.pointcloud.full'
    obstacles = 'sil.jidocab.lidar.obstacles'
    localization = 'sil.jidocab.fusion.localization'
    consumer = Consumer({'bootstrap.servers': '127.0.0.1:9092',
                         'group.id': 'awsim-lidar-check-' + str(time.time_ns()),
                         'auto.offset.reset': 'latest', 'enable.auto.commit': False})
    consumer.subscribe([cloud, obstacles, localization])
    seen = {}
    pose = None
    try:
        deadline = time.monotonic() + 25
        while time.monotonic() < deadline and (set(seen) != {cloud, obstacles} or pose is None):
            message = consumer.poll(1)
            if message is None or message.error():
                continue
            value = json.loads(message.value())
            if time.time() - value.get('timestamp', 0) > 1.0:
                continue
            if message.topic() == cloud:
                seen[cloud] = {'num_points': value['num_points'], 'sensor': value.get('sensor'),
                               'z_range': [min(value['z']), max(value['z'])],
                               'timestamp': value['timestamp']}
            elif message.topic() == obstacles:
                seen[obstacles] = {k: value.get(k) for k in
                                  ('num_obstacles', 'obstacles', 'fwd_cru_m',
                                   'fwd_cru_pts', 'timestamp')}
            else:
                pose = value
        assert set(seen) == {cloud, obstacles}, 'Missing live LiDAR or obstacle topic'
        assert seen[cloud]['num_points'] > 100, 'LiDAR cloud is unexpectedly small'
        assert pose is not None, 'No localization for the reference-object check'
        yaw = pose['orientation']['yaw']
        dx = 10 - pose['position']['x']
        dy = 8 - pose['position']['y']
        expected = (math.cos(yaw) * dx + math.sin(yaw) * dy,
                    -math.sin(yaw) * dx + math.cos(yaw) * dy)
        detected = seen[obstacles]['obstacles']
        assert any(math.hypot(o['x'] - expected[0], o['y'] - expected[1]) < 1.5
                   for o in detected), 'The real reference object was not detected at its measured location'
        assert all(o['width'] < 5 and o['depth'] < 5 for o in detected), 'Ground is still classified as a large obstacle'
    finally:
        consumer.close()
        (root / 'Logs/jido-lidar-check.json').write_text(json.dumps(seen, indent=2) + '\n')
        print(json.dumps(seen, indent=2))


if __name__ == '__main__':
    main()
