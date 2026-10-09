// Copyright 2026 JIDO Autonomy Lab. Licensed under Apache-2.0.
// Minimal ROS-free AWSIM vehicle adapter for the JIDO SIL transport.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Awsim.Entity;
using UnityEngine;

namespace Awsim.Jido
{
    /// <summary>
    /// Attach to a dedicated JIDO scene, not to AutowareSimulationDemo.
    /// Owns the AccelVehicle and IMU lifecycle in that scene.
    /// Unity coordinates are x right, y up, z forward; JIDO is x forward,
    /// y left, z up. Positive JIDO steering is left.
    /// </summary>
    public sealed class JidoUdpVehicleBridge : MonoBehaviour
    {
        [Serializable]
        private sealed class ControlPacket
        {
            public string kind;
            public float throttle;
            public float brake;
            public float steering_deg;
            public bool reverse;
        }

        [Serializable]
        private sealed class LocalizationPacket
        {
            public string kind;
            public double sim_time;
            public LocalizationPayload payload;
        }

        [Serializable]
        private sealed class ImuPacket
        {
            public string kind;
            public double sim_time;
            public ImuPayload payload;
        }

        [Serializable]
        private sealed class Point3
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        private sealed class Velocity3
        {
            public float vx;
            public float vy;
            public float vz;
        }

        [Serializable]
        private sealed class Orientation3
        {
            public float roll;
            public float pitch;
            public float yaw;
        }

        [Serializable]
        private sealed class Uncertainty
        {
            public float position = 0.05f;
        }

        [Serializable]
        private sealed class EnuReference
        {
            public double lat;
            public double lon;
            public double alt;
        }

        [Serializable]
        private sealed class LocalizationPayload
        {
            public Point3 position;
            public Velocity3 velocity;
            public Orientation3 orientation;
            public Uncertainty uncertainty = new Uncertainty();
            public EnuReference enu_ref;
            public string status = "converged";
            public string sensor = "awsim_truth";
        }

        [Serializable]
        private sealed class ImuPayload
        {
            public float ax;
            public float ay;
            public float az;
            public float wx;
            public float wy;
            public float wz;
        }

        [Header("AWSIM components")]
        [SerializeField] private AccelVehicle vehicle;
        [SerializeField] private ImuSensor imu;
        [SerializeField] private Rigidbody vehicleBody;

        [Header("JIDO UDP, same host by default")]
        [SerializeField] private string bridgeHost = "127.0.0.1";
        [SerializeField] private int telemetryPort = 25010;
        [SerializeField] private int commandPort = 25011;
        [SerializeField] private int commandTimeoutMs = 500;
        [SerializeField] private float poseHz = 20f;
        [SerializeField, Min(0f)] private float initialSettlingSeconds;

        [Header("Map and vehicle profile; calibrate before route tests")]
        [SerializeField] private double enuReferenceLatitude;
        [SerializeField] private double enuReferenceLongitude;
        [SerializeField] private double enuReferenceAltitude;
        [SerializeField] private float vehicleOriginToRearAxleM;

        private UdpClient receiver;
        private UdpClient sender;
        private IPEndPoint destination;
        private double lastCommandWallTime;
        private double nextPoseTime;
        private ControlPacket latestControl;
        private double settlingUntil;

        private void Start()
        {
            if (vehicle == null || vehicleBody == null || imu == null)
            {
                Debug.LogError("JIDO bridge needs AccelVehicle, Rigidbody and ImuSensor.");
                enabled = false;
                return;
            }
            if (commandPort < 1 || commandPort > 65535 || telemetryPort < 1 ||
                telemetryPort > 65535 || commandTimeoutMs <= 0 || poseHz <= 0f)
            {
                Debug.LogError("JIDO bridge UDP ports, timeout and poseHz must be positive.");
                enabled = false;
                return;
            }
            if (!IPAddress.TryParse(bridgeHost, out var address))
            {
                Debug.LogError("JIDO bridgeHost must be a numeric IP address.");
                enabled = false;
                return;
            }

            destination = new IPEndPoint(address, telemetryPort);
            receiver = new UdpClient(new IPEndPoint(IPAddress.Any, commandPort));
            receiver.Client.Blocking = false;
            sender = new UdpClient();
            latestControl = new ControlPacket { kind = "control", brake = 1f };
            lastCommandWallTime = double.NegativeInfinity;
            vehicle.Initialize();
            settlingUntil = Time.timeAsDouble + initialSettlingSeconds;
            imu.OnOutput += PublishImu;
            imu.Initialize();
            ApplyControl();
        }

        private void Update()
        {
            if (receiver == null) return;
            // Drain all queued datagrams; only the newest valid command matters.
            while (receiver.Available > 0)
            {
                try
                {
                    var peer = new IPEndPoint(IPAddress.Any, 0);
                    var bytes = receiver.Receive(ref peer);
                    if (!peer.Address.Equals(destination.Address)) continue;
                    var packet = JsonUtility.FromJson<ControlPacket>(Encoding.UTF8.GetString(bytes));
                    if (packet == null || packet.kind != "control" ||
                        !FiniteUnit(packet.throttle) || !FiniteUnit(packet.brake) ||
                        float.IsNaN(packet.steering_deg) || float.IsInfinity(packet.steering_deg))
                        continue;
                    latestControl = packet;
                    lastCommandWallTime = Time.realtimeSinceStartupAsDouble;
                }
                catch (SocketException) { break; }
                catch (ArgumentException) { /* Ignore malformed JSON. */ }
            }
            vehicle.OnUpdate();
        }

        private static bool FiniteUnit(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) &&
                   value >= 0f && value <= 1f;
        }

        private void FixedUpdate()
        {
            if (receiver == null) return;
            ApplyControl();
            // Let native suspension settle before the controller freezes a stopped body.
            if (Time.timeAsDouble >= settlingUntil) vehicle.OnFixedUpdate();
            imu.OnFixedUpdate();
            if (Time.timeAsDouble >= nextPoseTime)
            {
                PublishLocalization();
                nextPoseTime = Time.timeAsDouble + 1.0 / poseHz;
            }
        }

        private void ApplyControl()
        {
            bool stale = Time.realtimeSinceStartupAsDouble - lastCommandWallTime >
                         commandTimeoutMs / 1000.0;
            var command = stale ? new ControlPacket { brake = 1f } : latestControl;
            float acceleration = command.brake > 0f
                ? -command.brake * vehicle.MaxDecelerationInput
                : command.throttle * vehicle.MaxAccelerationInput;
            vehicle.AccelerationInput = acceleration;
            // AWSIM keyboard convention is negative angle for left turn.
            vehicle.SteerTireAngleInput = -command.steering_deg;
            vehicle.GearInput = command.reverse ? Gear.Reverse : Gear.Drive;
        }

        private void PublishLocalization()
        {
            var rearAxle = vehicle.transform.position -
                           vehicle.transform.forward * vehicleOriginToRearAxleM;
            var velocity = vehicleBody.GetPointVelocity(rearAxle);
            var forward = vehicle.transform.forward;
            var right = vehicle.transform.right;
            var up = vehicle.transform.up;
            var payload = new LocalizationPayload
            {
                position = new Point3 { x = rearAxle.z, y = -rearAxle.x, z = rearAxle.y },
                velocity = new Velocity3 { vx = velocity.z, vy = -velocity.x, vz = velocity.y },
                orientation = new Orientation3
                {
                    yaw = Mathf.Atan2(-forward.x, forward.z),
                    pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)),
                    roll = Mathf.Atan2(-right.y, up.y)
                },
                enu_ref = new EnuReference
                {
                    lat = enuReferenceLatitude,
                    lon = enuReferenceLongitude,
                    alt = enuReferenceAltitude
                }
            };
            Send(new LocalizationPacket
            {
                kind = "localizacao", sim_time = Time.timeAsDouble, payload = payload
            });
        }

        private void PublishImu(ImuSensor.IReadOnlyOutputData data)
        {
            // AWSIM optionally adds downward gravity; JIDO/EKF expects specific
            // force (upward +g at rest). Preserve real translational acceleration.
            var gravity = imu.transform.InverseTransformDirection(Physics.gravity);
            var a = data.LinearAcceleration - gravity * (imu.EnableGravity ? 2f : 1f);
            var w = data.AngularVelocity;
            // Angular velocity is an axial vector across the handedness change.
            Send(new ImuPacket
            {
                kind = "imu", sim_time = Time.timeAsDouble,
                payload = new ImuPayload
                {
                    ax = a.z, ay = -a.x, az = a.y,
                    wx = -w.z, wy = w.x, wz = -w.y
                }
            });
        }

        private void Send(object packet)
        {
            if (sender == null) return;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                sender.Send(bytes, bytes.Length, destination);
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"JIDO UDP telemetry send failed: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            if (imu != null) imu.OnOutput -= PublishImu;
            receiver?.Close();
            sender?.Close();
            receiver = null;
            sender = null;
        }
    }
}
