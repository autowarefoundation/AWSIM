using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Awsim.Jido.Editor
{
    public static class JidoRouteSurvey
    {
        [Serializable] class Survey { public double lat, lon, alt; public Vector3[] points; public string[] surfaces; public float[] crossSlope; }
        [Serializable] class SettledPose { public Vector3 position; public float yaw; }
        public static void Export()
        {
            EditorSceneManager.OpenScene(JidoShuttlePhysicsCreator.CityPath);
            var bridge = UnityEngine.Object.FindFirstObjectByType<JidoUdpVehicleBridge>();
            var data = new SerializedObject(bridge);
            var map = GameObject.Find("Shinjuku");
            var colliders = map.GetComponentsInChildren<Collider>(true);
            var forward = bridge.transform.forward; forward.y = 0; forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            var start = bridge.transform.position - forward * data.FindProperty("vehicleOriginToRearAxleM").floatValue;
            if(Environment.GetEnvironmentVariable("JIDO_ROUTE_USE_SETTLED_POSE")=="1") {
                var settled=JsonUtility.FromJson<SettledPose>(File.ReadAllText("Logs/jido-shinjuku-settled-pose.json"));
                start=new Vector3(-settled.position.y,settled.position.z,settled.position.x);
                forward=new Vector3(-Mathf.Sin(settled.yaw),0,Mathf.Cos(settled.yaw));
                right=Vector3.Cross(Vector3.up,forward);
            }
            var survey = new Survey {
                lat=data.FindProperty("enuReferenceLatitude").doubleValue,
                lon=data.FindProperty("enuReferenceLongitude").doubleValue,
                alt=data.FindProperty("enuReferenceAltitude").doubleValue,
                points=new Vector3[9], surfaces=new string[9], crossSlope=new float[9] };
            for(int i=0;i<9;i++) {
                var p=start+forward*i;
                RaycastHit Ground(Vector3 q) {
                    var ray=new Ray(q+Vector3.up*10,Vector3.down);
                    var hits=colliders.Select(c=>c.Raycast(ray,out var h,30)?(RaycastHit?)h:null)
                        .Where(h=>h.HasValue&&h.Value.normal.y>.9f)
                        .OrderBy(h=>Mathf.Abs(h.Value.point.y-start.y)).ToArray();
                    if(hits.Length==0) throw new InvalidOperationException("Route has no road support at "+q);
                    return hits[0].Value;
                }
                var center=Ground(p); var left=Ground(p-right*1.131f); var r=Ground(p+right*1.131f);
                if(left.collider!=center.collider||r.collider!=center.collider)
                    throw new InvalidOperationException("Vehicle footprint crosses surface boundaries at waypoint "+i);
                survey.points[i]=new Vector3(center.point.z,-center.point.x,center.point.y);
                survey.surfaces[i]=center.collider.name;
                survey.crossSlope[i]=Mathf.Abs(left.point.y-r.point.y)/2.262f;
                if(survey.crossSlope[i]>.08f) throw new InvalidOperationException("Excess cross slope");
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/jido-shinjuku-route-survey.json",JsonUtility.ToJson(survey,true));
        }
    }
}
