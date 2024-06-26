using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace AWSIM.Scripts.UI
{
    public class GraphicsSettings : MonoBehaviour
    {
        private Dropdown _dropdown;
        private Light _sunSource;
        private List<GameObject> _cameraObjectsList;

        private int _gpuMemorySize;
        private bool _isInitialised;
        private int _initialQualityLevel;

        private readonly List<string> _dropdownOptions = new List<string>
        {
            "Low",
            "Medium",
            "High",
            "Ultra"
        };

        // Initialised as true to apply expensive changes on start
        private bool _doApplyExpensiveChanges = true;

        private void Awake()
        {
            // Get the size of the device's graphics memory
            _gpuMemorySize = SystemInfo.graphicsMemorySize;
        }

        private void Start()
        {
            // Populate list of cameras. Don't include sensor cameras
            _cameraObjectsList = new List<GameObject>
            {
                Camera.main?.gameObject,
                GameObject.FindGameObjectWithTag("BEVCamera")
            };

            _sunSource = GameObject.FindGameObjectWithTag("Sun").GetComponent<Light>();

            // Set initial quality level
            InitialQualityLevel();

            // Populate dropdown with quality settings
            _dropdown = GetComponent<Dropdown>();
            _dropdown.options.Clear();
            foreach (var option in _dropdownOptions)
            {
                _dropdown.options.Add(new Dropdown.OptionData(option));
            }

            _dropdown.value = _initialQualityLevel;
            _dropdown.RefreshShownValue();
        }

        private void InitialQualityLevel()
        {
            if (!_isInitialised)
            {
                // Select Quality settings level (URP Asset) based on the size of the device's graphics memory
                switch (_gpuMemorySize)
                {
                    case <= 2048:
                        _initialQualityLevel = 0;
                        GraphicsLowQuality(true);
                        break;
                    case <= 4096:
                        _initialQualityLevel = 1;
                        GraphicsMediumQuality(true);
                        break;
                    case <= 6144:
                        _initialQualityLevel = 2;
                        GraphicsHighQuality(true);
                        break;
                    case <= 8192:
                        _initialQualityLevel = 3;
                        GraphicsUltraQuality(true);
                        break;
                    default:
                        _initialQualityLevel = 3;
                        GraphicsUltraQuality(true);
                        break;
                }

                _isInitialised = true;
            }
        }

        public void ExpensiveChangesToggle(bool value)
        {
            _doApplyExpensiveChanges = value;
        }

        public void UISetQuality(int value)
        {
            switch (value)
            {
                case 0:
                    GraphicsLowQuality(_doApplyExpensiveChanges);
                    break;
                case 1:
                    GraphicsMediumQuality(_doApplyExpensiveChanges);
                    break;
                case 2:
                    GraphicsHighQuality(_doApplyExpensiveChanges);
                    break;
                case 3:
                    GraphicsUltraQuality(_doApplyExpensiveChanges);
                    break;
            }
        }

        private void GraphicsLowQuality(bool doExpensiveChanges)
        {
            QualitySettings.SetQualityLevel(0, doExpensiveChanges);

            // update camera parameters
            foreach (var cam in _cameraObjectsList)
            {
                cam.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData);
                cam.TryGetComponent<Volume>(out var cameraVolume);
                var cameraVolumeProfile = cameraVolume.profile;

                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.renderPostProcessing = false;
                cameraData.renderShadows = false;
                cameraData.allowHDROutput = false;

                if (cameraVolumeProfile.TryGet(out Bloom bloom))
                {
                    bloom.active = false;
                }

                if (cameraVolumeProfile.TryGet(out Tonemapping tonemapping))
                {
                    tonemapping.active = false;
                }
            }

            _sunSource.shadows = LightShadows.Hard;
        }

        private void GraphicsMediumQuality(bool doExpensiveChanges)
        {
            QualitySettings.SetQualityLevel(1, doExpensiveChanges);

            // update camera and volume parameters
            foreach (var cam in _cameraObjectsList)
            {
                cam.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData);
                cam.TryGetComponent<Volume>(out var cameraVolume);
                var cameraVolumeProfile = cameraVolume.profile;

                cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                cameraData.antialiasingQuality = AntialiasingQuality.Medium;
                cameraData.renderPostProcessing = true;
                cameraData.renderShadows = true;
                cameraData.allowHDROutput = false;

                if (cameraVolumeProfile.TryGet(out Bloom bloom))
                {
                    bloom.active = false;
                    bloom.intensity.value = 0.25f;
                }

                if (cameraVolumeProfile.TryGet(out Tonemapping tonemapping))
                {
                    tonemapping.active = false;
                }
            }

            _sunSource.shadows = LightShadows.Hard;
        }

        private void GraphicsHighQuality(bool doExpensiveChanges)
        {
            QualitySettings.SetQualityLevel(2, doExpensiveChanges);

            // update camera parameters
            foreach (var cam in _cameraObjectsList)
            {
                cam.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData);
                cam.TryGetComponent<Volume>(out var cameraVolume);
                var cameraVolumeProfile = cameraVolume.profile;

                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cameraData.antialiasingQuality = AntialiasingQuality.Medium;
                cameraData.renderPostProcessing = true;
                cameraData.renderShadows = true;
                cameraData.allowHDROutput = true;

                if (cameraVolumeProfile.TryGet(out Bloom bloom))
                {
                    bloom.active = true;
                    bloom.intensity.value = 0.5f;
                    bloom.highQualityFiltering.overrideState = false;
                    bloom.highQualityFiltering.value = false;
                }

                if (cameraVolumeProfile.TryGet(out Tonemapping tonemapping))
                {
                    tonemapping.active = true;
                    tonemapping.mode.value = TonemappingMode.ACES;
                }
            }

            _sunSource.shadows = LightShadows.Soft;
        }

        private void GraphicsUltraQuality(bool doExpensiveChanges)
        {
            QualitySettings.SetQualityLevel(3, doExpensiveChanges);

            // update camera parameters
            foreach (var cam in _cameraObjectsList)
            {
                cam.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData);
                cam.TryGetComponent<Volume>(out var cameraVolume);
                var cameraVolumeProfile = cameraVolume.profile;

                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
                cameraData.renderPostProcessing = true;
                cameraData.renderShadows = true;
                cameraData.allowHDROutput = true;

                if (cameraVolumeProfile.TryGet(out Bloom bloom))
                {
                    bloom.active = true;
                    bloom.intensity.value = 1.0f;
                    bloom.highQualityFiltering.overrideState = true;
                    bloom.highQualityFiltering.value = true;
                }

                if (cameraVolumeProfile.TryGet(out Tonemapping tonemapping))
                {
                    tonemapping.active = true;
                    tonemapping.mode.value = TonemappingMode.ACES;
                }
            }

            _sunSource.shadows = LightShadows.Soft;
        }

        // TODO: update settings based on user input
        private void CustomAssetProperties()
        {
        }
    }
}
