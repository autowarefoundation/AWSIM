using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AWSIM.Scripts.UI
{
    [Serializable]
    public class Hotkey
    {
        public string key;
        public string description;
    }

    [Serializable]
    public class HotkeySource
    {
        public string name;
        public List<Hotkey> hotkeys;
    }

    [Serializable]
    public class HotkeySourceList
    {
        public List<HotkeySource> keyBinds;
    }

    public class UIHotkeysPanel : MonoBehaviour
    {
        [SerializeField] private UICard _uiCard;
        [SerializeField] private GameObject _hotkeyGroupPrefab;
        [SerializeField] private GameObject _hotkeyLinePrefab;
        [SerializeField] private GameObject _hotkeyTextPrefab;

        private bool _uiTabReady;

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
            var jsonText = Resources.Load<TextAsset>("Hotkeys");
            var hotkeySourceList = JsonUtility.FromJson<HotkeySourceList>(jsonText.text);

            foreach (var hotkeySource in hotkeySourceList.keyBinds)
            {
                // Create header
                var hotkeyGroup = Instantiate(_hotkeyGroupPrefab, transform);
                hotkeyGroup.name = hotkeySource.name;
                var hotkeyHeader = (RectTransform)hotkeyGroup.transform.GetChild(0);
                hotkeyHeader.GetComponent<Text>().text = hotkeySource.name;
                var hotkeyCount = hotkeySource.hotkeys.Count;

                // Create hotkeys
                foreach (var hotkey in hotkeySource.hotkeys)
                {
                    var line = Instantiate(_hotkeyLinePrefab, hotkeyGroup.transform);

                    var keyText = Instantiate(_hotkeyTextPrefab, line.transform.GetChild(0));
                    keyText.GetComponent<Text>().text = hotkey.key;

                    var descriptionText = Instantiate(_hotkeyTextPrefab, line.transform.GetChild(1));
                    descriptionText.GetComponent<Text>().text = ": " + hotkey.description;
                }

                // Calculate group height
                var hotkeyGroupLayout = hotkeyGroup.GetComponent<LayoutGroup>();
                var verticalOffsets =
                    hotkeyGroupLayout.padding.bottom * 2; // top and bottom padding are different: use bottom
                var hotkeyHeaderHeight = hotkeyHeader.sizeDelta.y;
                var lineHeight = _hotkeyLinePrefab.GetComponent<RectTransform>().sizeDelta.y;
                var groupHeight = lineHeight * hotkeyCount + hotkeyHeaderHeight + verticalOffsets;

                hotkeyGroup.GetComponent<RectTransform>().sizeDelta = new Vector2(0, groupHeight);
            }

            card.RecalculateTabBackgroundHeight();
            Resources.UnloadAsset(jsonText);
        }
    }
}
