// Copyright 2022 Robotec.ai.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using ROS2;
using YamlDotNet.Serialization;
using Awsim.Common;

namespace RGLUnityPlugin
{
    /// <summary>
    /// This object is meant to be a singleton responsible for synchronizing model data between Unity and RGL.
    /// On every frame, it detects changes happening on the scene and reacts appropriately.
    /// It allows to use three different strategies to obtain 3D model for game objects.
    /// Strategies are implemented as private methods IntoRGLObjects* and called via IntoRGLObject delegate.
    ///
    /// Scene Manager will ignore:
    /// - Disabled Game Objects
    /// - Implicitly disabled game objects (e.g. disabled parent, grandparent, etc.)
    /// - Disabled colliders
    /// - Disabled MeshRenderers and SkinnedMeshRenderers
    /// - MRs and SMRs that belong to a disabled LOD
    /// </summary>
    public class SceneManager : MonoBehaviour
    {
        public enum MeshSource
        {
            OnlyColliders,
            RegularMeshesAndCollidersInsteadOfSkinned,
            RegularMeshesAndSkinnedMeshes
        }

        // Singleton pattern
        public static SceneManager Instance { get; private set; }

        [SerializeField]
        private MeshSource meshSource = MeshSource.RegularMeshesAndSkinnedMeshes;

        [SerializeField, Tooltip("Use the Unity simulation clock in scenes that do not initialize ROS 2.")]
        private bool useUnitySimulationTime;
        public bool UseUnitySimulationTime
        {
            get => useUnitySimulationTime;
            set => useUnitySimulationTime = value;
        }

        [SerializeField, Tooltip("Optional hierarchy excluded from ray tracing, such as the sensor's own vehicle.")]
        private Transform ignoredHierarchy;
        public Transform IgnoredHierarchy
        {
            get => ignoredHierarchy;
            set => ignoredHierarchy = value;
        }

        [field: SerializeField, Tooltip("File to save dictionary for GameObjects and their SemanticCategories")]
        private string semanticCategoryDictionaryFile;

        // Getting meshes strategies
        private delegate IEnumerable<IRGLObject> IntoRGLObjectsStrategy(IEnumerable<GameObject> gameObjects);

        private IntoRGLObjectsStrategy IntoRGLObjects;

        // Keeping track of the scene objects
        private HashSet<GameObject> lastFrameGameObjects = new HashSet<GameObject>();
        private readonly Dictionary<GameObject, IRGLObject> uploadedRGLObjects = new Dictionary<GameObject, IRGLObject>();

        // This dictionary keeps tracks of identifier -> instance id of objects that were removed (e.g. temporary NPCs)
        // This is needed to include them in the instance id dictionary yaml saved at the end of simulation.
        // Since categoryId can be changed in the runtime, this is filled only on object removal / simulation end.
        private readonly Dictionary<string, int> semanticDict = new Dictionary<string, int>();

        // Last frame of the simulation in which there was an update.
        public int LastUpdateFrame { get; private set; } = -1;

        // Last FixedUpdate (physics cycle) in the `LastUpdateFrame` in which there was an update.
        public int LastFixedUpdateFrame { get; private set; } = -1;

        private void OnDisable()
        {
            Clear();
        }

        private void OnValidate()
        {
            UpdateMeshSource();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("SceneManager is already on the scene. Removing this component");
                Destroy(this);
                return;
            }
            Instance = this;

            if (IntoRGLObjects == null)
            {
                UpdateMeshSource();
            }
        }

        private void UpdateMeshSource()
        {
            IntoRGLObjectsStrategy UpdatedIntoRGLObjects = meshSource switch
            {
                MeshSource.OnlyColliders => IntoRGLObjectsUsingColliders,
                MeshSource.RegularMeshesAndCollidersInsteadOfSkinned => IntoRGLObjectsHybrid,
                MeshSource.RegularMeshesAndSkinnedMeshes => IntoRGLObjectsUsingMeshes,
                _ => throw new ArgumentOutOfRangeException()
            };

            if (IntoRGLObjects != UpdatedIntoRGLObjects)
            {
                Clear();
                IntoRGLObjects = UpdatedIntoRGLObjects;
                Debug.Log($"RGL mesh source: {meshSource.ToString()}");
            }
        }

        /// <summary>
        /// Find out what changes happened on the scene since the last update and update RGL data.
        /// </summary>
        /// <param name="fixedUpdateInCurrentFrame">Indicates which in turn the FixedUpdate call in the current frame it is.
        ///                                         It enables updating when FixedUpdate is called more frequently than Update.</param>
        public void DoUpdate(int fixedUpdateInCurrentFrame = 0)
        {
            /* TODO(prybicki):
             * Placing this code in Update() might cause an artifact - only undefined subset of
             * spawned / despawned game objects would be visible. (due to the Update()s ordering)
             * A better place would be EarlyUpdate, which would introduce a 1 frame delay,
             * but guarantee consistency (exclude dependency on the Update() ordering)
             * However, EarlyUpdate does not exist, but could be implemented via PlayerLoop.
             * It would be also allow to hide async computations related to RGL - skinning and raytracing.
             */

            // The following snippet is a consequence of inability to easily synchronize with LiDAR publishing.
            // TODO: rework it in the future
            // fixedUpdateFrame was added to deal with situations when FixedUpdate is called more frequently than Update (low frame rate of the simulation).
            // Every FixedUpdate cycle starts a new physics cycle in which game objects change their position/animation.
            // The problem was that lidars updated their position every raytrace execution (fixedUpdate) while RGL scene did not update because it was the same frame of the simulation.
            // In this case, we are raytracing on the same scene changing only lidars position which may overlap with the body of not-updated objects.
            // Now, lidars track its FixedUpdate cycles in the currect frame and pass it as fixedUpdateFrame.
            if (LastUpdateFrame == Time.frameCount && LastFixedUpdateFrame == fixedUpdateInCurrentFrame)
            {
                return; // Already done in this frame and fixed update (physics cycle)
            }

            LastFixedUpdateFrame = fixedUpdateInCurrentFrame;
            LastUpdateFrame = Time.frameCount;

            SynchronizeSceneTime();

            Profiler.BeginSample("Find changes and make TODO list");
            var thisFrameGOs = new HashSet<GameObject>(FindObjectsOfType<GameObject>());
            thisFrameGOs.RemoveWhere(IsNotActiveOrParentHasLidar);

            // Added
            var toAddGOs = new HashSet<GameObject>(thisFrameGOs);
            toAddGOs.ExceptWith(lastFrameGameObjects);
            var toAdd = IntoRGLObjects(toAddGOs).ToArray();
            var toAddTerrain = IntoRGLTerrainObjects(toAddGOs).ToArray();
            if (toAddTerrain.Length != 0)
            {
                toAdd = toAdd.Concat(toAddTerrain).ToArray();
            }

            // Removed
            var toRemoveGOs = new HashSet<GameObject>(lastFrameGameObjects);
            toRemoveGOs.ExceptWith(thisFrameGOs);
            toRemoveGOs.IntersectWith(uploadedRGLObjects.Keys);
            var toRemove = toRemoveGOs.Select((o) => uploadedRGLObjects[o]).ToArray();

            lastFrameGameObjects = thisFrameGOs;
            Profiler.EndSample();

            Profiler.BeginSample("Remove despawned objects");
            foreach (var rglObject in toRemove)
            {
                rglObject.DestroyInRGL();
                uploadedRGLObjects.Remove(rglObject.RepresentedGO);
            }
            Profiler.EndSample();

            Profiler.BeginSample("Mark spawned objects as updated");
            foreach (var rglObject in toAdd)
            {
                // Game Objects must not have duplicate representations.
                // Occasionally, this assertion may fail due to disabled Read/Write setting of the prefab's mesh.
                Assert.IsFalse(uploadedRGLObjects.ContainsKey(rglObject.RepresentedGO));

                uploadedRGLObjects.Add(rglObject.RepresentedGO, rglObject);
            }
            Profiler.EndSample();

            // TODO(prybicki): This part can take up to 8ms on Shinjuku scene, two ideas to optimize it soon:
            // - Implement batch update in RGL
            // - Use Transform.hasChanged to filter out some objects
            Profiler.BeginSample("Update transforms and skinned meshes");
            foreach (var gameRGLObject in uploadedRGLObjects)
            {
                gameRGLObject.Value.Update();
            }
            Profiler.EndSample();
        }

        private void SynchronizeSceneTime()
        {
            UInt64 timeNs;
            if (useUnitySimulationTime)
            {
                timeNs = (UInt64)(Time.timeAsDouble * 1e9);
            }
            else
            {
                AwsimRos2Node.GetTime(out var seconds, out var nanoseconds);
                timeNs = (UInt64)(seconds * 1e9) + nanoseconds;
            }
            RGLNativeAPI.CheckErr(RGLNativeAPI.rgl_scene_set_time(IntPtr.Zero, timeNs));
        }

        private void Clear()
        {
            if (!lastFrameGameObjects.Any() && !uploadedRGLObjects.Any()) return;

            foreach (var rglObject in uploadedRGLObjects)
            {
                rglObject.Value.DestroyInRGL();
            }

            RGLMeshSharingManager.Clear();
            RGLTextureSharingManager.Clear();
            BonesPoseCacheManager.Clear();
            uploadedRGLObjects.Clear();
            lastFrameGameObjects.Clear();
            Debug.Log("RGLSceneManager: cleared");
        }

        private void updateSemanticDict(IRGLObject rglObject)
        {
            if (rglObject.CategoryId.HasValue)
            {
                if (!semanticDict.ContainsKey(rglObject.CategoryName))
                {
                    semanticDict.Add(rglObject.CategoryName, rglObject.CategoryId.Value);
                }
            }
        }

        private void OnApplicationQuit()
        {
            if (string.IsNullOrEmpty(semanticCategoryDictionaryFile))
            {
                return;
            }

            foreach (var rglObject in uploadedRGLObjects.Values)
            {
                updateSemanticDict(rglObject);
            }
            var serializer = new SerializerBuilder().Build();
            var yaml = serializer.Serialize(semanticDict);
            File.WriteAllText(semanticCategoryDictionaryFile, yaml);
            Debug.Log($"Saved semantic category dictionary with {semanticDict.Count} objects at {semanticCategoryDictionaryFile}");
        }

        /// <summary>
        /// Yields a collection of RGL objects based on active colliders found in provided game objects.
        /// This function ignores whether gameObject is active, if filtering is needed, it should be done earlier.
        /// </summary>
        private static IEnumerable<IRGLObject> IntoRGLObjectsUsingColliders(IEnumerable<GameObject> gameObjects)
        {
            foreach (var gameObject in gameObjects)
            {
                if (gameObject.TryGetComponent<Collider>(out var collider))
                {
                    if (!collider.enabled) continue;
                    if (collider.GetType() == typeof(WheelCollider))
                    {
                        // WheelCollider, unlike other types of Colliders, does not have 3D mesh representation.
                        // https://docs.unity3d.com/Manual/class-WheelCollider.html
                        // It is basically a 2D ring, which is used to compute interactions with the ground.
                        // I.e. it does not include any information on tire width, so it's difficult to generate any mesh.
                        // At the moment, WheelColliders are used only in the Lexus Ego vehicle,
                        // therefore, as long as E2E does not support multiple Egos, it won't be a problem.
                        // In the future, collider-based lidar may be dropped at all,
                        // or an additional (inactive) mesh collider may be added along with WheelCollider.
                        continue;
                    }

                    if (collider.GetType() == typeof(TerrainCollider))
                    {
                        // terrain has to be present regardless of the type of IntoRGLObjectsStrategy,
                        // so it is handled separately in the IntoRGLTerrainObjects function
                        continue;
                    }

                    if (RGLObjectHelper.TryCreateRGLObject(collider, out IRGLObject rglObject))
                    {
                        yield return rglObject;
                    }
                }
            }
        }

        /// <summary>
        /// Yields a collection of RGL objects found in provided game objects, based on
        /// - Mesh for non-skinned MeshRenderers
        /// - Set of colliders (if provided) attached to the rootBone and below for SkinnedMeshRenderers.
        /// This function ignores whether gameObject is active, if filtering is needed, it should be done earlier.
        /// </summary>
        private static IEnumerable<IRGLObject> IntoRGLObjectsHybrid(IEnumerable<GameObject> gameObjects)
        {
            var collidersToYield = new HashSet<Collider>();
            foreach (var renderer in GetUniqueRenderersInGameObjects(gameObjects))
            {
                if (renderer is SkinnedMeshRenderer smr)
                {
                    if (smr.rootBone == null)
                    {
                        Debug.LogWarning($"Root bone of {smr.gameObject} is null (hence no colliders), skipping");
                        continue;
                    }
                    // Some SkinnedMeshRenderers may point to a common root bone.
                    var boneColliders = smr.rootBone.gameObject.GetComponentsInChildren<Collider>();
                    var enabledBoneColliders = boneColliders.Where(c => c.enabled);
                    collidersToYield.UnionWith(enabledBoneColliders);
                }

                if (renderer is MeshRenderer mr)
                {
                    if (RGLObjectHelper.TryCreateRGLObject(renderer, out IRGLObject rglObject))
                    {
                        yield return rglObject;
                    }
                }
            }

            foreach (var collider in collidersToYield)
            {
                if (RGLObjectHelper.TryCreateRGLObject(collider, out IRGLObject rglObject))
                {
                    yield return rglObject;
                }
            }
        }

        /// <summary>
        /// Yields a collection of RGL objects found in provided game objects,
        /// based on MeshRenderer and SkinnedMeshRenderer.
        /// This function ignores whether gameObject is active, if filtering is needed, it should be done earlier.
        /// </summary>
        private static IEnumerable<IRGLObject> IntoRGLObjectsUsingMeshes(IEnumerable<GameObject> gameObjects)
        {
            foreach (var renderer in GetUniqueRenderersInGameObjects(gameObjects))
            {
                if (RGLObjectHelper.TryCreateRGLObject(renderer, out IRGLObject rglObject))
                {
                    yield return rglObject;
                }
            }
        }

        private static IEnumerable<IRGLObject> IntoRGLTerrainObjects(IEnumerable<GameObject> gameObjects)
        {
            foreach (var gameObject in gameObjects)
            {
                if (gameObject.TryGetComponent<Terrain>(out var terrain))
                {
                    if (RGLObjectHelper.TryCreateRGLObject(terrain, out IRGLObject rglObject))
                    {
                        yield return rglObject;
                    }
                }
            }
        }

        /// <summary>
        /// Some prefabs have LOD Group component (Level Of Detail)
        /// which implies using different renders based on the distance from the camera.
        /// For raytracing purposes, the best available LOD is used (index: 0).
        /// Some prefabs may not use LOD and use mesh renderers directly.
        /// This function takes the aforementioned into account to output exactly one Renderer per game object.
        /// It is guaranteed that yielded meshes are enabled and are not attached to a disabled LOD.
        /// </summary>
        private static IEnumerable<Renderer> GetUniqueRenderersInGameObjects(IEnumerable<GameObject> gameObjects)
        {
            var lodGroups = new HashSet<LODGroup>();
            var smrs = new HashSet<SkinnedMeshRenderer>();
            var mrs = new HashSet<MeshRenderer>();

            foreach (var gameObject in gameObjects)
            {
                if (gameObject.TryGetComponent<LODGroup>(out var lodGroup))
                {
                    lodGroups.Add(lodGroup);
                }

                if (gameObject.TryGetComponent<SkinnedMeshRenderer>(out var smr) && smr.enabled)
                {
                    smrs.Add(smr);
                }

                bool hasMeshRenderer = gameObject.TryGetComponent<MeshRenderer>(out var mr) && mr.enabled;
                bool hasMeshFilter = gameObject.TryGetComponent<MeshFilter>(out _); // Mesh filter can't be disabled
                if (hasMeshRenderer && hasMeshFilter)
                {
                    mrs.Add(mr);
                }
            }

            foreach (var lodGroup in lodGroups)
            {
                for (int lodIndex = 0; lodIndex < lodGroup.lodCount; ++lodIndex)
                {
                    var lod = lodGroup.GetLODs()[lodIndex];
                    foreach (var renderer in lod.renderers)
                    {
                        // In theory it is possible that the renderer-containing game object is not a descendant of the LoD
                        // therefore we need to additionally check if renderer.gameObject.activeInHierarchy
                        if (lodGroup.enabled && lodIndex == 0 && renderer.enabled && renderer.gameObject.activeInHierarchy)
                        {
                            yield return renderer;
                        }

                        smrs.Remove(renderer as SkinnedMeshRenderer);
                        mrs.Remove(renderer as MeshRenderer);
                    }
                }
            }

            foreach (var smr in smrs) yield return smr;
            foreach (var mr in mrs) yield return mr;
        }

        private bool IsNotActiveOrParentHasLidar(GameObject gameObject)
        {
            return !gameObject.activeInHierarchy ||
                   (ignoredHierarchy != null &&
                    (gameObject.transform == ignoredHierarchy || gameObject.transform.IsChildOf(ignoredHierarchy))) ||
                   gameObject.GetComponentsInParent<LidarSensor>().Length != 0 ||
                   gameObject.GetComponentsInParent<RadarSensor>().Length != 0;
        }
    }
}
