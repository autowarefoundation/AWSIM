using System;
using UnityEngine;
using ROS2;

namespace AWSIM
{
    /// <summary>
    /// Convert the data output from PoseSensor to ROS2 msg and Publish.
    /// </summary>
    [RequireComponent(typeof(PoseSensor))]
    public class PoseRos2Publisher : MonoBehaviour
    {
        /// <summary>
        /// Topic name in pose msg.
        /// </summary>
        public string Topic = "/awsim/ground_truth/vehicle/pose";

        /// <summary>
        /// Pose sensor frame id.
        /// </summary>
        public string FrameID = "base_link";

        /// <summary>
        /// QoS settings.
        /// </summary>
        public QoSSettings QosSettings = new QoSSettings()
        {
            ReliabilityPolicy = ReliabilityPolicy.QOS_POLICY_RELIABILITY_BEST_EFFORT,
            DurabilityPolicy = DurabilityPolicy.QOS_POLICY_DURABILITY_VOLATILE,
            HistoryPolicy = HistoryPolicy.QOS_POLICY_HISTORY_KEEP_LAST,
            Depth = 1,
        };

        private IPublisher<geometry_msgs.msg.PoseStamped> _poseStampedPublisher;
        private geometry_msgs.msg.PoseStamped _msg;
        private PoseSensor _poseSensor;

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
            // Create msg.
            _msg = new geometry_msgs.msg.PoseStamped()
            {
                Header = new std_msgs.msg.Header()
                {
                    Frame_id = FrameID,
                },
                Pose = new geometry_msgs.msg.Pose(),
            };

            // Create publisher.
            var qos = QosSettings.GetQoSProfile();
            _poseStampedPublisher = SimulatorROS2Node.CreatePublisher<geometry_msgs.msg.PoseStamped>(Topic, qos);
        }

        private void ConnectSensor()
        {
            // Get PoseSensor component.
            _poseSensor = GetComponent<PoseSensor>();

            // Set callback.
            _poseSensor.OnOutputData += Publish;
        }

        private void Publish(PoseSensor.OutputData outputData)
        {
            // Converts data output from GnssSensor to ROS2 msg
            var rosPosition = outputData.Position;
            var rosRotation = outputData.Rotation;

            _msg.Pose.Position.X = rosPosition.x;
            _msg.Pose.Position.Y = rosPosition.y;
            _msg.Pose.Position.Z = rosPosition.z;

            _msg.Pose.Orientation.X = rosRotation.x;
            _msg.Pose.Orientation.Y = rosRotation.y;
            _msg.Pose.Orientation.Z = rosRotation.z;
            _msg.Pose.Orientation.W = rosRotation.w;

            // Update msg header.
            var header = _msg as MessageWithHeader;
            SimulatorROS2Node.UpdateROSTimestamp(ref header);

            // Publish to ROS2.
            _poseStampedPublisher.Publish(_msg);
        }

        public void ReInitializePublisher()
        {
            CreatePublisher();
            ConnectSensor();
        }

        // Note: It takes some time for topic to be removed from Ros2Topic list.
        // Sometimes it doesn't remove the topic.
        private void OnDestroy()
        {
            _poseSensor.OnOutputData -= Publish;
            SimulatorROS2Node.RemovePublisher<geometry_msgs.msg.PoseStamped>(_poseStampedPublisher);
        }
    }
}
