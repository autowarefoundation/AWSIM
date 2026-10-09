// Copyright 2026 JIDO Autonomy Lab. Licensed under Apache-2.0.
using System;
using System.IO;
using System.Linq;
using Awsim.Common;
using Awsim.Entity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Awsim.Jido.Editor
{
    public static class JidoShinjukuSceneCreator
    {
        public const string ScenePath = "Assets/Awsim/Scenes/Jido/JidoShinjukuHDRP.unity";

        [MenuItem("JIDO/Create Shinjuku Scene/HDRP")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Awsim/Externals/Shinjuku/Prefabs/Shinjuku.prefab") == null)
                throw new InvalidOperationException("Import Downloads/Shinjuku.unitypackage first.");

            JidoTestSceneCreator.CreateShinjukuBase();
            var target = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var bridge = UnityEngine.Object.FindFirstObjectByType<JidoUdpVehicleBridge>();
            var demo = EditorSceneManager.OpenScene("Assets/Awsim/Scenes/AutowareSimulationDemo.unity", OpenSceneMode.Additive);
            var transforms = demo.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var map = transforms.Single(t => t.name == "Shinjuku");
            var demoVehicle = demo.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AccelVehicle>(true)).Single();
            var spawn = demoVehicle.transform.position;
            var rotation = demoVehicle.transform.rotation;
            var mgrs = map.GetComponentInChildren<MgrsPosition>(true).Mgrs;
            var mgrsString = mgrs.GridZone + ((int)(mgrs.Position.x * 10000)).ToString("D9") + ((int)(mgrs.Position.y * 10000)).ToString("D9");
            var utm = GeographicLib.Geocodes.MGRS.Reverse(mgrsString.AsSpan());
            var geo = GeographicLib.UTMUPS.Reverse(utm.Item1, utm.Item2, utm.Item3, utm.Item4);

            map.SetParent(null, true);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(map.gameObject, target);
            EditorSceneManager.CloseScene(demo, true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(target);
            UnityEngine.Object.DestroyImmediate(GameObject.Find("Test surface 100x100 m"));

            // Raycast against the actual map surface, excluding the ego prefab.
            var ray = new Ray(new Vector3(spawn.x, spawn.y + 10f, spawn.z), Vector3.down);
            var ground = map.GetComponentsInChildren<Collider>(true)
                .Select(c => c.Raycast(ray, out var hit, 30f) ? (RaycastHit?)hit : null)
                .Where(hit => hit.HasValue && hit.Value.normal.y > 0.8f)
                .OrderBy(hit => Mathf.Abs(hit.Value.point.y - (spawn.y + 0.43f)))
                .FirstOrDefault();
            if (!ground.HasValue) throw new InvalidOperationException("No drivable Shinjuku collider below spawn.");
            spawn.y = ground.Value.point.y - 0.43f;
            var alignment = JidoMapAlignment.Apply(map, utm.Item1, utm.Item2,
                utm.Item3, utm.Item4, geo.Item1, geo.Item2, mgrs.Position.z);
            spawn = alignment.TransformPoint(spawn);
            rotation = alignment.rotation * rotation;
            bridge.transform.SetPositionAndRotation(spawn, rotation);
            var serialized = new SerializedObject(bridge);
            serialized.FindProperty("enuReferenceLatitude").doubleValue = geo.Item1;
            serialized.FindProperty("enuReferenceLongitude").doubleValue = geo.Item2;
            serialized.FindProperty("enuReferenceAltitude").doubleValue = mgrs.Position.z;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var camera = target.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single();
            camera.transform.position = spawn - bridge.transform.forward * 14f + Vector3.up * 8f;
            camera.transform.LookAt(spawn + Vector3.up);
            camera.farClipPlane = 1500f;
            JidoTestSceneCreator.AddLidarHdrp();

            var missing = map.GetComponentsInChildren<MeshFilter>(true).Count(m => m.sharedMesh == null);
            if (missing > 0) throw new InvalidOperationException($"Shinjuku has {missing} missing meshes.");
            EditorSceneManager.SaveScene(target, ScenePath);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/jido-shinjuku-scene.txt", $"Scene: {ScenePath}\nMap meshes: {map.GetComponentsInChildren<MeshFilter>(true).Length}\nMap colliders: {map.GetComponentsInChildren<Collider>(true).Length}\nSpawn: {spawn}\nHeading: {rotation.eulerAngles}\nGround: {ground.Value.collider.name}, y={ground.Value.point.y}\nENU anchor: {geo.Item1:R}, {geo.Item2:R}, {mgrs.Position.z}\nVehicle: Lexus RX450h 2015; LiDAR: VLP16; transport: JIDO UDP\nGeoreferenced route alignment requires validation before autonomous routes.\n");
            Debug.Log("JIDO Shinjuku scene saved: " + ScenePath);
        }
    }
}
