// Copyright 2026 JIDO Autonomy Lab. Licensed under Apache-2.0.
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Awsim.Entity;

namespace Awsim.Jido
{
    /// <summary>Optional test-only physics diagnostics, separate from JIDO sensor transport.</summary>
    public sealed class JidoShuttlePhysicsProbe : MonoBehaviour
    {
        [Serializable] public class Sample
        {
            public string kind = "shuttle_physics";
            public double sim_time;
            public int grounded_wheels;
            public float mass_kg;
            public Vector3 position;
            public Vector3 rotation_deg;
            public float wheel_visual_error_m;
            public bool barrier_contact;
            public float longitudinal_acceleration_mps2;
            public Vector3 forward;
            public Vector3 right;
            public Vector3 up;
            public Vector3 rear_ground_contact;
            public Vector3 imu_position;
            public Vector3 lidar_optical_position;
        }
        private UdpClient sender;
        private double nextSample;
        private bool barrierContact;
        void Start() => sender = new UdpClient();
        void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.name == "JIDO physics barrier") barrierContact = true;
        }
        void LateUpdate()
        {
            if (sender == null || Time.timeAsDouble < nextSample) return;
            nextSample = Time.timeAsDouble + .1;
            var wheels = GetComponentsInChildren<WheelCollider>();
            float error = 0;
            foreach (var wheel in wheels)
            {
                wheel.GetWorldPose(out var position, out _);
                var visual = transform.Find("Wheel visual " + wheel.name);
                if (visual != null) error = Mathf.Max(error, Vector3.Distance(position, visual.position));
            }
            var sample = new Sample { sim_time = Time.timeAsDouble,
                grounded_wheels = wheels.Count(w => w.isGrounded), mass_kg = GetComponent<Rigidbody>().mass,
                position = transform.position, rotation_deg = transform.eulerAngles,
                wheel_visual_error_m = error, barrier_contact = barrierContact,
                longitudinal_acceleration_mps2 = GetComponent<AccelVehicle>().LocalAcceleration.z,
                forward = transform.forward, right = transform.right, up = transform.up,
                imu_position = GetComponentInChildren<ImuSensor>().transform.position };
            var rearWheels = wheels.Where(w => w.name.Contains("Rear")).ToArray();
            var rearPoints = rearWheels.Select(w => w.GetGroundHit(out var hit) ? hit.point : Vector3.zero).ToArray();
            if (rearPoints.Length == 2) sample.rear_ground_contact = (rearPoints[0] + rearPoints[1]) * .5f;
            var lidar = GetComponentInChildren<RGLUnityPlugin.LidarSensor>();
            if (lidar != null)
            {
                var pose = lidar.transform.localToWorldMatrix * lidar.configuration.GetLidarOriginTransfrom();
                sample.lidar_optical_position = pose.GetColumn(3);
            }
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(sample));
            sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, 25013));
        }
        void OnDestroy() => sender?.Close();
    }
}
