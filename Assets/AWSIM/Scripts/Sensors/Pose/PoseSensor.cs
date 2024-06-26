using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AWSIM
{
    /// <summary>
    /// Outputs the true value of pose.
    /// TODO: Currently the Mgrs coordinate system is output, but refactor the Environment to Unity coordinate system + offset.
    /// </summary>
    public class PoseSensor : MonoBehaviour
    {
        /// <summary>
        /// This data is output from PoseSensor at the OutputHz cycle.
        /// </summary>
        public class OutputData
        {
            public Vector3 Position;
            public Quaternion Rotation;

            public OutputData()
            {
                Position = new Vector3();
                Rotation = new Quaternion();
            }
        }

        /// <summary>
        /// Data output hz.
        /// Sensor processing and callbacks are called in this hz.
        /// </summary>
        [Range(0, 100)]
        public int OutputHz = 100;

        /// <summary>
        /// Delegate used in callbacks.
        /// </summary>
        public delegate void OnOutputDataDelegate(OutputData outputData);

        /// <summary>
        /// Called each time data is output.
        /// </summary>
        public OnOutputDataDelegate OnOutputData;

        float timer = 0;
        OutputData outputData = new OutputData();
        Transform m_transform;

        void Start()
        {
            m_transform = transform;
        }

        void FixedUpdate()
        {
            // Matching output to hz.
            timer += Time.deltaTime;
            var interval = 1.0f / OutputHz;
            interval -= 0.00001f;       // Allow for accuracy errors.
            if (timer < interval)
                return;
            timer = 0;

            // update ground truth position and rotation.
            var rosPosition = ROS2Utility.UnityToRosPosition(m_transform.position);
            outputData.Position = rosPosition + Environment.Instance.MgrsOffsetPosition;     // TODO: Handled in Unity coordinate system
                                                                                             // ros gnss sensor's pos + mgrs offset pos.
            var rosRotation = ROS2Utility.UnityToRosRotation(m_transform.rotation);
            outputData.Rotation = rosRotation;

            // Calls registered callbacks
            OnOutputData.Invoke(outputData);
        }
    }
}
