using System;
using System.Collections.Generic;
using AWSIM.Scripts.Utilities;
using RGLUnityPlugin;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AWSIM.Scripts.UI
{
    /// <summary>
    /// Sensor toggle functions
    /// </summary>
    public static class SensorToggleFunctions
    {
        // Toggle Lidar sensor
        public static void ToggleSensor(LidarSensor lidarSensor, bool isOn, List<object> configList)
        {
            if (isOn)
            {
                var newPublisher = lidarSensor.gameObject.AddComponent<RglLidarPublisher>();
                Ros2PublisherUtilities.ConfigAssigner(newPublisher, configList);
                newPublisher.ReInitializePublisher();

                lidarSensor.gameObject.SetActive(true);
                lidarSensor.enabled = true;
            }
            else
            {
                lidarSensor.gameObject.SetActive(false);
                lidarSensor.enabled = false;
                Object.Destroy(lidarSensor.GetComponent<RglLidarPublisher>());
            }
        }

        // Toggle sensor visualization
        public static void ToggleSensor(MonoBehaviour visualization, bool isOn)
        {
            visualization.enabled = isOn;
        }

        // Toggle sensors with provided transforms
        public static void ToggleSensor(Transform sensor, bool isOn, List<object> configList)
        {
            if (isOn)
            {
                sensor.gameObject.SetActive(true);
                switch (sensor.tag)
                {
                    case "CameraSensor":
                        {
                            var publisher = sensor.gameObject.AddComponent<CameraRos2Publisher>();
                            Ros2PublisherUtilities.ConfigAssigner(publisher, configList);
                            publisher.ReInitializePublisher();
                        }
                        break;
                    case "GNSSSensor":
                        {
                            var publisher = sensor.gameObject.AddComponent<GnssRos2Publisher>();
                            Ros2PublisherUtilities.ConfigAssigner(publisher, configList);
                            publisher.ReInitializePublisher();
                        }
                        break;
                    case "IMUSensor":
                        {
                            var publisher = sensor.gameObject.AddComponent<ImuRos2Publisher>();
                            Ros2PublisherUtilities.ConfigAssigner(publisher, configList);
                            publisher.ReInitializePublisher();
                        }
                        break;
                    case "OdometrySensor":
                        {
                            var publisher = sensor.gameObject.AddComponent<OdometryRos2Publisher>();
                            Ros2PublisherUtilities.ConfigAssigner(publisher, configList);
                            publisher.ReInitializePublisher();
                        }
                        break;
                    case "PoseSensor":
                        {
                            var publisher = sensor.gameObject.AddComponent<PoseRos2Publisher>();
                            Ros2PublisherUtilities.ConfigAssigner(publisher, configList);
                            publisher.ReInitializePublisher();
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                sensor.gameObject.SetActive(false);
                var publisher = sensor.tag switch
                {
                    "CameraSensor" => typeof(CameraRos2Publisher),
                    "GNSSSensor" => typeof(GnssRos2Publisher),
                    "IMUSensor" => typeof(ImuRos2Publisher),
                    "OdometrySensor" => typeof(OdometryRos2Publisher),
                    "PoseSensor" => typeof(PoseRos2Publisher),
                    _ => throw new ArgumentOutOfRangeException()
                };
                Object.Destroy(sensor.GetComponent(publisher));
            }
        }
    }
}
