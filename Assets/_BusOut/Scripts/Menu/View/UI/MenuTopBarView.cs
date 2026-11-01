using System;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Menu
{
    /// Thanh trên của Menu: nút bánh răng (mở popup Cài đặt dùng chung).
    /// Gắn trên HomeContext/UI/Canvas/SafeArea/TopUI.
    public class MenuTopBarView : MonoBehaviour
    {
        [Tooltip("TopUI/SettingBtn.")]
        [SerializeField] private Button settingsButton;

        public event Action SettingsClicked;

        public bool Init()
        {
            if (settingsButton == null)
            {
                Debug.LogError("[BusOut] MenuTopBarView thiếu Settings Button. Kéo TopUI/SettingBtn vào.", this);
                return false;
            }
            settingsButton.onClick.AddListener(HandleSettingsClicked);
            return true;
        }

        private void OnDestroy()
        {
            if (settingsButton != null) settingsButton.onClick.RemoveListener(HandleSettingsClicked);
        }

        private void HandleSettingsClicked() => SettingsClicked?.Invoke();
    }
}
