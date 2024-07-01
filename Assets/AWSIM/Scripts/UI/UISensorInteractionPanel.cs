using System;
using System.Collections.Generic;
using System.Linq;
using AWSIM.Scripts.Utilities;
using RGLUnityPlugin;
using UnityEngine;
using UnityEngine.UI;

namespace AWSIM.Scripts.UI
{
    public class UISensorInteractionPanel : MonoBehaviour
    {
        [SerializeField] private UICard _uiCard;
        [SerializeField] private GameObject _horizontalCardGroupPrefab;
        [SerializeField] private float _horizontalCardGroupHeight = 15f;
        [SerializeField] private float _horizontalCardGroupSpacing = 2.5f;
        [SerializeField] private GameObject _verticalCardGroupPrefab;
        [SerializeField] private GameObject _togglePrefab;
        [SerializeField] private GameObject _toggleVisualizationPrefab;
        [SerializeField] private GameObject _textPrefab;

        private List<Tuple<Transform, Transform>> _lidarAndParentPairs;

        [SerializeField] private bool _buildSensorListManually;

        [SerializeField]
        private List<Transform> _lidarSensors,
            _cameraSensors,
            _gnssSensors,
            _imuSensors,
            _odometrySensors,
            _poseSensors;

        private List<List<Transform>> _listOfSensorLists;
        private GameObject _togglesPanel;
        private bool _uiTabReady;

        private void Start()
        {
            // Build sensor list based on user preference
            if (!_buildSensorListManually)
            {
                SensorListAutomaticBuilder();
            }

            // Create list for sensor lists.
            _listOfSensorLists = new List<List<Transform>>
            {
                _lidarSensors,
                _cameraSensors,
                _gnssSensors,
                _imuSensors,
                _odometrySensors,
                _poseSensors
            };
        }

        private void Update()
        {
            if (!_uiTabReady)
            {
                SetupUICard(_uiCard);
                _uiTabReady = true;
            }
        }

        private void SetupUICard(UICard card)
        {
            _togglesPanel = Instantiate(_verticalCardGroupPrefab, card.transform);
            // Insert panel to the hierarchy
            _togglesPanel.transform.SetSiblingIndex(1);
            var togglePanelLayoutGroup = _togglesPanel.GetComponent<LayoutGroup>();
            _togglesPanel.name = "Sensor Toggles";

            // Build tab elements
            foreach (var sensorList in _listOfSensorLists.Where(sensorList => sensorList != null))
            {
                CreateSensorGroups(sensorList);
            }

            // Calculate the preferred height of the panel
            var panelVerticalOffsets = togglePanelLayoutGroup.padding.top + togglePanelLayoutGroup.padding.bottom;
            var togglePanelPreferredHeight = (_lidarSensors.Count + _cameraSensors.Count + _gnssSensors.Count +
                                              _imuSensors.Count + _odometrySensors.Count + _poseSensors.Count) *
                _horizontalCardGroupHeight + panelVerticalOffsets;
            _togglesPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(0, togglePanelPreferredHeight);

            // Recalculate the card height
            card.RecalculateTabBackgroundHeight();
        }

        /// Create toggle groups
        private void CreateSensorGroups(List<Transform> transformList)
        {
            foreach (var sensorTf in transformList)
            {
                //create group
                var cardGroup = Instantiate(_horizontalCardGroupPrefab, _togglesPanel.transform);
                cardGroup.GetComponent<RectTransform>()
                    .SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _horizontalCardGroupHeight);
                cardGroup.GetComponent<LayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                cardGroup.GetComponent<HorizontalLayoutGroup>().spacing = _horizontalCardGroupSpacing;

                //create toggle
                switch (sensorTf.tag)
                {
                    case "LidarSensor":
                        {
                            //create toggle
                            var sensor = sensorTf.GetComponent<LidarSensor>();
                            var toggleObject = Instantiate(_togglePrefab, cardGroup.transform);
                            var toggle = toggleObject.GetComponent<UnityEngine.UI.Toggle>();
                            toggle.isOn = sensor.enabled; // Set initial state
                            var publisher = sensor.GetComponent<RglLidarPublisher>();
                            var configList = Ros2PublisherUtilities.ConfigListBuilder(publisher); // Get publisher config
                            ToggleComponentBuilder.BuildToggleComponent(SensorToggleFunctions.ToggleSensor, toggle,
                                configList, sensor);

                            //get lidar visualization
                            var visualization = sensorTf.GetComponent<PointCloudVisualization>();
                            var visualizationToggleObject = Instantiate(_toggleVisualizationPrefab, cardGroup.transform);
                            var visualizationToggle = visualizationToggleObject.GetComponent<UnityEngine.UI.Toggle>();
                            visualizationToggle.isOn = visualization.enabled; // Set initial state
                            ToggleComponentBuilder.BuildToggleComponent(SensorToggleFunctions.ToggleSensor,
                                visualizationToggle, visualization);
                        }
                        break;
                    case "CameraSensor":
                        {
                            //create toggle
                            var toggleObject = Instantiate(_togglePrefab, cardGroup.transform);
                            var toggle = toggleObject.GetComponent<UnityEngine.UI.Toggle>();
                            var sensorPublisher = sensorTf.GetComponent<CameraRos2Publisher>();
                            var configList =
                                Ros2PublisherUtilities.ConfigListBuilder(sensorPublisher); // Get publisher config
                            toggle.isOn = sensorTf.gameObject.activeSelf; // Set initial state
                            ToggleComponentBuilder.BuildToggleComponent(SensorToggleFunctions.ToggleSensor, toggle,
                                configList, sensorTf);

                            //get camera visualization
                            var visualization = sensorTf.GetComponent<UICameraBridge>();
                            var visualizationToggleObject = Instantiate(_toggleVisualizationPrefab, cardGroup.transform);
                            var visualizationToggle = visualizationToggleObject.GetComponent<UnityEngine.UI.Toggle>();
                            visualizationToggle.isOn = visualization.enabled; // Set initial state
                            ToggleComponentBuilder.BuildToggleComponent(SensorToggleFunctions.ToggleSensor,
                                visualizationToggle, visualization);
                        }
                        break;
                    default:
                        {
                            //create toggle
                            var toggleObject = Instantiate(_togglePrefab, cardGroup.transform);
                            var toggle = toggleObject.GetComponent<UnityEngine.UI.Toggle>();

                            // Get sensor publisher
                            object sensorPublisher = sensorTf.tag switch
                            {
                                "GNSSSensor" => sensorPublisher = sensorTf.GetComponent<GnssRos2Publisher>(),
                                "IMUSensor" => sensorPublisher = sensorTf.GetComponent<ImuRos2Publisher>(),
                                "OdometrySensor" => sensorPublisher = sensorTf.GetComponent<OdometryRos2Publisher>(),
                                "PoseSensor" => sensorPublisher = sensorTf.GetComponent<PoseRos2Publisher>(),
                                _ => throw new ArgumentOutOfRangeException()
                            };
                            var configList =
                                Ros2PublisherUtilities.ConfigListBuilder(sensorPublisher); // Get publisher config
                            toggle.isOn = sensorTf.gameObject.activeSelf; // Set initial state
                            ToggleComponentBuilder.BuildToggleComponent(SensorToggleFunctions.ToggleSensor, toggle,
                                configList, sensorTf);
                        }
                        break;
                }

                //create frameID
                var textObject = Instantiate(_textPrefab, cardGroup.transform);
                SetupSensorIDText(textObject, sensorTf);
            }
        }

        // Setup sensor ID text for the toggles
        private void SetupSensorIDText(GameObject textObject, Transform sensorTf)
        {
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 140);
            textObject.GetComponent<Text>().text = sensorTf.tag switch
            {
                "LidarSensor" => "Lidar: " + sensorTf.GetComponent<RglLidarPublisher>().frameID,
                "CameraSensor" => "Camera: " + sensorTf.GetComponent<CameraRos2Publisher>().frameId,
                "GNSSSensor" => "GNSS: " + sensorTf.GetComponent<GnssRos2Publisher>().frameId,
                "IMUSensor" => "IMU: " + sensorTf.GetComponent<ImuRos2Publisher>().frameId,
                "OdometrySensor" => "Odometry: " + sensorTf.GetComponent<OdometryRos2Publisher>().FrameID,
                "PoseSensor" => "Pose: " + sensorTf.GetComponent<PoseRos2Publisher>().FrameID,
                _ => textObject.GetComponent<Text>().text
            };
        }

        // Build sensor list automatically
        private void SensorListAutomaticBuilder()
        {
            _lidarSensors = CollectionBuilder.CreateComponentListByTag("LidarSensor", sensor => sensor.transform);
            _cameraSensors = CollectionBuilder.CreateComponentListByTag("CameraSensor", sensor => sensor.transform);
            _gnssSensors = CollectionBuilder.CreateComponentListByTag("GNSSSensor", sensor => sensor.transform);
            _imuSensors = CollectionBuilder.CreateComponentListByTag("IMUSensor", sensor => sensor.transform);
            _odometrySensors = CollectionBuilder.CreateComponentListByTag("OdometrySensor", sensor => sensor.transform);
            _poseSensors = CollectionBuilder.CreateComponentListByTag("PoseSensor", sensor => sensor.transform);
        }
    }
}
