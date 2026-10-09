"""Exercise the shuttle's own geometry/physics; no Kafka or Core control."""
import json
import math
import os
from pathlib import Path
import select
import shutil
import signal
import socket
import subprocess
import sys
import time


def main():
    root = Path(__file__).resolve().parents[2]
    city = '--city' in sys.argv
    build_dir = 'JidoShuttle' if city else 'JidoShuttlePhysics'
    report_name = 'jido-shuttle-city-check' if city else 'jido-shuttle-physics-check'
    telemetry = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    telemetry.bind(('127.0.0.1', 25010))
    diagnostics = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    diagnostics.bind(('127.0.0.1', 25013))
    sender = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    env = dict(os.environ, JIDO_PLAYER=str(root / 'Builds' / build_dir / 'Jido.x86_64'))
    player = subprocess.Popen(['bash', str(root / 'tools/jido/run.sh')], env=env, start_new_session=True)
    latest = None
    physics = None
    samples = []
    result = {'passed': False, 'model': 'JIDO SK_Jido_F', 'mass_provisional_kg': 1500, 'city_scene': city}

    def poll():
        nonlocal latest, physics
        if player.poll() is not None:
            raise RuntimeError(f'Unity exited: {player.returncode}')
        for sock in select.select([telemetry, diagnostics], [], [], .02)[0]:
            value = json.loads(sock.recvfrom(65535)[0])
            if value['kind'] == 'localizacao':
                latest = value['payload']
                latest['_sim_time'] = value['sim_time']
            elif value['kind'] == 'shuttle_physics':
                physics = value
                samples.append(value)

    def phase(seconds, throttle=0, steering=0, reverse=False, send=True):
        end = time.monotonic() + seconds
        next_command = 0
        while time.monotonic() < end:
            if send and time.monotonic() >= next_command:
                command = dict(kind='control', throttle=throttle, brake=0 if throttle else 1,
                               steering_deg=steering, reverse=reverse)
                sender.sendto(json.dumps(command).encode(), ('127.0.0.1', 25011))
                next_command = time.monotonic() + .03
            poll()
        if latest is None or physics is None:
            raise RuntimeError('Missing shuttle telemetry/physics diagnostics')
        return latest, physics

    def speed(pose):
        return math.hypot(pose['velocity']['vx'], pose['velocity']['vy'])

    def distance(a, b):
        return math.hypot(b['position']['x']-a['position']['x'], b['position']['y']-a['position']['y'])

    try:
        end = time.monotonic()+120
        while (latest is None or physics is None) and time.monotonic()<end:
            poll()
        initial, settled = phase(5)
        assert settled['grounded_wheels'] == 4, 'Suspension did not ground all four wheels'
        assert abs(settled['mass_kg']-1500) < 1, 'Unexpected body mass'
        stable, _ = phase(2)
        drift = abs(initial['position']['z']-stable['position']['z'])
        assert drift < .03, 'Body did not settle at rest'
        straight_start = len(samples)
        straight, _ = phase(3, throttle=.2)
        straight_samples = samples[straight_start:]
        turned, _ = phase(3, throttle=.1, steering=12)
        yaw = math.atan2(math.sin(turned['orientation']['yaw']-straight['orientation']['yaw']),
                         math.cos(turned['orientation']['yaw']-straight['orientation']['yaw']))
        stopped, stop_diag = phase(3, send=False)
        backwards, _ = phase(3, throttle=.2, reverse=True)
        v = backwards['velocity']; heading=backwards['orientation']['yaw']
        reverse_speed = v['vx']*math.cos(heading)+v['vy']*math.sin(heading)
        before_barrier, _ = phase(3, reverse=True)
        normal_samples = list(samples)
        hit, hit_diag = (before_barrier, physics) if city else phase(14, throttle=.4)
        final, _ = phase(3)
        active_samples = [s for s in samples if s['sim_time'] > 4]
        max_wheel_error = max(s['wheel_visual_error_m'] for s in active_samples)
        min_contacts = min(s['grounded_wheels'] for s in normal_samples if s['sim_time'] > 4)
        max_acceleration = max(s['longitudinal_acceleration_mps2'] for s in straight_samples)
        tilt = max(max(abs((s['rotation_deg'][axis]+180)%360-180) for axis in ('x','z')) for s in active_samples)
        result.update(initial_pose=initial, settled_diagnostics=settled, rest_height_drift_m=drift,
                      straight_distance_m=distance(initial,straight), straight_speed_mps=speed(straight),
                      acceleration_interval_sim_s=straight['_sim_time']-stable['_sim_time'],
                      left_yaw_delta_rad=yaw, timeout_final_speed_mps=speed(stopped),
                      reverse_speed_mps=reverse_speed, reverse_distance_m=distance(stopped,backwards),
                      barrier_contact=hit_diag['barrier_contact'], barrier_pose=hit['position'],
                      barrier_speed_mps=speed(hit), minimum_grounded_wheels=min_contacts,
                      max_wheel_visual_error_m=max_wheel_error, max_body_tilt_deg=tilt, maximum_normal_acceleration_mps2=max_acceleration,
                      final_speed_mps=speed(final), diagnostic_samples=len(samples))
        assert distance(initial,straight) > .25 and speed(straight) > .2, 'Shuttle did not accelerate'
        assert yaw > .05, 'Positive steering did not turn left'
        assert speed(stopped) < .1 and speed(final) < .1, 'Shuttle did not stop after braking/deadman'
        assert reverse_speed < -.1, 'Shuttle did not reverse'
        if not city:
            assert hit_diag['barrier_contact'], 'Body collider did not contact the test barrier'
            assert hit['position']['x'] < 20, 'Body passed through the barrier'
            assert speed(hit) < .3, 'Body did not stop against the barrier'
        assert max_wheel_error < .05, 'Visual wheels do not follow their physical wheels'
        assert min_contacts == 4, 'A wheel lost ground contact on the flat track'
        assert tilt < 10, 'Shuttle became unstable'
        assert max_acceleration < (.7 if city else .5), 'Acceleration exceeded the requested 0.3 m/s2 after idling'
        if not city:
            assert abs(initial['position']['z']) < .02, 'Ride height differs from the model floor'
        result['passed'] = True
    except Exception as exc:
        result['error'] = str(exc)
        raise
    finally:
        sender.close(); telemetry.close(); diagnostics.close()
        if player.poll() is None:
            os.killpg(player.pid, signal.SIGTERM)
            try:
                player.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(player.pid, signal.SIGKILL); player.wait()
        (root/'Logs'/ (report_name + '.json')).write_text(json.dumps(result,indent=2)+'\n')
        if (root/'Logs/jido-player.log').exists():
            shutil.copyfile(root/'Logs/jido-player.log',root/'Logs'/ (report_name + '-player.log'))
        print(json.dumps(result,indent=2),flush=True)


if __name__ == '__main__':
    main()
