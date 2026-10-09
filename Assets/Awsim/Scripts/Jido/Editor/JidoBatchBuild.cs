// Copyright 2026 JIDO Autonomy Lab. Licensed under Apache-2.0.

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Awsim.Jido.Editor
{
    /// <summary>Builds the isolated JIDO track through Unity's -executeMethod.</summary>
    public static class JidoBatchBuild
    {
        public static void BuildLinux()
        {
            string selected = Environment.GetEnvironmentVariable("JIDO_SCENE");
            bool shuttleTrack = selected == "shuttle-physics";
            bool shuttleCity = selected == "shuttle-shinjuku";
            bool shuttleSensors = selected == "shuttle-sensors";
            bool shinjuku = selected == "shinjuku";
            bool validation = selected == "shuttle-validation";
            if (validation) JidoValidationScene.Create();
            else if (shuttleSensors) JidoShuttlePhysicsCreator.CreateSensors();
            else if (shuttleTrack) JidoShuttlePhysicsCreator.CreateTrack();
            else if (shuttleCity) JidoShuttlePhysicsCreator.CreateCity();
            else if (shinjuku) JidoShinjukuSceneCreator.Create();
            else JidoTestSceneCreator.CreateHdrp();
            string scene = shuttleSensors ? JidoShuttlePhysicsCreator.SensorsPath : shuttleTrack ? JidoShuttlePhysicsCreator.TrackPath : shuttleCity ? JidoShuttlePhysicsCreator.CityPath : shinjuku ? JidoShinjukuSceneCreator.ScenePath : "Assets/Awsim/Scenes/Jido/JidoUdpTestTrackHDRP.unity";
            if(validation) scene=JidoValidationScene.Path;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scene || !File.Exists(scene))
                throw new InvalidOperationException("JIDO scene creation failed.");

            if (!validation && !shinjuku && !shuttleTrack && !shuttleCity && !shuttleSensors && Environment.GetEnvironmentVariable("JIDO_WITH_LIDAR") != "0")
            {
                JidoTestSceneCreator.AddLidarHdrp();
                if (UnityEngine.Object.FindFirstObjectByType<JidoLidarUdpPublisher>() == null)
                    throw new InvalidOperationException("JIDO LiDAR creation failed.");
                // A measured scene object verifies perception without obstructing the short route.
                var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = "JIDO LiDAR reference obstacle (ENU 10, 8)";
                obstacle.transform.position = new Vector3(-8f, 1f, 10f);
                obstacle.transform.localScale = new Vector3(1f, 2f, 1f);
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }

            string output = Environment.GetEnvironmentVariable("JIDO_BUILD_DIR") ?? "Builds/Jido";
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = output + "/Jido.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"JIDO build failed: {report.summary.result}");
        }
    }
}
