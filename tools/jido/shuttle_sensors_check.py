"""Validate sensor frames, real LiDAR targets/ground, and IMU against motion."""
import json
import math
from pathlib import Path
import socket
import statistics
import sys
import time
from confluent_kafka import Consumer, Producer


def main():
    root = Path(__file__).resolve().parents[2]
    city = '--city' in sys.argv
    geometry = json.loads((root/'Logs/jido-shuttle-sensor-geometry.json').read_text())
    topics = {k: 'sil.jidocab.'+v for k,v in {
        'pose':'fusion.localization', 'imu':'sensor.imu',
        'cloud':'lidar.pointcloud.full', 'obstacles':'lidar.obstacles'}.items()}
    consumer = Consumer({'bootstrap.servers':'127.0.0.1:9092',
                         'group.id':'shuttle-sensors-'+str(time.time_ns()),
                         'auto.offset.reset':'latest','enable.auto.commit':False,
                         'fetch.message.max.bytes':4194304})
    consumer.subscribe(list(topics.values()))
    producer = Producer({'bootstrap.servers':'127.0.0.1:9092'})
    probe = socket.socket(socket.AF_INET,socket.SOCK_DGRAM)
    probe.bind(('127.0.0.1',25013)); probe.setblocking(False)
    latest = {}; counts = {}; imu_samples = []; clouds = []; diagnostics = None
    result = {'passed':False, 'city':city, 'geometry':geometry}

    def poll():
        nonlocal diagnostics
        while True:
            try: diagnostics = json.loads(probe.recvfrom(65535)[0])
            except BlockingIOError: break
        message = consumer.poll(.02)
        if message is None or message.error(): return
        value = json.loads(message.value())
        if time.time()-value.get('timestamp',0)>1: return
        name = next(k for k,v in topics.items() if v==message.topic())
        latest[name]=value; counts[name]=counts.get(name,0)+1
        if name=='imu': imu_samples.append(value)
        if name=='cloud':
            clouds.append(value)
            if len(clouds)>5: clouds.pop(0)

    def phase(seconds,throttle=0,angle=0,send=True):
        end=time.monotonic()+seconds; next_command=0
        while time.monotonic()<end:
            if send and time.monotonic()>=next_command:
                producer.produce('sil.jidocab.actuator.motor',key='ego-01',value=json.dumps(
                    dict(on=bool(throttle),throttle=throttle,brake=0 if throttle else 1,reverse=False)).encode())
                producer.produce('sil.jidocab.actuator.steering',key='ego-01',value=json.dumps({'angle':angle}).encode())
                producer.flush(1); next_command=time.monotonic()+.05
            poll()
        return latest['pose']

    def plane(points):
        # Least-squares z = a*x+b*y+c, independent of the configured sensor height.
        rows=[[sum(p[0]*p[0] for p in points),sum(p[0]*p[1] for p in points),sum(p[0] for p in points),sum(p[0]*p[2] for p in points)],
              [sum(p[0]*p[1] for p in points),sum(p[1]*p[1] for p in points),sum(p[1] for p in points),sum(p[1]*p[2] for p in points)],
              [sum(p[0] for p in points),sum(p[1] for p in points),len(points),sum(p[2] for p in points)]]
        for col in range(3):
            pivot=max(range(col,3),key=lambda i:abs(rows[i][col])); rows[col],rows[pivot]=rows[pivot],rows[col]
            scale=rows[col][col]; rows[col]=[v/scale for v in rows[col]]
            for i in range(3):
                if i!=col:
                    factor=rows[i][col]; rows[i]=[v-factor*w for v,w in zip(rows[i],rows[col])]
        return [row[3] for row in rows]

    def distance(a,b): return math.sqrt(sum((a[k]-b[k])**2 for k in ('x','y','z')))

    try:
        end=time.monotonic()+120
        while (len(latest)<4 or diagnostics is None) and time.monotonic()<end: poll()
        assert len(latest)==4 and diagnostics, 'Missing sensor/cluster/physics telemetry'
        phase(3)
        imu_start=len(imu_samples); pose=phase(3)
        diag=diagnostics
        center=diag['position']; forward=diag['forward']; offset=geometry['rear_axle_offset_m']
        expected={'x':center['z']-forward['z']*offset,
                  'y':-center['x']+forward['x']*offset,
                  'z':center['y']-forward['y']*offset}
        rear_error=distance(pose['position'],expected)
        contact=diag['rear_ground_contact']; contact_jido={'x':contact['z'],'y':-contact['x'],'z':contact['y']}
        contact_error=distance(pose['position'],contact_jido)
        imu_rest=imu_samples[imu_start:]
        measured={axis:statistics.mean(s[axis] for s in imu_rest) for axis in ('ax','ay','az','wx','wy','wz')}
        pitch=pose['orientation']['pitch']; roll=pose['orientation']['roll']
        gravity={'ax':-9.81*math.sin(pitch),'ay':9.81*math.sin(roll)*math.cos(pitch),'az':9.81*math.cos(roll)*math.cos(pitch)}
        gravity_error=math.sqrt(sum((measured[k]-gravity[k])**2 for k in gravity))
        lidar_height=diag['lidar_optical_position']['y']-center['y']
        imu_height=diag['imu_position']['y']-center['y']
        all_points=[p for cloud in clouds for p in zip(cloud['x'],cloud['y'],cloud['z'])]
        floor=[p for p in all_points if 8<p[0]<16 and abs(p[1])<1.2 and -2.55<p[2]<-2.15
               and (city or not (10.8<p[0]<13.2))]
        result.update(counts=counts,rear_reference_error_m=rear_error,rear_contact_error_m=contact_error,imu_rest_mean=measured,imu_gravity_error_mps2=gravity_error,measured_lidar_world_height_m=lidar_height,floor_points=len(floor))
        assert len(floor)>100,'Not enough real floor returns for height calibration'
        coefficients=plane(floor); ground_height=-coefficients[2]
        residual=math.sqrt(statistics.mean((p[2]-sum(a*b for a,b in zip(coefficients,(p[0],p[1],1))))**2 for p in floor))
        obstacles=latest['obstacles']; detections=obstacles['obstacles']
        target_errors=[]
        if not city:
            for x,y in ((12,0),(8,6),(8,-6)):
                error=min((math.hypot(o['x']-x,o['y']-y) for o in detections),default=999)
                target_errors.append(error)
            result.update(measured_target_errors_m=target_errors,static_obstacles=obstacles,ground_optical_height_m=ground_height,ground_plane_rmse_m=residual)
            assert max(target_errors)<1,'Measured front/left/right target was not detected in its proper axis'
            assert 10.5<obstacles['fwd_cru_m']<13,'Floor produced a false corridor obstacle, or the front target is missing'
            assert abs(coefficients[0])<.005 and abs(coefficients[1])<.005,'LiDAR mount is tilted on the flat track'
        else:
            assert obstacles['fwd_cru_m']<0 or obstacles['fwd_cru_m']>5,'Unexpected close obstacle in the previously clear urban corridor'
        start=phase(1); moving=phase(3,throttle=.2)
        turn_start=latest['pose']; turning=phase(4,throttle=.1,angle=12)
        turn_samples=[s for s in imu_samples if turn_start['sim_time']<=s['sim_time']<=turning['sim_time']]
        gyro_yaw=sum(.5*(a['wz']+b['wz'])*(b['sim_time']-a['sim_time']) for a,b in zip(turn_samples,turn_samples[1:]))
        yaw_delta=math.atan2(math.sin(turning['orientation']['yaw']-turn_start['orientation']['yaw']),math.cos(turning['orientation']['yaw']-turn_start['orientation']['yaw']))
        stopped=phase(3,send=False); speed=math.hypot(stopped['velocity']['vx'],stopped['velocity']['vy'])
        result.update(counts=counts,rear_reference_error_m=rear_error,rear_contact_error_m=contact_error,
                      measured_lidar_world_height_m=lidar_height,measured_imu_world_height_m=imu_height,
                      imu_rest_mean=measured,imu_gravity_expected=gravity,imu_gravity_error_mps2=gravity_error,
                      ground_plane_coefficients=coefficients,ground_optical_height_m=ground_height,
                      ground_plane_rmse_m=residual,floor_points=len(floor),cloud_points=latest['cloud']['num_points'],
                      measured_target_errors_m=target_errors,static_obstacles=obstacles,
                      gyro_integrated_yaw_rad=gyro_yaw,pose_yaw_delta_rad=yaw_delta,
                      final_speed_mps=speed,initial_pose=pose)
        assert rear_error<.02 and contact_error<.04,'Localization is not on the rear axle ground reference'
        assert abs(lidar_height-geometry['lidar_optical_height_m'])<.02,'Wrong optical sensor height'
        assert abs(imu_height-1)<.02,'Wrong IMU mount height'
        assert abs(ground_height-geometry['lidar_optical_height_m'])<(.08 if city else .03),'Ground returns disagree with sensor height'
        assert residual<(.08 if city else .02),'Floor returns do not form a plausible ground plane'
        assert gravity_error<.1,'IMU specific-force axes/gravity sign mismatch'
        assert max(abs(measured[k]) for k in ('wx','wy','wz'))<.01,'IMU gyroscope is nonzero at rest'
        assert yaw_delta>.03 and gyro_yaw>0,'Gyroscope/pose yaw has the wrong steering sign'
        assert abs(gyro_yaw-yaw_delta)<.04,'Gyroscope units or yaw magnitude do not match actual rotation'
        assert speed<.1,'Shuttle did not stop after commands ceased'
        result['passed']=True
    except Exception as exc:
        result['error']=str(exc); raise
    finally:
        producer.produce('sil.jidocab.actuator.motor',key='ego-01',value=b'{"on":false,"throttle":0,"brake":1}')
        producer.flush(2); consumer.close(); probe.close()
        name='jido-shuttle-city-sensors-check.json' if city else 'jido-shuttle-sensors-check.json'
        (root/'Logs'/name).write_text(json.dumps(result,indent=2)+'\n')
        print(json.dumps(result,indent=2),flush=True)


if __name__=='__main__': main()
