using UnityEngine;
using UnityEngine.UI;

namespace AWSIM.Scripts.UI
{
    public class UICameraBridge : MonoBehaviour
    {
        [SerializeField] private Canvas cameraOutputCanvas;
        [SerializeField] private float startXOffset;
        [SerializeField] private float startYOffset;
        [SerializeField] private float height;

        private RawImage rawImage;
        private AspectRatioFitter aspectRatioFitter;

        private void Start()
        {
            if (cameraOutputCanvas == null) {
                UICameraOutputCanvas uiCameraOutputCanvas = GameObject.FindObjectOfType<UICameraOutputCanvas>();
                cameraOutputCanvas = uiCameraOutputCanvas.GetComponent<Canvas>();
            }
            SetupCameraOutput();
        }

        private void SetupCameraOutput()
        {
            var cameraOutput = new GameObject(
                "CameraOutput",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UIInteractionHandler),
                typeof(RawImage),
                typeof(AspectRatioFitter));

            cameraOutput.transform.SetParent(cameraOutputCanvas.transform, false);

            // Configure RectTransform for bottom left position with specified offsets
            var rectTransform = cameraOutput.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.anchoredPosition = new Vector2(startXOffset, startYOffset);
            // Width will be adjusted by AspectRatioFitter, only set height here
            rectTransform.sizeDelta = new Vector2(0, height);

            rawImage = cameraOutput.GetComponent<RawImage>();
            aspectRatioFitter = cameraOutput.GetComponent<AspectRatioFitter>();
            aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
        }

        public void RenderCameraToUI(CameraSensor.CameraParameters camParams, Texture camRenderTex)
        {
            aspectRatioFitter.aspectRatio = (float)camParams.width / camParams.height;
            rawImage.rectTransform.sizeDelta = new Vector2(height * aspectRatioFitter.aspectRatio, height);
            rawImage.texture = camRenderTex;
        }

        private void OnEnable()
        {
            if (rawImage != null)
                rawImage.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (rawImage != null)
                rawImage.gameObject.SetActive(false);
        }
    }
}
