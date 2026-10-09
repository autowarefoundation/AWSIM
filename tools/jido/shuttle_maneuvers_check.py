"""Real Core + LiDAR + native physics: two arcs and a appearing barrier."""
import json
import math
import os
from pathlib import Path
import socket
import subprocess
import sys
import time
from confluent_kafka import Consumer, Producer

root=Path(__file__).resolve().parents[2]
source=root.parent/'JIDO/jido_carla_bridge/src'
if not source.exists(): source=Path('/media/ladybug/Dataset1/jido-testes/jido_carla_bridge/src')
sys.path.insert(0,str(source))
from jido_carla_bridge.geodesia import Referencia


def run_case(mode):
    env=dict(os.environ,JIDO_PROFILE='shuttle',JIDO_PLAYER=str(root/'Builds/JidoShuttleValidation/Jido.x86_64'))
    for line in (root/'tools/jido/profiles/shuttle.env').read_text().splitlines():
        if line and not line.startswith('#'): k,v=line.split('=',1); env[k]=v
    env.update(JIDO_TOPIC_PREFIX='sil.',JIDO_TOPIC_PREFIX_ACT='sil.',JIDO_REQUIRE_SENSORS_READY='1',
        JIDO_SHADOW='0',JIDO_MQTT='0',JIDO_GEOFENCE_M='50',JIDO_MAX_SPEED_MS='0.6',
        JIDO_MAX_THROTTLE='0.2',JIDO_WAYPOINT_RADIUS_M='0.5',JIDO_THROTTLE_DEADBAND='0.2',
        JIDO_THROTTLE_DEADBAND_ATE_MS='0.3',JIDO_EMERGENCY_DIST_M='3.0')
    env.pop('JIDO_CMD_TOKEN',None)
    (root/'Logs/jido-validation-mode.txt').write_text(mode)
    group='shuttle-'+mode+'-'+str(time.time_ns()); prefix='sil.jidocab.'
    consumer=Consumer({'bootstrap.servers':'127.0.0.1:9092','group.id':group,
        'auto.offset.reset':'latest','enable.auto.commit':False})
    consumer.subscribe([prefix+n for n in ('core.status','fusion.localization','lidar.obstacles','sensor.imu')])
    producer=Producer({'bootstrap.servers':'127.0.0.1:9092'})
    probe=socket.socket(socket.AF_INET,socket.SOCK_DGRAM); probe.bind(('127.0.0.1',25013)); probe.setblocking(False)
    latest={}; diagnostics=None; states=set(); samples=[]; physical=[]; detected=False; detection_pose=None
    core=None; log=open(root/f'Logs/jido-shuttle-{mode}-core.log','w')
    result={'mode':mode,'passed':False,'speed_limit_mps':.6,'emergency_distance_m':3.}

    def publish(value):
        producer.produce(prefix+'core.route',value=json.dumps(value).encode()); producer.flush(2)

    def pump(seconds):
        nonlocal diagnostics,detected,detection_pose
        end=time.monotonic()+seconds
        while time.monotonic()<end:
            if core is not None and core.poll() is not None: raise RuntimeError('Core exited')
            while True:
                try:
                    diagnostics=json.loads(probe.recvfrom(65535)[0]); physical.append(diagnostics)
                except BlockingIOError: break
            msg=consumer.poll(.03)
            if msg is None or msg.error(): continue
            value=json.loads(msg.value())
            if time.time()-value.get('timestamp',0)>1.5: continue
            name=msg.topic()[len(prefix):]; latest[name]=value
            if name=='core.status': states.add(value.get('state'))
            if name=='fusion.localization':
                v=value['velocity']; samples.append({'time':time.monotonic(),'position':value['position'],
                    'yaw':value['orientation']['yaw'],'speed':math.hypot(v['vx'],v['vy'])})
            if name=='lidar.obstacles' and mode=='obstacle' and value.get('num_obstacles',0)>0 and value.get('fwd_cru_m',-1)>0:
                if not detected: detection_pose=samples[-1] if samples else None
                detected=True

    try:
        subprocess.run(['bash','tools/jido/server.sh','start'],cwd=root,env=env,check=True)
        deadline=time.monotonic()+45
        while (not all(n in latest for n in ('fusion.localization','lidar.obstacles','sensor.imu')) or diagnostics is None) and time.monotonic()<deadline: pump(.2)
        assert diagnostics and 'fusion.localization' in latest,'No live vehicle telemetry'
        pump(3)
        initial=latest['fusion.localization']; x=initial['position']['x']; y=initial['position']['y']; yaw=initial['orientation']['yaw']
        reference=Referencia(**{'lat_deg':initial['enu_ref']['lat'],'lon_deg':initial['enu_ref']['lon'],'alt_m':initial['enu_ref']['alt']})
        points=[]
        sign=1 if mode=='left' else -1
        for i in range(17 if mode!='obstacle' else 25):
            s=i*.5
            bx=10*math.sin(s/10) if mode!='obstacle' else s
            by=sign*10*(1-math.cos(s/10)) if mode!='obstacle' else 0
            e=x+bx*math.cos(yaw)-by*math.sin(yaw); n=y+bx*math.sin(yaw)+by*math.cos(yaw)
            up=-reference.alt
            for _ in range(8): lat,lon,alt=reference.para_wgs84(e,n,up); up-=alt
            lat,lon,_=reference.para_wgs84(e,n,up)
            points.append({'x':e,'y':n,'coordinates':[lon,lat]})
        route={'type':'Feature','properties':{'name':'Shuttle validation '+mode},
            'geometry':{'type':'LineString','coordinates':[p['coordinates'] for p in points]}}
        (root/'routes'/f'jido-shuttle-validation-{mode}.geojson').write_text(json.dumps(route,indent=2)+'\n')
        node=Path('/media/ladybug/Dataset1/jido-testes/jido_core/build-linux/autonomous_core_node')
        core=subprocess.Popen([str(node),'127.0.0.1:9092',group+'-core'],cwd=node.parent,env=env,stdout=log,stderr=subprocess.STDOUT)
        pump(5); assert 'core.status' in latest,'No Core status'
        motion_start=len(samples); physics_start=len(physical)
        publish(route); deadline=time.monotonic()+90; stopped_since=None
        while time.monotonic()<deadline:
            pump(.2)
            status=latest.get('core.status',{}); state=status.get('state')
            if mode!='obstacle' and state in ('ARRIVED','EMERGENCY_STOP'): break
            if mode=='obstacle' and detected and samples[-1]['speed']<.05 and state in ('AVOIDING_OBSTACLE','EMERGENCY_STOP','HOLD'):
                if stopped_since is None: stopped_since=time.monotonic()
                if time.monotonic()-stopped_since>2: break
            else: stopped_since=None
        pump(2)
        moving=samples[motion_start:]; physics=physical[physics_start:]; final=moving[-1]
        delta_yaw=math.atan2(math.sin(final['yaw']-yaw),math.cos(final['yaw']-yaw))
        distance=math.hypot(final['position']['x']-x,final['position']['y']-y)
        result.update(states=sorted(s for s in states if s),initial=initial,final=final,final_status=latest.get('core.status'),
            distance_m=distance,yaw_change_rad=delta_yaw,maximum_speed_mps=max(s['speed'] for s in moving),
            minimum_grounded_wheels=min(p['grounded_wheels'] for p in physics),
            barrier_contact=any(p['barrier_contact'] for p in physics),lidar_barrier_detected=detected)
        assert result['minimum_grounded_wheels']==4,'Wheel lost ground contact'
        assert not result['barrier_contact'],'Physical collision with barrier'
        assert result['maximum_speed_mps']<.8 and final['speed']<.1,'Speed or stop failed'
        if mode=='obstacle':
            assert detected and distance>1,'Barrier detection or initial movement missing'
            front=max(p['position']['z']+abs(p['forward']['z'])*3.8489058/2+abs(p['right']['z'])*2.26090169/2+max(0,p['up']['z']*2.135) for p in physics)
            result['minimum_body_clearance_m']=7.5-front
            result['detection_pose']=detection_pose
            assert result['minimum_body_clearance_m']>.3,'Body stopped too close to barrier'
            assert latest['core.status']['state']!='ARRIVED','Core incorrectly arrived through barrier'
        else:
            result['target_error_m']=math.hypot(final['position']['x']-points[-1]['x'],final['position']['y']-points[-1]['y'])
            result['maximum_radial_error_m']=max(abs(math.hypot((s['position']['x']-x)*math.cos(yaw)+(s['position']['y']-y)*math.sin(yaw),
                -(s['position']['x']-x)*math.sin(yaw)+(s['position']['y']-y)*math.cos(yaw)-sign*10)-10) for s in moving)
            assert 'NAVIGATING' in states and latest['core.status']['state']=='ARRIVED','Curve not completed'
            assert 'EMERGENCY_STOP' not in states and sign*delta_yaw>.4,'Wrong turn direction or emergency'
            assert result['target_error_m']<.7 and result['maximum_radial_error_m']<.5,'Curve tracking error'
        result['passed']=True
    except Exception as exc:
        result['error']=str(exc)
        raise
    finally:
        if core is not None:
            publish({'action':'clear'})
            if core.poll() is None:
                core.terminate()
                try: core.wait(timeout=5)
                except subprocess.TimeoutExpired: core.kill(); core.wait()
        producer.produce(prefix+'actuator.motor',value=json.dumps({'on':False,'throttle':0,'brake':1,'reverse':False}).encode()); producer.flush(2)
        subprocess.run(['bash','tools/jido/server.sh','stop'],cwd=root,env=env,check=False)
        consumer.close(); probe.close(); log.close()
        (root/'Logs/jido-validation-mode.txt').unlink(missing_ok=True)
        result['samples']=samples
        (root/'Logs'/f'jido-shuttle-{mode}-check.json').write_text(json.dumps(result,indent=2)+'\n')
        print(json.dumps({k:v for k,v in result.items() if k not in ('samples','initial','final_status')},indent=2),flush=True)
    return result


if __name__=='__main__':
    results=[]
    for mode in (sys.argv[1:] or ['left','right','obstacle']):
        assert mode in ('left','right','obstacle')
        results.append(run_case(mode))
    (root/'Logs/jido-shuttle-maneuvers-check.json').write_text(json.dumps({'passed':all(r['passed'] for r in results),'cases':results},indent=2)+'\n')
