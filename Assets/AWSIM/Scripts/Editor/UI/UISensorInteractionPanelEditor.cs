using AWSIM.Scripts.UI;
using AWSIM.Scripts.UI.Toggle;
using UnityEditor;

namespace AWSIM.Scripts.Editor.UI
{
    [CustomEditor(typeof(UISensorInteractionPanel))]
    public class UISensorInteractionPanelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var script = (UISensorInteractionPanel)target;
            serializedObject.Update();

            // Draw all fields except the sensor lists
            DrawPropertiesExcluding(serializedObject,
                "_lidarSensors",
                "_cameraSensors",
                "_gnssSensors",
                "_imuSensors",
                "_odometrySensors",
                "_poseSensors");

            // Draw the sensor lists based on _buildSensorListManually bool
            var buildSensorListManually = serializedObject.FindProperty("_buildSensorListManually");
            if (buildSensorListManually.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_lidarSensors"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_cameraSensors"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_gnssSensors"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_imuSensors"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_odometrySensors"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_poseSensors"), true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
