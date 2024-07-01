using UnityEngine;
using ROS2;

namespace AWSIM
{
    /// <summary>
    /// Convert the data output from GnssSensor to ROS2 msg and Publish.
    /// </summary>
    [RequireComponent(typeof(GnssSensor))]
    public class GnssRos2Publisher : MonoBehaviour
    {
        /// <summary>
        /// Topic name in pose msg.
        /// </summary>
        public string poseTopic = "/sensing/gnss/pose";

        /// <summary>
        /// Topic name in poseWithCovarianceStamped msg.
        /// </summary>
        public string poseWithCovarianceStampedTopic = "/sensing/gnss/pose_with_covariance";

        /// <summary>
        /// Gnss sensor frame id.
        /// </summary>
        public string frameId = "gnss_link";

        /// <summary>
        /// QoS settings.
        /// </summary>
        public QoSSettings qosSettings;

        private IPublisher<geometry_msgs.msg.PoseStamped> _posePublisher;
        private IPublisher<geometry_msgs.msg.PoseWithCovarianceStamped> _poseWithCovarianceStampedPublisher;
        private geometry_msgs.msg.PoseStamped _poseMsg;
        private geometry_msgs.msg.PoseWithCovarianceStamped _poseWithCovarianceStampedMsg;
        private GnssSensor _gnssSensor;

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
            _poseMsg = new geometry_msgs.msg.PoseStamped()
            {
                Header = new std_msgs.msg.Header()
                {
                    Frame_id = frameId,
                },
                Pose = new geometry_msgs.msg.Pose(),
            };
            _poseWithCovarianceStampedMsg = new geometry_msgs.msg.PoseWithCovarianceStamped()
            {
                Header = new std_msgs.msg.Header()
                {
                    Frame_id = frameId,
                },
                Pose = new geometry_msgs.msg.PoseWithCovariance(),
            };
            for (int i = 0; i < _poseWithCovarianceStampedMsg.Pose.Covariance.Length; i++)
                _poseWithCovarianceStampedMsg.Pose.Covariance[i] = 0;

            // Create publisher.
            var qos = qosSettings.GetQoSProfile();
            _posePublisher = SimulatorROS2Node.CreatePublisher<geometry_msgs.msg.PoseStamped>(poseTopic, qos);
            _poseWithCovarianceStampedPublisher =
                SimulatorROS2Node.CreatePublisher<geometry_msgs.msg.PoseWithCovarianceStamped>(
                    poseWithCovarianceStampedTopic, qos);
        }

        private void ConnectSensor()
        {
            // Get GnssSensor component.
            _gnssSensor = GetComponent<GnssSensor>();

            // Set callback.
            _gnssSensor.OnOutputData += Publish;
        }

        private void Publish(GnssSensor.OutputData outputData)
        {
            // Converts data output from GnssSensor to ROS2 msg
            _poseMsg.Pose.Position.X = outputData.MgrsPosition.x;
            _poseMsg.Pose.Position.Y = outputData.MgrsPosition.y;
            _poseMsg.Pose.Position.Z = outputData.MgrsPosition.z;
            _poseWithCovarianceStampedMsg.Pose.Pose.Position.X = outputData.MgrsPosition.x;
            _poseWithCovarianceStampedMsg.Pose.Pose.Position.Y = outputData.MgrsPosition.y;
            _poseWithCovarianceStampedMsg.Pose.Pose.Position.Z = outputData.MgrsPosition.z;

            // Update msg header.
            var poseWithCovarianceStampedHeader = _poseWithCovarianceStampedMsg as MessageWithHeader;
            SimulatorROS2Node.UpdateROSTimestamp(ref poseWithCovarianceStampedHeader);
            var poseHeader = _poseMsg as MessageWithHeader;
            SimulatorROS2Node.UpdateROSTimestamp(ref poseHeader);

            // Publish to ROS2.
            _posePublisher.Publish(_poseMsg);
            _poseWithCovarianceStampedPublisher.Publish(_poseWithCovarianceStampedMsg);
        }

        public void ReInitializePublisher()
        {
            CreatePublisher();
            ConnectSensor();
        }

        private void OnDestroy()
        {
            _gnssSensor.OnOutputData -= Publish;
            SimulatorROS2Node.RemovePublisher<geometry_msgs.msg.PoseStamped>(_posePublisher);
            SimulatorROS2Node.RemovePublisher<geometry_msgs.msg.PoseWithCovarianceStamped>(
                _poseWithCovarianceStampedPublisher);
        }
    }
}
