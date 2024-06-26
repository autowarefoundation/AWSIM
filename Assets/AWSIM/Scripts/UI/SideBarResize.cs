using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AWSIM.Scripts.UI
{
    public class SideBarResize : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float _sidebarMinimumWidth = 220;
        [SerializeField] private float _sidebarDragBorderSize = 5;

        private Vector2 _initialDragPointOffset;
        private Vector2 _initialSizeDelta;
        private RectTransform _sideBarRectTransform;
        private UISideBarHandler _sideBarHandler;
        private Image _sideBarImage;
        private bool _dragActive;

        private void Start()
        {
            _sideBarRectTransform = GetComponent<RectTransform>();
            _sideBarHandler = GetComponentInParent<UISideBarHandler>();
            _sideBarImage = GetComponent<Image>();
            _sideBarImage.raycastPadding = new Vector4(_sidebarMinimumWidth - _sidebarDragBorderSize, 0, 0, 0);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            var canvas = _sideBarRectTransform.GetComponentInParent<Canvas>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_sideBarRectTransform, eventData.position,
                canvas.worldCamera, out var localPointerPosition);

            var scaleFactor = canvas.scaleFactor;
            // Calculate detection bounds with "canvas.scaleFactor"
            var pointerPositionX = localPointerPosition.x * scaleFactor;
            var areaRightBound = _sideBarRectTransform.rect.xMax * scaleFactor;
            var areaLeftBound = (_sideBarRectTransform.rect.xMin + _sideBarImage.raycastPadding.x) * scaleFactor;

            // Debugs for testing
            // Debug.DrawLine(new Vector3(_sideBarRectTransform.rect.xMax * scaleFactor, 500, 0),
            //     new Vector3((_sideBarRectTransform.rect.xMin + _sideBarImage.raycastPadding.x) *
            //                 scaleFactor, 500, 0), Color.red, 1000f,
            //     false);
            // Debug.Log(_sideBarRectTransform.rect.xMax * scaleFactor + " " + pointerPositionX + " " +
            //           (_sideBarRectTransform.rect.xMin + _sideBarImage.raycastPadding.x) * scaleFactor);

            // Check if the pointer is within the drag border
            if (areaRightBound > pointerPositionX && pointerPositionX > areaLeftBound)
            {
                _dragActive = true;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragActive)
            {
                // Lock the cursor to game window
                Cursor.lockState = CursorLockMode.Confined;

                var canvas = _sideBarRectTransform.GetComponent<Canvas>();

                RectTransformUtility.ScreenPointToLocalPointInRectangle(_sideBarRectTransform, eventData.position,
                    canvas.worldCamera, out var localPointerPosition);

                _sideBarRectTransform.sizeDelta =
                    new Vector2(localPointerPosition.x, _initialSizeDelta.y) -
                    new Vector2(_initialDragPointOffset.x, 0);

                var padding = _sideBarImage.raycastPadding;
                padding.x = _sideBarRectTransform.sizeDelta.x - _sidebarDragBorderSize;
                _sideBarImage.raycastPadding = padding;

                _sideBarHandler.SideBarPositionDisabled = new Vector2(-_sideBarRectTransform.sizeDelta.x, 0);
            }
        }

        // Reset cursor lock and drag state
        public void OnPointerUp(PointerEventData eventData)
        {
            Cursor.lockState = CursorLockMode.None;
            _dragActive = false;
        }

        // Limit the minimum width of the sidebar
        public void OnRectTransformDimensionsChange()
        {
            if (_sideBarRectTransform == null) return;
            if (_sideBarRectTransform.sizeDelta.x < _sidebarMinimumWidth)
            {
                _sideBarRectTransform.sizeDelta = new Vector2(_sidebarMinimumWidth, _initialSizeDelta.y);
            }
        }
    }
}
