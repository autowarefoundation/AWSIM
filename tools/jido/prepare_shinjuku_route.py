"""Export a surveyed 8 m route using the existing Core's altitude-zero convention."""
import json
import math
import os
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
source = root.parent/'JIDO/jido_carla_bridge/src'
if not source.exists():
    source = Path(os.environ.get('JIDO_GEODESIA_SRC', '/media/ladybug/Dataset1/jido-testes/jido_carla_bridge/src'))
sys.path.insert(0, str(source))
from jido_carla_bridge.geodesia import Referencia

survey = json.loads((root/'Logs/jido-shinjuku-route-survey.json').read_text())
alignment = json.loads((root/'Logs/jido-map-alignment.json').read_text())
reference = Referencia(survey['lat'], survey['lon'], survey['alt'])
coordinates = []
errors = []
for point in survey['points']:
    # Core ignores GeoJSON height and projects at ellipsoid altitude zero.
    # Solve the inverse on that surface while retaining the requested E/N.
    up = -survey['alt']
    for _ in range(8):
        lat, lon, altitude = reference.para_wgs84(point['x'], point['y'], up)
        up -= altitude
    lat, lon, _ = reference.para_wgs84(point['x'], point['y'], up)
    east, north, _ = reference.para_enu(lat, lon, 0)
    errors.append(math.hypot(east-point['x'], north-point['y']))
    coordinates.append([lon, lat])
length = sum(math.hypot(b['x']-a['x'], b['y']-a['y']) for a,b in zip(survey['points'], survey['points'][1:]))
assert max(errors)<.001, 'Route projection differs from simulator position'
assert 7.99<length<8.01, 'Unexpected route length'
route = {'type':'FeatureCollection','features':[{'type':'Feature', 'properties':{
    'name':'JIDO Shinjuku - reta de 8 m', 'enu_ref':{k:survey[k] for k in ('lat','lon','alt')},
    'purpose':'SIL short route', 'frame':'AWSIM simulated ENU',
    'geographic_accuracy':'UTM grid convergence and scale corrected using local WGS84 tangent plane; simulator route, not a surveyed real-world route.'},
    'geometry':{'type':'LineString','coordinates':coordinates}}]}
folder = root/'routes'; folder.mkdir(exist_ok=True)
(folder/'jido-shinjuku-8m.geojson').write_text(json.dumps(route,indent=2)+'\n')
report = {'passed':True,'length_m':length,'waypoints':len(coordinates),
          'maximum_core_projection_error_m':max(errors),'maximum_cross_slope':max(survey['crossSlope']),
          'surfaces':sorted(set(survey['surfaces'])),'survey':survey,
          'map_alignment':alignment,
          'scope':'Simulator route alignment with local UTM-to-ENU rotation/scale, not a surveyed real-world route or autonomous-driving result.'}
(root/'Logs/jido-shinjuku-route-check.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k not in ('survey',)},indent=2))
