using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AWSIM.Scripts.UI
{
    public class UICard : MonoBehaviour
    {
        [SerializeField] private Button _cardTopBarButton;
        [SerializeField] private RectTransform _barChevronRect;
        [SerializeField] private float _chevronExpandedDegrees = 90f;
        [SerializeField] private float _chevronCollapsedDegrees = 0f;

        [SerializeField] private bool _isCardOpen;
        [SerializeField] public float ElementHeight = 30f;
        [SerializeField] public float ElementSpacing = 5f;

        private List<RectTransform> _rectTransformsForHeightCalculation;
        private float _tabOffBgHeight;
        private float _tabOnBgHeight;
        private float _lerpValue;

        private void Awake()
        {
            // set initial on/off height
            _rectTransformsForHeightCalculation = BuildChildRectTransformList();
            _tabOnBgHeight = UIFunctions.CalculateCardTotalHeight(_rectTransformsForHeightCalculation, ElementSpacing);
            _tabOffBgHeight = ElementHeight;
        }

        private void Start()
        {
            // Get lerp value
            _lerpValue = GetComponentInParent<UISideBarHandler>().UIAnimationLerpValue;
        }

        // TODO: Make tabs more responsive to input. (mozzz)
        public void ClickTabBar()
        {
            if (_isCardOpen == false)
            {
                StartCoroutine(UIFunctions.LerpUICardPreferredHeight(_cardTopBarButton, GetComponent<LayoutElement>(),
                    _tabOnBgHeight, _lerpValue));
                StartCoroutine(UIFunctions.LerpRectTransformRotation(_barChevronRect, 0, _lerpValue));
                _isCardOpen = true;
            }
            else
            {
                StartCoroutine(UIFunctions.LerpUICardPreferredHeight(_cardTopBarButton, GetComponent<LayoutElement>(),
                    _tabOffBgHeight, _lerpValue));
                StartCoroutine(UIFunctions.LerpRectTransformRotation(_barChevronRect, 90, _lerpValue));
                _isCardOpen = false;
            }
        }

        // Call this from other scripts to recalculate the tab background height
        public void RecalculateTabBackgroundHeight()
        {
            _rectTransformsForHeightCalculation = BuildChildRectTransformList();
            _tabOnBgHeight = UIFunctions.CalculateCardTotalHeight(_rectTransformsForHeightCalculation, ElementSpacing);
            if (_isCardOpen)
            {
                StartCoroutine(UIFunctions.LerpUICardPreferredHeight(_cardTopBarButton, GetComponent<LayoutElement>(),
                    _tabOnBgHeight, _lerpValue));
            }
        }

        private List<RectTransform> BuildChildRectTransformList()
        {
            var rectTransforms = new List<RectTransform>();
            for (var i = 0; i < transform.childCount; i++)
            {
                rectTransforms.Add(transform.GetChild(i).GetComponent<RectTransform>());
            }

            return rectTransforms;
        }
    }
}
