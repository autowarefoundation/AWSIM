// Copyright 2026 JIDO Autonomy Lab. Licensed under Apache-2.0.

using Awsim.Entity;
using RGLUnityPlugin;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Awsim.Jido.Editor
{
    /// <summary>Creates an isolated track for the first JIDO control gate.</summary>
    public static class JidoTestSceneCreator
    {
        private const string VehicleUrp =
            "Assets/Awsim/Prefabs/Entity/EgoVehicle/Vehicle/Urp/Lexus RX450h 2015.prefab";
        private const string VehicleHdrp =
            "Assets/Awsim/Prefabs/Entity/EgoVehicle/Vehicle/Hdrp/Lexus RX450h 2015.prefab";
        private const string SceneFolder = "Assets/Awsim/Scenes/Jido";
        private const string LidarUrp =
            "Assets/Awsim/Prefabs/Entity/EgoVehicle/Sensor/Lidars/Urp/VelodyneVLP16.prefab";
        private const string LidarHdrp =
            "Assets/Awsim/Prefabs/Entity/EgoVehicle/Sensor/Lidars/Hdrp/VelodyneVLP16.prefab";

        [MenuItem("JIDO/Create UDP Test Track/URP")]
        public static void CreateUrp() => Create(VehicleUrp, "JidoUdpTestTrackURP");

        [MenuItem("JIDO/Create UDP Test Track/HDRP")]
        public static void CreateHdrp() => Create(VehicleHdrp, "JidoUdpTestTrackHDRP");

        [MenuItem("JIDO/Add VLP16 to Current Test Track/URP")]
        public static void AddLidarUrp() => AddLidar(LidarUrp);

        [MenuItem("JIDO/Add VLP16 to Current Test Track/HDRP")]
        public static void AddLidarHdrp() => AddLidar(LidarHdrp);

        private static void AddLidar(string prefabPath)
        {
            var bridge = Object.FindFirstObjectByType<JidoUdpVehicleBridge>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (bridge == null || prefab == null)
            {
                Debug.LogError("Open a JIDO test scene and verify the VLP16 prefab exists.");
                return;
            }
            if (Object.FindFirstObjectByType<RGLUnityPlugin.SceneManager>() == null)
                new GameObject("RGL Scene Manager").AddComponent<RGLUnityPlugin.SceneManager>();

            var vehicle = bridge.GetComponentInChildren<AccelVehicle>(true);
            if (vehicle == null)
            {
                Debug.LogError("JIDO scene has no AccelVehicle.");
                return;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "JIDO VLP16";
            instance.transform.SetParent(vehicle.transform, false);
            instance.transform.localPosition = new Vector3(0f, 2f, 0f);
            instance.transform.localRotation = Quaternion.identity;

            var sensor = instance.GetComponent<LidarSensor>();
            if (sensor == null)
            {
                Debug.LogError("VLP16 prefab has no LidarSensor.");
                Object.DestroyImmediate(instance);
                return;
            }
            sensor.AutomaticCaptureHz = 10;
            foreach (var publisher in instance.GetComponentsInChildren<RglLidarPublisher>(true))
                publisher.enabled = false;
            foreach (var publisher in instance.GetComponentsInChildren<LidarUdpPublisher>(true))
                publisher.enabled = false;
            instance.AddComponent<JidoLidarUdpPublisher>();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("JIDO VLP16 added. Save the scene and check LiDAR geometry/height.");
        }

        private static void Create(string prefabPath, string sceneName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"JIDO test scene: vehicle prefab missing: {prefabPath}");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var road = GameObject.CreatePrimitive(PrimitiveType.Plane);
            road.name = "Test surface 100x100 m";
            road.transform.localScale = new Vector3(10f, 1f, 10f);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "JIDO Ego Vehicle";
            instance.transform.position = new Vector3(0f, 0.6f, 0f);
            instance.transform.rotation = Quaternion.identity;

            var vehicle = instance.GetComponentInChildren<AccelVehicle>(true);
            var body = instance.GetComponentInChildren<Rigidbody>(true);
            if (vehicle == null || body == null)
            {
                Debug.LogError("JIDO test scene: vehicle prefab lacks AccelVehicle or Rigidbody.");
                return;
            }

            var imuObject = new GameObject("JIDO IMU");
            imuObject.transform.SetParent(vehicle.transform, false);
            imuObject.transform.localPosition = new Vector3(0f, 1f, 0f);
            var imu = imuObject.AddComponent<ImuSensor>();
            var bridge = instance.AddComponent<JidoUdpVehicleBridge>();
            var serialized = new SerializedObject(bridge);
            serialized.FindProperty("vehicle").objectReferenceValue = vehicle;
            serialized.FindProperty("vehicleBody").objectReferenceValue = body;
            serialized.FindProperty("imu").objectReferenceValue = imu;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var lightObject = new GameObject("Sun");
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var cameraObject = new GameObject("JIDO overview camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 8f, -14f);
            cameraObject.transform.LookAt(instance.transform.position);

            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets/Awsim/Scenes", "Jido");
            var path = $"{SceneFolder}/{sceneName}.unity";
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeGameObject = instance;
            Debug.Log($"JIDO UDP test scene saved: {path}. Start the Python bridge, then press Play.");
        }
    }
}
