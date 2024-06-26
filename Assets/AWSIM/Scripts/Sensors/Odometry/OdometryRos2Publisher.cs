using UnityEngine;
using ROS2;

namespace AWSIM
{
    /// <summary>
    /// Convert the data output from Odometry Sensor to ROS2 msg and Publish.
    /// </summary>
    [RequireComponent(typeof(OdometrySensor))]
    public class OdometryRos2Publisher : MonoBehaviour
    {
        /// <summary>
        /// Topic name in pose msg.
        /// </summary>
        public string Topic = "/awsim/ground_truth/localization/kinematic_state";

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

        private IPublisher<nav_msgs.msg.Odometry> _odometryPublisher;
        private nav_msgs.msg.Odometry _msg;
        private OdometrySensor _odometrySensor;

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
            _msg = new nav_msgs.msg.Odometry()
            {
                Header = new std_msgs.msg.Header()
                {
                    Frame_id = FrameID,
                },
                Child_frame_id = "",
                Pose = new geometry_msgs.msg.PoseWithCovariance(),
                Twist = new geometry_msgs.msg.TwistWithCovariance(),
            };

            // Create publisher.
            var qos = QosSettings.GetQoSProfile();
            _odometryPublisher = SimulatorROS2Node.CreatePublisher<nav_msgs.msg.Odometry>(Topic, qos);
        }

        private void ConnectSensor()
        {
            // Get OdometrySensor component.
            _odometrySensor = GetComponent<OdometrySensor>();

            // Set callback.
            _odometrySensor.OnOutputData += Publish;
        }

        private void Publish(OdometrySensor.OutputData outputData)
        {
            // Converts data output from Pose to ROS2 msg
            var rosPosition = outputData.Position;
            var rosRotation = outputData.Rotation;

            // TODO: Add double[36] covariance
            _msg.Pose.Pose.Position.X = rosPosition.x;
            _msg.Pose.Pose.Position.Y = rosPosition.y;
            _msg.Pose.Pose.Position.Z = rosPosition.z;

            _msg.Pose.Pose.Orientation.X = rosRotation.x;
            _msg.Pose.Pose.Orientation.Y = rosRotation.y;
            _msg.Pose.Pose.Orientation.Z = rosRotation.z;
            _msg.Pose.Pose.Orientation.W = rosRotation.w;

            // Converts data output from Twist to ROS2 msg
            var rosLinearVelocity = ROS2Utility.UnityToRosPosition(outputData.linearVelocity);
            var rosAngularVelocity = ROS2Utility.UnityToRosAngularVelocity(outputData.angularVelocity);
            _msg.Twist.Twist.Linear.X = rosLinearVelocity.x;
            _msg.Twist.Twist.Linear.Y = rosLinearVelocity.y;
            _msg.Twist.Twist.Linear.Z = rosLinearVelocity.z;

            _msg.Twist.Twist.Angular.X = rosAngularVelocity.x;
            _msg.Twist.Twist.Angular.Y = rosAngularVelocity.y;
            _msg.Twist.Twist.Angular.Z = rosAngularVelocity.z;

            // Add covariance 6x6
            const int size = 6;
            for (int i = 0; i < size; i++)
            {
                _msg.Pose.Covariance[i * size + i] = 1;
                _msg.Twist.Covariance[i * size + i] = 1;
            }

            // Update msg header.
            var header = _msg as MessageWithHeader;
            SimulatorROS2Node.UpdateROSTimestamp(ref header);

            _msg.Child_frame_id = "base_link";

            // Publish to ROS2.
            _odometryPublisher.Publish(_msg);
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
            _odometrySensor.OnOutputData -= Publish;
            SimulatorROS2Node.RemovePublisher<nav_msgs.msg.Odometry>(_odometryPublisher);
        }
    }
}
