"""Short integration test through the real Core, on the running JIDO track."""
import json
import math
import os
from pathlib import Path
import subprocess
import time

from confluent_kafka import Consumer, Producer


def main():
    root = Path(__file__).resolve().parents[2]
    node = Path('/media/ladybug/Dataset1/jido-testes/jido_core/build-linux/autonomous_core_node')
    prefix = 'sil.jidocab.'
    topics = [prefix + name for name in ('core.status', 'fusion.localization', 'actuator.motor')]
    group = 'awsim-core-probe-' + str(time.time_ns())
    consumer = Consumer({'bootstrap.servers': '127.0.0.1:9092', 'group.id': group,
                         'auto.offset.reset': 'latest', 'enable.auto.commit': False})
    consumer.subscribe(topics)
    producer = Producer({'bootstrap.servers': '127.0.0.1:9092'})
    latest = {}
    states = set()
    max_throttle = 0.0
    env = dict(os.environ, JIDO_TOPIC_PREFIX='sil.', JIDO_TOPIC_PREFIX_ACT='sil.',
               JIDO_REQUIRE_SENSORS_READY='1', JIDO_SHADOW='0', JIDO_MQTT='0',
               JIDO_MAX_THROTTLE='0.3', JIDO_MAX_SPEED_MS='1.0',
               JIDO_WHEELBASE='2.7878773', JIDO_MAX_STEER_DEG='35',
               JIDO_WAYPOINT_RADIUS_M='0.5', JIDO_VEHICLE_WIDTH_M='2.0')
    env.pop('JIDO_CMD_TOKEN', None)
    log = open(root / 'Logs/jido-core.log', 'w')
    core = subprocess.Popen([str(node), '127.0.0.1:9092', group], env=env,
                            cwd=node.parent, stdout=log, stderr=subprocess.STDOUT)
    report = {'control_passed': False, 'core_pid': core.pid}

    def publish(value):
        producer.produce(prefix + 'core.route', value=json.dumps(value).encode())
        producer.flush(2)

    def pump(seconds, command=None):
        nonlocal max_throttle
        end = time.monotonic() + seconds
        next_command = 0.0
        while time.monotonic() < end:
            if core.poll() is not None:
                raise RuntimeError(f'Core exited with code {core.returncode}')
            if command is not None and time.monotonic() >= next_command:
                publish(command)
                next_command = time.monotonic() + 0.1
            message = consumer.poll(0.05)
            if message is not None and not message.error():
                value = json.loads(message.value())
                latest[message.topic()] = value
                if message.topic().endswith('core.status'):
                    states.add(value.get('state'))
                if message.topic().endswith('actuator.motor'):
                    max_throttle = max(max_throttle, value.get('throttle', 0))
            producer.poll(0)

    try:
        pump(5)
        assert topics[0] in latest, 'No Core status received'
        assert topics[1] in latest, 'No Unity localization received'
        publish({'action': 'manual_on'})
        pump(1)
        initial = latest[topics[1]]['position']
        print('Core manual mode: applying a short low-throttle command', flush=True)
        pump(3, {'action': 'manual_cmd', 'throttle': 0.2, 'angle': 0.0})
        moving = latest[topics[1]]['position']
        distance = math.hypot(moving['x'] - initial['x'], moving['y'] - initial['y'])
        print('Withholding manual commands to test the Core deadman', flush=True)
        pump(3)
        velocity = latest[topics[1]]['velocity']
        stopped_speed = math.hypot(velocity['vx'], velocity['vy'])
        report.update(distance_m=distance, maximum_core_throttle=max_throttle,
                      final_speed_mps=stopped_speed, manual_states=sorted(states))
        report['control_passed'] = distance > 0.2 and max_throttle > 0 and stopped_speed < 0.1
        publish({'action': 'manual_off'})
        publish({'action': 'reset_emergency'})
        pump(1)
        loc = latest[topics[1]]
        x, y = loc['position']['x'], loc['position']['y']
        yaw = loc['orientation']['yaw']
        # Local diagnostic route only: this test track's ENU anchor is zero.
        coords = [[(x + d * math.cos(yaw)) / 111319.490793,
                   (y + d * math.sin(yaw)) / 110574.275822] for d in (0, 2.5, 5)]
        publish({'type': 'Feature', 'properties': {'name': 'AWSIM Core quick probe'},
                 'geometry': {'type': 'LineString', 'coordinates': coords}})
        print('Core autonomous mode: following a five-metre route', flush=True)
        deadline = time.monotonic() + 25
        autonomous_states = set()
        while time.monotonic() < deadline:
            pump(0.2)
            state = latest.get(topics[0], {}).get('state')
            if state:
                autonomous_states.add(state)
            if state in ('ARRIVED', 'EMERGENCY_STOP'):
                break
        report['autonomous_probe_status'] = latest.get(topics[0])
        target = (x + 5 * math.cos(yaw), y + 5 * math.sin(yaw))
        end_pos = latest[topics[1]]['position']
        report['autonomous_states'] = sorted(autonomous_states)
        report['autonomous_distance_m'] = math.hypot(end_pos['x'] - x, end_pos['y'] - y)
        report['autonomous_target_error_m'] = math.hypot(end_pos['x'] - target[0], end_pos['y'] - target[1])
        report['autonomous_passed'] = ('NAVIGATING' in autonomous_states
                                       and 'ARRIVED' in autonomous_states
                                       and 'EMERGENCY_STOP' not in autonomous_states)
        report['states_seen'] = sorted(states)
        if not report['control_passed']:
            raise RuntimeError('Core-to-vehicle control test failed; inspect report and log')
        if not report['autonomous_passed']:
            raise RuntimeError('Core did not complete the short autonomous route')
    except Exception as exc:
        report['error'] = str(exc)
        raise
    finally:
        publish({'action': 'clear'})
        publish({'action': 'manual_on'})
        publish({'action': 'manual_cmd', 'throttle': 0.0, 'angle': 0.0})
        if core.poll() is None:
            core.terminate()
            try:
                core.wait(timeout=5)
            except subprocess.TimeoutExpired:
                core.kill()
                core.wait()
        consumer.close()
        log.close()
        (root / 'Logs/jido-core-smoke.json').write_text(json.dumps(report, indent=2) + '\n')
        print(json.dumps({k: v for k, v in report.items() if k != 'autonomous_probe_status'}, indent=2))


if __name__ == '__main__':
    main()
