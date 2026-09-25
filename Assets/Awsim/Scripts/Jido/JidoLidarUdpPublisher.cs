// Copyright 2026 JIDO Autonomy Lab. Licensed under Apache-2.0.
// RGL point cloud -> bounded UDP chunks; no ROS 2 publisher.

using System;
using System.Net;
using System.Net.Sockets;
using RGLUnityPlugin;
using UnityEngine;

namespace Awsim.Jido
{
    [RequireComponent(typeof(LidarSensor))]
    public sealed class JidoLidarUdpPublisher : MonoBehaviour
    {
        [SerializeField] private string bridgeHost = "127.0.0.1";
        [SerializeField] private int lidarPort = 25012;
        [SerializeField] private int maxPointsPerChunk = 100;

        private const int HeaderBytes = 14;
        private const int PointBytes = 12;
        private const string YieldNodeId = "JIDO_XYZ";

        private LidarSensor lidar;
        private RGLNodeSequence yieldGraph;
        private UdpClient sender;
        private IPEndPoint destination;
        private Vector3[] points = Array.Empty<Vector3>();
        private uint scanId;

        private void Awake()
        {
            lidar = GetComponent<LidarSensor>();
            yieldGraph = new RGLNodeSequence()
                .AddNodePointsYield(YieldNodeId, RGLField.XYZ_VEC3_F32);
        }

        private void Start()
        {
            if (!IPAddress.TryParse(bridgeHost, out var address) ||
                lidarPort < 1 || lidarPort > 65535 ||
                maxPointsPerChunk < 1 || maxPointsPerChunk > 100)
            {
                Debug.LogError("JIDO LiDAR UDP configuration is invalid.");
                enabled = false;
                return;
            }
            destination = new IPEndPoint(address, lidarPort);
            sender = new UdpClient();
            lidar.ConnectToLidarFrame(yieldGraph);
            lidar.onNewData += Publish;
        }

        private void Publish()
        {
            int count;
            try
            {
                count = yieldGraph.GetResultData<Vector3>(ref points);
            }
            catch (RGLException ex)
            {
                Debug.LogWarning($"JIDO LiDAR read failed: {ex.Message}");
                return;
            }
            if (count <= 0) return;
            int chunks = (count + maxPointsPerChunk - 1) / maxPointsPerChunk;
            if (chunks > ushort.MaxValue)
            {
                Debug.LogWarning("JIDO LiDAR scan exceeds UDP chunk limit.");
                return;
            }
            scanId++;
            for (int index = 0; index < chunks; index++)
            {
                int offset = index * maxPointsPerChunk;
                int chunkCount = Math.Min(maxPointsPerChunk, count - offset);
                var packet = new byte[HeaderBytes + chunkCount * PointBytes];
                packet[0] = (byte)'J'; packet[1] = (byte)'D';
                packet[2] = (byte)'L'; packet[3] = (byte)'1';
                WriteU32Be(packet, 4, scanId);
                WriteU16Be(packet, 8, (ushort)index);
                WriteU16Be(packet, 10, (ushort)chunks);
                WriteU16Be(packet, 12, (ushort)chunkCount);
                for (int i = 0; i < chunkCount; i++)
                {
                    var point = points[offset + i];
                    int at = HeaderBytes + i * PointBytes;
                    WriteF32Le(packet, at, point.x);
                    WriteF32Le(packet, at + 4, point.y);
                    WriteF32Le(packet, at + 8, point.z);
                }
                try
                {
                    sender.Send(packet, packet.Length, destination);
                }
                catch (SocketException ex)
                {
                    Debug.LogWarning($"JIDO LiDAR UDP send failed: {ex.Message}");
                    return;
                }
            }
        }

        private static void WriteU16Be(byte[] bytes, int at, ushort value)
        {
            bytes[at] = (byte)(value >> 8);
            bytes[at + 1] = (byte)value;
        }

        private static void WriteU32Be(byte[] bytes, int at, uint value)
        {
            bytes[at] = (byte)(value >> 24);
            bytes[at + 1] = (byte)(value >> 16);
            bytes[at + 2] = (byte)(value >> 8);
            bytes[at + 3] = (byte)value;
        }

        private static void WriteF32Le(byte[] bytes, int at, float value)
        {
            var raw = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(raw);
            Buffer.BlockCopy(raw, 0, bytes, at, 4);
        }

        private void OnDestroy()
        {
            if (lidar != null) lidar.onNewData -= Publish;
            sender?.Close();
            yieldGraph?.Clear();
        }
    }
}
