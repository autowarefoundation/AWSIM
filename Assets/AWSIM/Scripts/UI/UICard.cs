using System;
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
        [SerializeField] public float ElementHeight = 25f;
        [SerializeField] public float ElementSpacing = 5f;

        [NonSerialized] private List<RectTransform> RectTransformsForHeightCalculation;
        private float _tabOffBgHeight;
        private float _tabOnBgHeight;
        private float _lerpValue;

        private void Awake()
        {
            RectTransformsForHeightCalculation = BuildChildRectTransformList();
            _tabOnBgHeight =
                UIFunctions.CalculateCardTotalHeight(RectTransformsForHeightCalculation, ElementSpacing);
            _tabOffBgHeight = ElementHeight;
        }

        private void Start()
        {
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
            RectTransformsForHeightCalculation = BuildChildRectTransformList();
            _tabOnBgHeight =
                UIFunctions.CalculateCardTotalHeight(RectTransformsForHeightCalculation, ElementSpacing);
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
