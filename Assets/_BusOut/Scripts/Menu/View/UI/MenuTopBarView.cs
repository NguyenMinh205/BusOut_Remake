using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Menu
{
    /// Thanh trên của Menu: số coin, nút bánh răng (mở popup Cài đặt dùng chung).
    /// Gắn trên HomeContext/UI/Canvas/SafeArea/TopUI.
    public class MenuTopBarView : MonoBehaviour
    {
        [Tooltip("TopUI/SettingBtn.")]
        [SerializeField] private Button settingsButton;
        [Tooltip("TopUI/Coin/CoinTotal/CoinTxt: số coin.")]
        [SerializeField] private TMP_Text coinText;

        public event Action SettingsClicked;

        public bool Init()
        {
            string missing = "";
            if (settingsButton == null) missing += " SettingsButton";
            if (coinText == null) missing += " CoinText";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] MenuTopBarView thiếu tham chiếu:{missing}. Kéo TopUI/SettingBtn và CoinTxt vào.", this);
                return false;
            }
            settingsButton.onClick.AddListener(HandleSettingsClicked);
            return true;
        }

        private void OnDestroy()
        {
            if (settingsButton != null) settingsButton.onClick.RemoveListener(HandleSettingsClicked);
        }

        public void SetCoins(int coins) => coinText.SetText("{0}", coins);

        private void HandleSettingsClicked() => SettingsClicked?.Invoke();
    }
}
