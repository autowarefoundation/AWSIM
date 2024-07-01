using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AWSIM.Scripts.UI
{
    // Functions used by the UI elements
    public static class UIFunctions
    {
        // Lerp simple values
        public static IEnumerator LerpImageColor(Image image, Color targetColor, float lerpValue)
        {
            var elapsedTime = 0f;
            var currentColor = image.color;
            while (elapsedTime < 1f)
            {
                elapsedTime += Time.deltaTime * lerpValue;
                image.color = Color.Lerp(currentColor, targetColor, elapsedTime);
                yield return null;
            }

            image.color = targetColor;
        }

        // Lerp UI objects position
        public static IEnumerator LerpUIRectPosition(RectTransform uiRect, Vector2 targetPosition, float lerpValue,
            bool willDisableAtEnd)
        {
            var elapsedTime = 0f;
            var currentPosition = uiRect.anchoredPosition;

            while (elapsedTime < 1f)
            {
                elapsedTime += Time.deltaTime * lerpValue;
                uiRect.anchoredPosition = Vector2.Lerp(currentPosition, targetPosition, elapsedTime);
                yield return null;
            }

            // Ensure the UI element is exactly at the target position
            uiRect.anchoredPosition = targetPosition;

            // Disable at end if wanted
            if (willDisableAtEnd)
            {
                uiRect.gameObject.SetActive(false);
            }
        }

        // Lerp UI Card preferred height
        public static IEnumerator LerpUICardPreferredHeight(Button activator, LayoutElement uiElement,
            float targetHeight, float lerpValue)
        {
            var elapsedTime = 0f;
            var currentHeight = uiElement.preferredHeight;
            SetButtonInteraction(activator, false);
            while (elapsedTime < 1f)
            {
                elapsedTime += Time.deltaTime * lerpValue;
                var lerpHeight = Mathf.Lerp(currentHeight, targetHeight, elapsedTime);
                uiElement.preferredHeight = lerpHeight;
                yield return null;
            }

            // Ensure the UI element has the target height
            uiElement.preferredHeight = targetHeight;
            SetButtonInteraction(activator, true);
        }

        public static IEnumerator LerpRectTransformRotation(RectTransform rectTf, float targetDegrees, float lerpVal)
        {
            var elapsedTime = 0f;
            var currentRotation = rectTf.localEulerAngles.z;
            while (elapsedTime < 1f)
            {
                elapsedTime += Time.deltaTime * lerpVal;
                var lerpRotation = Mathf.Lerp(currentRotation, targetDegrees, elapsedTime);
                rectTf.localEulerAngles = new Vector3(0, 0, lerpRotation);
                yield return null;
            }

            // Ensure the UI element has the target rotation
            rectTf.localEulerAngles = new Vector3(0, 0, targetDegrees);
        }

        // Calculate the total height of a card based on the elements and spacing
        public static float CalculateCardTotalHeight(List<RectTransform> rectTransforms, float elementSpacing)
        {
            return rectTransforms.Sum(rectTransform => rectTransform.sizeDelta.y + elementSpacing);
        }

        // Set the interaction of a button
        private static void SetButtonInteraction(Button element, bool isInteractable)
        {
            element.interactable = isInteractable;
        }
    }
}
