using System;
using UnityEngine;
using UnityEngine.UI;

namespace AWSIM.Scripts.UI
{
    public class UISideBarHandler : MonoBehaviour
    {
        [SerializeField] public float UIAnimationLerpValue;

        [SerializeField] private GameObject _sideBar;
        [SerializeField] private Transform _sideBarChevron;
        [SerializeField] private Sprite _chevronSpriteLeft, _chevronSpriteRight;

        private Vector2 _sideBarPositionActive;
        [NonSerialized] public Vector2 SideBarPositionDisabled;

        private RectTransform _sideBarRectTransform;
        private bool _isSideBarActivePos;

        private void Start()
        {
            _sideBarRectTransform = _sideBar.GetComponent<RectTransform>();
            // Set the active and disabled position of the sidebar
            _sideBarPositionActive = new Vector2(0, 0);
            SideBarPositionDisabled = new Vector2(-_sideBarRectTransform.sizeDelta.x, 0);
            // Move the sidebar to the active position
            _sideBarRectTransform.anchoredPosition = _sideBarPositionActive;
            _isSideBarActivePos = true;
        }

        public void ToggleSideBar()
        {
            // Toggle chevron sprite
            _sideBarChevron.GetChild(0).GetComponent<Image>().sprite =
                _isSideBarActivePos ? _chevronSpriteLeft : _chevronSpriteRight;
            // Toggle sidebar position
            if (!_isSideBarActivePos)
            {
                StartCoroutine(UIFunctions.LerpUIRectPosition(_sideBarRectTransform, _sideBarPositionActive,
                    UIAnimationLerpValue, false));
                _isSideBarActivePos = true;
            }
            else
            {
                StartCoroutine(UIFunctions.LerpUIRectPosition(_sideBarRectTransform, SideBarPositionDisabled,
                    UIAnimationLerpValue, false));
                _isSideBarActivePos = false;
            }
        }
    }
}
