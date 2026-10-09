// Copyright 2026 JIDO Autonomy Lab. Licensed under Apache-2.0.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Awsim.Jido.Editor
{
    /// <summary>Visual preview only; retains the validated Lexus physics.</summary>
    public static class JidoShuttlePreview
    {
        const string Folder = "Assets/Awsim/Models/Entity/Vehicle/EgoVehicle/JidoShuttle";
        public const string ScenePath = "Assets/Awsim/Scenes/Jido/JidoShinjukuShuttlePreviewHDRP.unity";

        public static void CreateAndRender()
        {
            EditorSceneManager.OpenScene(JidoShinjukuSceneCreator.ScenePath);
            var bridge = UnityEngine.Object.FindFirstObjectByType<JidoUdpVehicleBridge>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SK_Jido_F.fbx");
            if (source == null) throw new InvalidOperationException("JIDO shuttle FBX missing.");
            foreach (var renderer in bridge.GetComponentsInChildren<Renderer>(true))
                if (renderer.GetComponentInParent<RGLUnityPlugin.LidarSensor>() == null)
                    renderer.enabled = false;

            var shuttle = (GameObject)PrefabUtility.InstantiatePrefab(source);
            shuttle.name = "JIDO Shuttle visual (Lexus physics pending calibration)";
            shuttle.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var bounds = BoundsOf(shuttle);
            if (bounds.size.x > bounds.size.z)
                shuttle.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            bounds = BoundsOf(shuttle);
            float scale = 3.849f / bounds.size.z;
            shuttle.transform.localScale *= scale;
            bounds = BoundsOf(shuttle);
            var offset = new Vector3(-bounds.center.x, 0.43f - bounds.min.y, -bounds.center.z);
            shuttle.transform.SetParent(bridge.transform, false);
            shuttle.transform.localPosition = offset;

            string materialFolder = Folder + "/Materials";
            if (!AssetDatabase.IsValidFolder(materialFolder)) AssetDatabase.CreateFolder(Folder, "Materials");
            foreach (var renderer in shuttle.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                {
                    var name = original == null ? "body" : original.name;
                    var path = materialFolder + "/" + name.Replace('/', '_') + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        material = new Material(Shader.Find("HDRP/Lit")) { name = name };
                        var key = name.ToLowerInvariant();
                        var color = original != null ? original.color : Color.gray;
                        if (key.Contains("purple_paint")) color = key.Contains("001") ? new Color(.35f,.04f,.28f) : new Color(.085f,.018f,.20f);
                        else if (key.Contains("white_paint")) color = new Color(.72f,.72f,.76f);
                        else if (key.Contains("glass") || key == "material.005") color = new Color(.015f,.018f,.025f);
                        else if (key.Contains("rubber") || key.Contains("tire") || key.Contains("belt")) color = new Color(.02f,.02f,.02f);
                        material.SetColor("_BaseColor", color);
                        material.SetFloat("_Smoothness", key.Contains("glass") ? .95f : .6f);
                        material.SetFloat("_Metallic", key.Contains("paint") ? .3f : 0f);
                        // Some panels in the supplied design mesh have mixed winding.
                        material.SetFloat("_DoubleSidedEnable", 1f);
                        material.EnableKeyword("_DOUBLESIDED_ON");
                        material.SetFloat("_CullMode", 0f);
                        material.SetFloat("_CullModeForward", 0f);
                        HDMaterial.ValidateMaterial(material);
                        AssetDatabase.CreateAsset(material, path);
                    }
                    return material;
                }).ToArray();
            }

            var lidar = bridge.GetComponentInChildren<RGLUnityPlugin.LidarSensor>(true);
            lidar.transform.localPosition = new Vector3(0f, 0.43f + bounds.size.y + .15f, 0f);
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera.GetComponent<HDAdditionalCameraData>() == null) camera.gameObject.AddComponent<HDAdditionalCameraData>();
            var focus = bridge.transform.position + Vector3.up * 1.6f;
            camera.transform.position = focus + bridge.transform.TransformDirection(new Vector3(6.5f, 3f, 8.5f));
            camera.transform.LookAt(focus);
            camera.fieldOfView = 45;
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                if (light.GetComponent<HDAdditionalLightData>() == null) light.gameObject.AddComponent<HDAdditionalLightData>();
                light.intensity = 100000f;
            }
            var volumeObject = new GameObject("JIDO preview exposure");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            var profilePath = Folder + "/PreviewExposure.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            if (!profile.TryGet<Exposure>(out var exposure)) exposure = profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(13f);
            volume.sharedProfile = profile;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/jido-shuttle-preview.txt", $"Source: SK_Jido_F.fbx\nVisual dimensions: {bounds.size}\nVisual scale: {scale}\nPhysics: Lexus placeholder, not calibrated JIDO dynamics\nScene: {ScenePath}\n");

            var texture = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
            texture.Create();
            for (int frame = 0; frame < 20; frame++)
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = texture });
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            File.WriteAllBytes("Logs/jido-shuttle-shinjuku.png", image.EncodeToPNG());
            RenderTexture.active = null;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(image);
            Debug.Log("JIDO shuttle preview rendered.");
        }

        static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("JIDO model has no renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
