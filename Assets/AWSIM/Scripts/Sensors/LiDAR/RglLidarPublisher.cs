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
using UnityEngine;
using AWSIM.PointCloudFormats;
using RGLUnityPlugin;

namespace AWSIM
{
    /// <summary>
    /// ROS2 publishing component for Robotec GPU Lidar Unity Plugin.
    /// </summary>
    public class RglLidarPublisher : MonoBehaviour
    {
        public string pcl24Topic = "lidar/pointcloud";
        public string pcl48Topic = "lidar/pointcloud_ex";
        public string instanceIdTopic = "lidar/instance_id";
        public string radarTopic = "radar/pointcloud";
        public string frameID = "world";
        public bool publishPCL24 = true;
        public bool publishPCL48 = true;
        public bool publishInstanceId = false;
        public bool publishRadar = false;

        public RGLQosPolicyReliability reliabilityPolicy = RGLQosPolicyReliability.QOS_POLICY_RELIABILITY_BEST_EFFORT;
        public RGLQosPolicyDurability durabilityPolicy = RGLQosPolicyDurability.QOS_POLICY_DURABILITY_VOLATILE;
        public RGLQosPolicyHistory historyPolicy = RGLQosPolicyHistory.QOS_POLICY_HISTORY_KEEP_LAST;
        public int historyDepth = 5;

        private RGLNodeSequence rglSubgraphUnity2Ros;
        private RGLNodeSequence rglSubgraphPcl24;
        private RGLNodeSequence rglSubgraphPcl48;
        private RGLNodeSequence rglSubgraphInstanceId;
        private RGLNodeSequence rglSubgraphRadar;

        private bool _didStart;

        private void Awake()
        {
            CreatePublisher();
        }

        private void Start()
        {
            ConnectSensor();
        }

        private void CreatePublisher()
        {
            rglSubgraphUnity2Ros = new RGLNodeSequence()
                .AddNodePointsTransform("UNITY_TO_ROS", ROS2.Transformations.Unity2RosMatrix4x4());

            if (publishPCL24 && rglSubgraphPcl24 == null)
            {
                rglSubgraphPcl24 = new RGLNodeSequence()
                    .AddNodePointsFormat("PCL24_FORMAT", FormatPCL24.GetRGLFields())
                    .AddNodePointsRos2Publish("PCL24_PUB", pcl24Topic, frameID, reliabilityPolicy, durabilityPolicy,
                        historyPolicy, historyDepth);
                RGLNodeSequence.Connect(rglSubgraphUnity2Ros, rglSubgraphPcl24);
            }

            if (publishPCL48 && rglSubgraphPcl48 == null)
            {
                rglSubgraphPcl48 = new RGLNodeSequence()
                    .AddNodePointsFormat("PCL48_FORMAT", FormatPCL48.GetRGLFields())
                    .AddNodePointsRos2Publish("PCL48_PUB", pcl48Topic, frameID, reliabilityPolicy, durabilityPolicy,
                        historyPolicy, historyDepth);
                RGLNodeSequence.Connect(rglSubgraphUnity2Ros, rglSubgraphPcl48);
            }

            if (publishInstanceId && rglSubgraphInstanceId == null)
            {
                rglSubgraphInstanceId = new RGLNodeSequence()
                    .AddNodePointsFormat("ML_FORMAT", FormatMLInstanceSegmentation.GetRGLFields())
                    .AddNodePointsRos2Publish("ML_PUB", instanceIdTopic, frameID, reliabilityPolicy, durabilityPolicy,
                        historyPolicy, historyDepth);
                RGLNodeSequence.Connect(rglSubgraphUnity2Ros, rglSubgraphInstanceId);
            }

            if (publishRadar && rglSubgraphRadar == null)
            {
                rglSubgraphRadar = new RGLNodeSequence()
                    .AddNodePointsFormat("RADAR_FORMAT", FormatRadarSmartMicro.GetRGLFields())
                    .AddNodePointsRos2Publish("RADAR_PUB", radarTopic, frameID, reliabilityPolicy, durabilityPolicy,
                        historyPolicy, historyDepth);
                RGLNodeSequence.Connect(rglSubgraphUnity2Ros, rglSubgraphRadar);
            }
        }

        private void ConnectSensor()
        {
            if (!publishPCL24 && !publishPCL48 && !publishInstanceId && !publishRadar)
            {
                Debug.LogWarning("All lidar message formats are disabled. Nothing to publish!");
            }

            MonoBehaviour sensor = null;
            // Check if LiDAR is attached
            var lidar = GetComponent<LidarSensor>();
            if (lidar != null)
            {
                lidar.ConnectToLidarFrame(rglSubgraphUnity2Ros);
                sensor = lidar;
            }

            // Check if radar is attached
            var radar = GetComponent<RadarSensor>();
            if (radar != null)
            {
                if (sensor != null)
                {
                    Debug.LogError($"More than one sensor is attached to the publisher. Destroying {name}.");
                    Destroy(this);
                    return;
                }

                radar.ConnectToRadarFrame(rglSubgraphUnity2Ros);
                sensor = radar;
            }

            if (sensor == null)
            {
                Debug.LogError($"Cannot publish point cloud to ROS2 without sensor. Destroying {name}.");
                Destroy(this);
            }

            _didStart = true;
        }

        public void OnValidate()
        {
            if (!enabled)
            {
                return;
            }

            ApplySubgraphState(ref rglSubgraphPcl24, publishPCL24);
            ApplySubgraphState(ref rglSubgraphPcl48, publishPCL48);
            ApplySubgraphState(ref rglSubgraphInstanceId, publishInstanceId);
            ApplySubgraphState(ref rglSubgraphRadar, publishRadar);
        }

        private void OnDisable()
        {
            ApplySubgraphState(ref rglSubgraphPcl24, false);
            ApplySubgraphState(ref rglSubgraphPcl48, false);
            ApplySubgraphState(ref rglSubgraphInstanceId, false);
            ApplySubgraphState(ref rglSubgraphRadar, false);
        }

        public void ReInitializePublisher()
        {
            CreatePublisher();
            ConnectSensor();
        }

        private void OnDestroy()
        {
            rglSubgraphPcl24?.Clear();
            rglSubgraphPcl48?.Clear();
            rglSubgraphInstanceId?.Clear();
            rglSubgraphUnity2Ros?.Clear();
            rglSubgraphRadar?.Clear();
        }

        private void ApplySubgraphState(ref RGLNodeSequence subgraph, bool activateState)
        {
            if (subgraph == null)
            {
                if (_didStart && activateState)
                {
                    Debug.LogWarning(
                        "Cannot activate the publisher because it was not created. Please activate it before the simulation starts.");
                }

                return;
            }

            if (activateState == RGLNodeSequence.AreConnected(rglSubgraphUnity2Ros, subgraph))
            {
                return;
            }

            if (activateState)
            {
                RGLNodeSequence.Connect(rglSubgraphUnity2Ros, subgraph);
            }
            else
            {
                RGLNodeSequence.Disconnect(rglSubgraphUnity2Ros, subgraph);
            }
        }
    }
}
