"""Run the unchanged Core on the surveyed shuttle route, at low speed."""
import json
import math
import os
from pathlib import Path
import subprocess
import sys
import time
from confluent_kafka import Consumer, Producer


def main():
    root=Path(__file__).resolve().parents[2]
    node=Path('/media/ladybug/Dataset1/jido-testes/jido_core/build-linux/autonomous_core_node')
    route=json.loads((root/'routes/jido-shinjuku-8m.geojson').read_text())
    survey=json.loads((root/'Logs/jido-shinjuku-route-survey.json').read_text())
    prefix='sil.jidocab.'
    group='shinjuku-core-'+str(time.time_ns())
    consumer=Consumer({'bootstrap.servers':'127.0.0.1:9092','group.id':group,
        'auto.offset.reset':'latest','enable.auto.commit':False})
    names=('core.status','fusion.localization','lidar.obstacles','sensor.imu')
    consumer.subscribe([prefix+n for n in names])
    producer=Producer({'bootstrap.servers':'127.0.0.1:9092'})
    env=dict(os.environ)
    for line in (root/'tools/jido/profiles/shuttle.env').read_text().splitlines():
        if line and not line.startswith('#'):
            k,v=line.split('=',1); env[k]=v
    env.update(JIDO_TOPIC_PREFIX='sil.',JIDO_TOPIC_PREFIX_ACT='sil.',
        JIDO_REQUIRE_SENSORS_READY='1',JIDO_SHADOW='0',JIDO_MQTT='0',
        JIDO_MAX_THROTTLE='0.2',JIDO_MAX_SPEED_MS='0.6',JIDO_WAYPOINT_RADIUS_M='0.5',
        JIDO_GEOFENCE_M='400',JIDO_LATERAL_MARGIN_M='0.3',JIDO_FAIXA_LATERAL='1',
        JIDO_THROTTLE_DEADBAND='0.2',JIDO_THROTTLE_DEADBAND_ATE_MS='0.3')
    env.pop('JIDO_CMD_TOKEN',None)
    latest={}; states=set(); maximum_speed=0.; samples=[]
    report={'passed':False,'speed_limit_mps':.6,'geofence_radius_m':400,'route':'routes/jido-shinjuku-8m.geojson'}
    log=open(root/'Logs/jido-shinjuku-core.log','w')
    core=None

    def publish(value):
        producer.produce(prefix+'core.route',value=json.dumps(value).encode()); producer.flush(2)

    def pump(seconds):
        nonlocal maximum_speed
        end=time.monotonic()+seconds
        while time.monotonic()<end:
            if core is not None and core.poll() is not None: raise RuntimeError('Core process exited')
            msg=consumer.poll(.05)
            if msg is None or msg.error(): continue
            value=json.loads(msg.value())
            if time.time()-value.get('timestamp',0)>1.5: continue
            name=msg.topic()[len(prefix):]; latest[name]=value
            if name=='core.status': states.add(value.get('state'))
            if name=='fusion.localization':
                v=value['velocity']; speed=math.hypot(v['vx'],v['vy'])
                maximum_speed=max(maximum_speed,speed)
                if not samples or time.monotonic()-samples[-1]['wall']>.2:
                    samples.append({'wall':time.monotonic(),'position':value['position'],'speed':speed})

    try:
        # Wait for current telemetry before starting a command-producing Core.
        deadline=time.monotonic()+40
        while not all(n in latest for n in names[1:]) and time.monotonic()<deadline: pump(.2)
        assert all(n in latest for n in names[1:]), 'Missing live sensors'
        pump(3)
        pose=latest['fusion.localization']; initial=pose['position']
        (root/'Logs/jido-shinjuku-settled-pose.json').write_text(json.dumps(
            {'position':initial,'yaw':pose['orientation']['yaw'],'enu_ref':pose['enu_ref']},indent=2)+'\n')
        report['initial']=initial
        if '--capture-pose' in sys.argv:
            report['capture_only']=True
            return
        for k in ('lat','lon','alt'):
            assert abs(pose['enu_ref'][k]-survey[k])<1e-9, 'Route/localization origin mismatch'
        start=survey['points'][0]; target=survey['points'][-1]
        error=math.hypot(initial['x']-start['x'],initial['y']-start['y'])
        assert error<.15, 'Settled shuttle is too far from route start: '+str(error)
        direction=math.atan2(target['y']-start['y'],target['x']-start['x'])
        yaw_error=math.atan2(math.sin(pose['orientation']['yaw']-direction),math.cos(pose['orientation']['yaw']-direction))
        assert abs(yaw_error)<.05, 'Route heading mismatch'
        report.update(initial=initial,start_error_m=error,initial_heading_error_rad=yaw_error)
        core=subprocess.Popen([str(node),'127.0.0.1:9092',group+'-node'],env=env,
            cwd=node.parent,stdout=log,stderr=subprocess.STDOUT)
        pump(5)
        assert 'core.status' in latest,'No live Core status'
        publish({'action':'reset_emergency'}); pump(.5)
        publish(route)
        deadline=time.monotonic()+60
        while time.monotonic()<deadline:
            pump(.2)
            state=latest.get('core.status',{}).get('state')
            if state in ('ARRIVED','EMERGENCY_STOP'): break
        pump(2)
        final=latest['fusion.localization']; p=final['position']; v=final['velocity']
        report.update(final=p,final_status=latest.get('core.status'),states=sorted(s for s in states if s),
            distance_m=math.hypot(p['x']-initial['x'],p['y']-initial['y']),
            target_error_m=math.hypot(p['x']-target['x'],p['y']-target['y']),
            final_speed_mps=math.hypot(v['vx'],v['vy']),maximum_speed_mps=maximum_speed)
        assert 'NAVIGATING' in states and latest['core.status']['state']=='ARRIVED','Route not completed'
        assert 'EMERGENCY_STOP' not in states,'Emergency stop occurred'
        assert report['target_error_m']<.7 and report['final_speed_mps']<.1,'Arrival position/stop failed'
        assert maximum_speed<.8,'Unexpected speed'
        report['passed']=True
    except Exception as exc:
        report['error']=str(exc)
        raise
    finally:
        if core is not None:
            publish({'action':'clear'}); publish({'action':'manual_on'})
            publish({'action':'manual_cmd','throttle':0.,'angle':0.})
            if core.poll() is None:
                core.terminate()
                try: core.wait(timeout=5)
                except subprocess.TimeoutExpired: core.kill(); core.wait()
        producer.produce(prefix+'actuator.motor',value=json.dumps({'on':False,'throttle':0,'brake':1,'reverse':False}).encode())
        producer.flush(2); consumer.close(); log.close()
        report['samples']=samples
        (root/'Logs/jido-shinjuku-core-check.json').write_text(json.dumps(report,indent=2)+'\n')
        print(json.dumps({k:v for k,v in report.items() if k not in ('samples','final_status')},indent=2),flush=True)


if __name__=='__main__': main()
