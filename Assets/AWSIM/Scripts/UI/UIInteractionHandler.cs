using UnityEngine;
using UnityEngine.EventSystems;

namespace AWSIM.Scripts.UI
{
    public class UIInteractionHandler : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        private Vector2 _initialDragPointOffset;

        public void OnPointerDown(PointerEventData eventData)
        {
            var canvas = GetComponentInParent<Canvas>();
            var canvasRectTransform = canvas.GetComponent<RectTransform>();

            // Store the initial offset from the pointer to the object's anchor point in local canvas coordinates
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, eventData.position,
                canvas.worldCamera, out var localPointerPosition);
            _initialDragPointOffset = localPointerPosition - ((RectTransform)transform).anchoredPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            var canvas = GetComponentInParent<Canvas>();
            var canvasRectTransform = canvas.GetComponent<RectTransform>();

            // Convert the current pointer position to local canvas coordinates
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, eventData.position,
                canvas.worldCamera, out var localPointerPosition);

            // Apply the initial offset to keep the drag point consistent
            ((RectTransform)transform).anchoredPosition = localPointerPosition - _initialDragPointOffset;

        }
    }
}
