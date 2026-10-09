// Copyright 2026 JIDO Autonomy Lab. Licensed under Apache-2.0.
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using Awsim.Entity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Awsim.Jido.Editor
{
    public static class JidoShuttlePhysicsCreator
    {
        const string Folder = "Assets/Awsim/Models/Entity/Vehicle/EgoVehicle/JidoShuttle";
        const string PrefabPath = Folder + "/JidoShuttleHDRP.prefab";
        public const string TrackPath = "Assets/Awsim/Scenes/Jido/JidoShuttlePhysicsHDRP.unity";
        public const string CityPath = "Assets/Awsim/Scenes/Jido/JidoShinjukuShuttleHDRP.unity";
        public const string SensorsPath = "Assets/Awsim/Scenes/Jido/JidoShuttleSensorsHDRP.unity";
        public const float LidarMountHeightM = 2.3f;
        public const float LidarOpticalOffsetM = .0377f;
        const float MassKg = 1500f; // Provisional baseline, same as the existing CARLA profile.
        const float Spring = 45000f;
        const float Damper = 6200f;
        const float Travel = .22f;
        const float RideHeightCorrection = .0805f; // Measured after native suspension settles on the flat track.

        public static GameObject CreatePrefab()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SK_Jido_F.fbx");
            if (source == null) throw new InvalidOperationException("Import JIDO SK_Jido_F.fbx first.");
            var root = new GameObject("JIDO Shuttle");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "JIDO body and wheel rig";
            model.transform.SetParent(root.transform, false);
            var bones = model.GetComponentsInChildren<Transform>(true);
            Transform Bone(string name) => bones.Single(t => t.name == name);
            var front = Bone("Wheel_Front_Left");
            var rear = Bone("Wheel_Rear_Left");
            var axis = front.position - rear.position;
            axis.y = 0;
            model.transform.rotation = Quaternion.Inverse(Quaternion.LookRotation(axis, Vector3.up));
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (var renderer in renderers)
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                {
                    var material = m == null ? null : AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + m.name.Replace('/', '_') + ".mat");
                    if (material == null) throw new InvalidOperationException("Run JidoShuttlePreview first to prepare HDRP materials.");
                    return material;
                }).ToArray();

            var body = root.AddComponent<Rigidbody>();
            body.mass = MassKg;
            body.centerOfMass = new Vector3(0, .85f, 0);
            body.linearDamping = .02f;
            body.angularDamping = .3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations = 12;
            body.solverVelocityIterations = 6;
            var collision = root.AddComponent<BoxCollider>();
            collision.center = new Vector3(0, (bounds.size.y + .15f) * .5f, 0);
            collision.size = new Vector3(bounds.size.x, bounds.size.y - .15f, bounds.size.z);

            var vehicle = root.AddComponent<AccelVehicle>();
            var settings = new SerializedObject(vehicle);
            settings.FindProperty("_rigidbody").objectReferenceValue = body;
            settings.FindProperty("_maxSteerTireAngleInput").floatValue = 35f;
            settings.FindProperty("_maxAccelerationInput").floatValue = 1.5f;
            settings.FindProperty("_maxDecelerationInput").floatValue = 3.5f;
            settings.FindProperty("_accelerationTimeConstant").floatValue = .15f;
            settings.FindProperty("_steerTireAngleTimeConstant").floatValue = .2f;
            settings.FindProperty("_decelerationCurve").animationCurveValue = AnimationCurve.Linear(0, 0, 10, .2f);
            var report = new System.Text.StringBuilder();
            report.AppendLine("JIDO shuttle: new Rigidbody, body collider and four WheelColliders; no Lexus components.");
            report.AppendLine($"Mass: {MassKg} kg (provisional); COM y=0.85 m (estimated); spring={Spring} N/m; damper={Damper} Ns/m; travel={Travel} m (estimated).");
            report.AppendLine($"Measured body dimensions: {bounds.size}; acceleration limit=1.5 m/s2; braking=3.5 m/s2; steering=35 deg.");
            foreach (var axle in new[] { "Front", "Rear" })
            foreach (var side in new[] { "Left", "Right" })
            {
                string name = "Wheel_" + axle + "_" + side;
                var bone = Bone(name);
                var center = root.transform.InverseTransformPoint(bone.position);
                float radius = axle == "Front" ? .3826416f : .38042635f;
                var wheelObject = new GameObject(name);
                wheelObject.transform.SetParent(root.transform, false);
                // Static equilibrium of the native WheelCollider suspension.
                wheelObject.transform.localPosition = center + Vector3.up * (Travel * .5f - MassKg * 9.81f / (4f * Spring) + RideHeightCorrection);
                var wheel = wheelObject.AddComponent<WheelCollider>();
                wheel.radius = radius;
                wheel.mass = 30f;
                wheel.suspensionDistance = Travel;
                wheel.suspensionSpring = new JointSpring { spring = Spring, damper = Damper, targetPosition = .5f };
                wheel.forceAppPointDistance = .3f;
                var visual = new GameObject("Wheel visual " + name);
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = center;
                bone.SetParent(visual.transform, true);
                string field = (axle == "Front" ? "_frontAxle" : "_rearAxle") + (side == "Left" ? "._leftWheel" : "._rightWheel");
                settings.FindProperty(field + "._wheelCollider").objectReferenceValue = wheel;
                settings.FindProperty(field + "._wheelVisualTransform").objectReferenceValue = visual.transform;
                report.AppendLine($"{name}: measured center={center}; radius={radius} m; suspension mount={wheelObject.transform.localPosition}.");
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            var imuObject = new GameObject("JIDO IMU");
            imuObject.transform.SetParent(root.transform, false);
            imuObject.transform.localPosition = Vector3.up;
            var imu = imuObject.AddComponent<ImuSensor>();
            var bridge = root.AddComponent<JidoUdpVehicleBridge>();
            var adapter = new SerializedObject(bridge);
            adapter.FindProperty("vehicle").objectReferenceValue = vehicle;
            adapter.FindProperty("vehicleBody").objectReferenceValue = body;
            adapter.FindProperty("imu").objectReferenceValue = imu;
            adapter.FindProperty("initialSettlingSeconds").floatValue = 2f;
            float rearAxle = -(root.transform.InverseTransformPoint(Bone("Wheel_Rear_Left").position).z +
                               root.transform.InverseTransformPoint(Bone("Wheel_Rear_Right").position).z) * .5f;
            adapter.FindProperty("vehicleOriginToRearAxleM").floatValue = rearAxle;
            adapter.ApplyModifiedPropertiesWithoutUndo();
            body.ResetInertiaTensor();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/jido-shuttle-physics-profile.txt", report.ToString());
            Directory.CreateDirectory("tools/jido/profiles");
            string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
            float wheelbase = (root.transform.InverseTransformPoint(front.position).z + root.transform.InverseTransformPoint(Bone("Wheel_Front_Right").position).z) * .5f + rearAxle;
            File.WriteAllText("tools/jido/profiles/shuttle.env", $"# Generated from the AWSIM shuttle model and sensor configuration.\nJIDO_BUILD_PROFILE=shuttle\nJIDO_LIDAR_HEIGHT_M={F(LidarMountHeightM + LidarOpticalOffsetM)}\nJIDO_LIDAR_PISO_M=0.10\nJIDO_LIDAR_TETO_M=2.0\nJIDO_VEHICLE_WIDTH_M={F(bounds.size.x)}\nJIDO_WHEELBASE={F(wheelbase)}\nJIDO_PP_EIXO_TRASEIRO_M=0\nJIDO_FENCE_LEN_M={F(bounds.size.z)}\nJIDO_FENCE_WIDTH_M={F(bounds.size.x)}\nJIDO_FENCE_LIDAR_X_M=0\nJIDO_FENCE_LIDAR_Y_M=0\nJIDO_MAX_STEER_DEG=35\n");
            File.WriteAllText("Logs/jido-shuttle-sensor-geometry.json", "{\"pose_frame\":\"rear_axle_ground\",\"rear_axle_offset_m\":" + F(rearAxle) + ",\"lidar_mount_height_m\":" + F(LidarMountHeightM) + ",\"lidar_optical_height_m\":" + F(LidarMountHeightM + LidarOpticalOffsetM) + ",\"imu_height_m\":1,\"imu_forward_of_rear_axle_m\":" + F(rearAxle) + ",\"lidar_forward_of_rear_axle_m\":" + F(rearAxle) + ",\"imu_axes\":\"x_forward_y_left_z_up_specific_force\",\"lidar_axes\":\"x_forward_y_left_z_up_sensor_origin\"}\n");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        public static void CreateTrack()
        {
            var prefab = CreatePrefab();
            JidoTestSceneCreator.CreateShuttleTrackBase();
            var old = UnityEngine.Object.FindFirstObjectByType<JidoUdpVehicleBridge>();
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            var shuttle = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            shuttle.transform.position = new Vector3(0, .15f, 0);
            shuttle.AddComponent<JidoShuttlePhysicsProbe>();
            var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = "JIDO physics barrier";
            barrier.transform.position = new Vector3(0, 1.5f, 20);
            barrier.transform.localScale = new Vector3(60, 3, .5f);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), TrackPath);
        }

        public static void CreateCity()
        {
            var prefab = CreatePrefab();
            JidoShinjukuSceneCreator.Create();
            var old = UnityEngine.Object.FindFirstObjectByType<JidoUdpVehicleBridge>();
            var position = old.transform.position + Vector3.up * (.43f + .15f);
            var rotation = old.transform.rotation;
            var oldSettings = new SerializedObject(old);
            var reference = new[] { "enuReferenceLatitude", "enuReferenceLongitude", "enuReferenceAltitude" }
                .Select(name => oldSettings.FindProperty(name).doubleValue).ToArray();
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            var shuttle = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            shuttle.transform.SetPositionAndRotation(position, rotation);
            var adapter = new SerializedObject(shuttle.GetComponent<JidoUdpVehicleBridge>());
            var names = new[] { "enuReferenceLatitude", "enuReferenceLongitude", "enuReferenceAltitude" };
            for (int i = 0; i < names.Length; i++) adapter.FindProperty(names[i]).doubleValue = reference[i];
            adapter.ApplyModifiedPropertiesWithoutUndo();
            shuttle.AddComponent<JidoShuttlePhysicsProbe>();
            JidoTestSceneCreator.AddLidarHdrp();
            var lidar = shuttle.GetComponentInChildren<RGLUnityPlugin.LidarSensor>();
            lidar.transform.localPosition = new Vector3(0, LidarMountHeightM, 0);
            lidar.transform.localRotation = Quaternion.identity;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), CityPath);
        }

        public static void CreateSensors()
        {
            CreateTrack();
            JidoTestSceneCreator.AddLidarHdrp();
            var lidar = UnityEngine.Object.FindFirstObjectByType<RGLUnityPlugin.LidarSensor>();
            lidar.transform.localPosition = new Vector3(0, LidarMountHeightM, 0);
            lidar.transform.localRotation = Quaternion.identity;
            foreach (var target in new[] { new Vector3(0, .7f, 12), new Vector3(-6, .6f, 8), new Vector3(6, .9f, 8) })
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"JIDO measured sensor target ({target.z}, {-target.x})";
                cube.transform.position = target;
                cube.transform.localScale = new Vector3(1.2f, target.y * 2f, 1.2f);
            }
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), SensorsPath);
        }
    }
}
