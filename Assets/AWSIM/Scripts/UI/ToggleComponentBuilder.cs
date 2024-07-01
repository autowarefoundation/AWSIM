using System;
using System.Collections.Generic;
using RGLUnityPlugin;
using UnityEngine;

namespace AWSIM.Scripts.UI
{
    /// <summary>
    /// Setup components for the toggles
    /// </summary>
    public static class ToggleComponentBuilder
    {
        // Setup Lidar sensor toggles
        public static void BuildToggleComponent(Action<LidarSensor, bool, List<object>> delegateMethod,
            UnityEngine.UI.Toggle toggle, List<object> configList, LidarSensor lidarSensor)
        {
            toggle.onValueChanged.AddListener(delegate { delegateMethod(lidarSensor, toggle.isOn, configList); });
        }

        // Setup sensor visualization toggles
        public static void BuildToggleComponent(Action<MonoBehaviour, bool> delegateMethod,
            UnityEngine.UI.Toggle toggle, MonoBehaviour sensorVisualization)
        {
            toggle.onValueChanged.AddListener(delegate { delegateMethod(sensorVisualization, toggle.isOn); });
        }

        // Setup general sensor toggles
        public static void BuildToggleComponent(Action<Transform, bool, List<object>> delegateMethod,
            UnityEngine.UI.Toggle toggle, List<object> configList, Transform inputTransform)
        {
            toggle.onValueChanged.AddListener(delegate { delegateMethod(inputTransform, toggle.isOn, configList); });
        }
    }
}
