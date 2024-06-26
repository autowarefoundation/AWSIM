using UnityEngine;
using ROS2;

namespace AWSIM
{
    /// <summary>
    /// Convert the data output from ImuSensor to ROS2 msg and Publish.
    /// </summary>
    [RequireComponent(typeof(ImuSensor))]
    public class ImuRos2Publisher : MonoBehaviour
    {
        /// <summary>
        /// Topic name in Imu msg.
        /// </summary>
        public string topic = "/sensing/imu/tamagawa/imu_raw";

        /// <summary>
        /// Imu sensor frame id.
        /// </summary>
        public string frameId = "tamagawa/imu_link";

        /// <summary>
        /// QoS settings.
        /// </summary>
        public QoSSettings qosSettings = new QoSSettings()
        {
            ReliabilityPolicy = ReliabilityPolicy.QOS_POLICY_RELIABILITY_RELIABLE,
            DurabilityPolicy = DurabilityPolicy.QOS_POLICY_DURABILITY_VOLATILE,
            HistoryPolicy = HistoryPolicy.QOS_POLICY_HISTORY_KEEP_LAST,
            Depth = 1000,
        };

        private IPublisher<sensor_msgs.msg.Imu> _imuPublisher;
        private sensor_msgs.msg.Imu _imuMsg;
        private ImuSensor _imuSensor;

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
            // create Imu ros msg.
            _imuMsg = new sensor_msgs.msg.Imu()
            {
                Linear_acceleration = new geometry_msgs.msg.Vector3(),
                Angular_velocity = new geometry_msgs.msg.Vector3(),
                Orientation = new geometry_msgs.msg.Quaternion()
                {
                    W = 1,
                    X = 0,
                    Y = 0,
                    Z = 0,
                },
                Header = new std_msgs.msg.Header()
                {
                    Frame_id = frameId,
                }
            };

            // Set covariances to 0.
            for (int i = 0; i < _imuMsg.Angular_velocity_covariance.Length; i++)
                _imuMsg.Angular_velocity_covariance[i] = 0;
            for (int i = 0; i < _imuMsg.Linear_acceleration_covariance.Length; i++)
                _imuMsg.Linear_acceleration_covariance[i] = 0;
            for (int i = 0; i < _imuMsg.Orientation_covariance.Length; i++)
                _imuMsg.Orientation_covariance[i] = 0;

            // Create publisher.
            var qos = qosSettings.GetQoSProfile();
            _imuPublisher = SimulatorROS2Node.CreatePublisher<sensor_msgs.msg.Imu>(topic, qos);
        }

        private void ConnectSensor()
        {
            // Get ImuSensor component.
            _imuSensor = GetComponent<ImuSensor>();

            // Set callback.
            _imuSensor.OnOutputData += Publish;
        }

        private void Publish(ImuSensor.OutputData outputData)
        {
            // Convert from Unity to ROS coordinate system and set the value to msg.
            var rosLinearAcceleration = ROS2Utility.UnityToRosPosition(outputData.LinearAcceleration);
            var rosAngularVelocity = ROS2Utility.UnityToRosAngularVelocity(outputData.AngularVelocity);

            // Update msg.
            _imuMsg.Linear_acceleration.X = rosLinearAcceleration.x;
            _imuMsg.Linear_acceleration.Y = rosLinearAcceleration.y;
            _imuMsg.Linear_acceleration.Z = rosLinearAcceleration.z;
            _imuMsg.Angular_velocity.X = rosAngularVelocity.x;
            _imuMsg.Angular_velocity.Y = rosAngularVelocity.y;
            _imuMsg.Angular_velocity.Z = rosAngularVelocity.z;

            // Update msg header.
            var header = _imuMsg as MessageWithHeader;
            SimulatorROS2Node.UpdateROSTimestamp(ref header);

            // Publish to ROS2.
            _imuPublisher.Publish(_imuMsg);
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
            _imuSensor.OnOutputData -= Publish;
            SimulatorROS2Node.RemovePublisher<sensor_msgs.msg.Imu>(_imuPublisher);
        }
    }
}
