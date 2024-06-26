using System;
using ROS2;
using UnityEngine;

namespace AWSIM
{
    /// <summary>
    /// Convert the data output from CameraSensor to ROS2 msg and Publish.
    /// </summary>
    [RequireComponent(typeof(CameraSensor))]
    public class CameraRos2Publisher : MonoBehaviour
    {
        [Header("ROS Topic parameters")]
        /// <summary>
        /// Topic name for Image msg.
        /// </summary>
        public string imageTopic = "/sensing/camera/traffic_light/image_raw";

        /// <summary>
        /// Topic name for CameraInfo msg.
        /// </summary>
        public string cameraInfoTopic = "/sensing/camera/traffic_light/camera_info";

        /// <summary>
        /// Camera sensor frame id.
        /// </summary>
        public string frameId = "traffic_light_left_camera/camera_link";

        /// <summary>
        /// QoS settings.
        /// </summary>
        public QoSSettings qosSettings = new QoSSettings()
        {
            ReliabilityPolicy = ReliabilityPolicy.QOS_POLICY_RELIABILITY_BEST_EFFORT,
            DurabilityPolicy = DurabilityPolicy.QOS_POLICY_DURABILITY_VOLATILE,
            HistoryPolicy = HistoryPolicy.QOS_POLICY_HISTORY_KEEP_LAST,
            Depth = 1,
        };

        // Publishers
        private IPublisher<sensor_msgs.msg.Image> _imagePublisher;
        private IPublisher<sensor_msgs.msg.CameraInfo> _cameraInfoPublisher;
        private sensor_msgs.msg.Image _imageMsg;
        private sensor_msgs.msg.CameraInfo _cameraInfoMsg;

        private CameraSensor _cameraSensor;

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
            // Initialize msgs
            _imageMsg = InitializeEmptyImageMsg();
            _cameraInfoMsg = InitializeEmptyCameraInfoMsg();

            // Create publishers
            var qos = qosSettings.GetQoSProfile();
            _imagePublisher = SimulatorROS2Node.CreatePublisher<sensor_msgs.msg.Image>(imageTopic, qos);
            _cameraInfoPublisher = SimulatorROS2Node.CreatePublisher<sensor_msgs.msg.CameraInfo>(cameraInfoTopic, qos);
        }

        private void ConnectSensor()
        {
            _cameraSensor = GetComponent<CameraSensor>();
            // if (_cameraSensor == null)
            // {
            //     throw new MissingComponentException("No active CameraSensor component found.");
            // }

            // Set callback
            _cameraSensor.OnOutputData += UpdateMessagesAndPublish;
        }

        public void ReInitializePublisher()
        {
            CreatePublisher();
            ConnectSensor();
        }

        private void OnDestroy()
        {
            _cameraSensor.OnOutputData -= UpdateMessagesAndPublish;
            SimulatorROS2Node.RemovePublisher<sensor_msgs.msg.Image>(_imagePublisher);
            SimulatorROS2Node.RemovePublisher<sensor_msgs.msg.CameraInfo>(_cameraInfoPublisher);
        }

        private void UpdateMessagesAndPublish(CameraSensor.OutputData outputData)
        {
            if (!SimulatorROS2Node.Ok())
            {
                return;
            }

            // Update msgs
            UpdateImageMsg(outputData);
            UpdateCameraInfoMsg(outputData.cameraParameters);

            // Update msgs timestamp, timestamps should be synchronized in order to connect image and camera_info msgs
            var timeMsg = SimulatorROS2Node.GetCurrentRosTime();
            _imageMsg.Header.Stamp = timeMsg;
            _cameraInfoMsg.Header.Stamp = timeMsg;

            // Publish to ROS2
            _imagePublisher.Publish(_imageMsg);
            _cameraInfoPublisher.Publish(_cameraInfoMsg);
        }

        private void UpdateImageMsg(CameraSensor.OutputData data)
        {
            if (_imageMsg.Width != data.cameraParameters.width || _imageMsg.Height != data.cameraParameters.height)
            {
                _imageMsg.Width = (uint)data.cameraParameters.width;
                _imageMsg.Height = (uint)data.cameraParameters.height;
                _imageMsg.Step = (uint)(data.cameraParameters.width * 3);

                _imageMsg.Data = new byte[data.cameraParameters.height * data.cameraParameters.width * 3];
            }

            _imageMsg.Data = data.imageDataBuffer;
        }

        private void UpdateCameraInfoMsg(CameraSensor.CameraParameters cameraParameters)
        {
            if (_cameraInfoMsg.Width != cameraParameters.width || _cameraInfoMsg.Height != cameraParameters.height)
            {
                _cameraInfoMsg.Width = (uint)cameraParameters.width;
                _cameraInfoMsg.Height = (uint)cameraParameters.height;
            }

            // Update distortion parameters
            var D = cameraParameters.getDistortionParameters();
            if (!D.Equals(_cameraInfoMsg.D))
            {
                _cameraInfoMsg.D = cameraParameters.getDistortionParameters();
            }

            // Update camera matrix
            var K = cameraParameters.getCameraMatrix();
            if (!K.Equals(_cameraInfoMsg.K))
            {
                for (int i = 0; i < K.Length; i++)
                    _cameraInfoMsg.K[i] = K[i];
            }

            // Update projection matrix
            var P = cameraParameters.getProjectionMatrix();
            if (!P.Equals(_cameraInfoMsg.P))
            {
                for (int i = 0; i < P.Length; i++)
                    _cameraInfoMsg.P[i] = P[i];
            }
        }

        private sensor_msgs.msg.Image InitializeEmptyImageMsg()
        {
            return new sensor_msgs.msg.Image()
            {
                Header = new std_msgs.msg.Header()
                {
                    Frame_id = frameId
                },
                Encoding = "bgr8",
                Is_bigendian = 0,
            };
        }

        private sensor_msgs.msg.CameraInfo InitializeEmptyCameraInfoMsg()
        {
            var message = new sensor_msgs.msg.CameraInfo()
            {
                Header = new std_msgs.msg.Header()
                {
                    Frame_id = frameId
                },
                Distortion_model = "plumb_bob",
                Binning_x = 0,
                Binning_y = 0,
                Roi = new sensor_msgs.msg.RegionOfInterest()
                {
                    X_offset = 0,
                    Y_offset = 0,
                    Height = 0,
                    Width = 0,
                    Do_rectify = false,
                }
            };

            // Set the rectification matrix for monocular camera
            var R = new double[]
            {
                1.0, 0.0, 0.0,
                0.0, 1.0, 0.0,
                0.0, 0.0, 1.0
            };

            for (int i = 0; i < R.Length; i++)
                message.R[i] = R[i];

            return message;
        }
    }
}
