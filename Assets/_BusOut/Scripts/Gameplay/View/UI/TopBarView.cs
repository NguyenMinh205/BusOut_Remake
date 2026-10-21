using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Gameplay
{
    /// Thanh trên màn chơi: coin, số level, nút cài đặt, nút chơi lại. Gắn trên Canvas/TopUI.
    public class TopBarView : MonoBehaviour
    {
        [Tooltip("Chữ số coin (cạnh icon coin).")]
        [SerializeField] private TMP_Text coinText;
        [Tooltip("Chữ \"LEVEL n\" giữa thanh.")]
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button replayButton;

        public event Action SettingsClicked;
        public event Action ReplayClicked;

        public bool Init()
        {
            string missing = "";
            if (coinText == null) missing += " CoinText";
            if (levelText == null) missing += " LevelText";
            if (settingsButton == null) missing += " SettingsButton";
            if (replayButton == null) missing += " ReplayButton";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] TopBarView thiếu tham chiếu:{missing}. Kéo các object con của TopUI vào.", this);
                return false;
            }
            settingsButton.onClick.AddListener(HandleSettingsClicked);
            replayButton.onClick.AddListener(HandleReplayClicked);
            return true;
        }

        private void OnDestroy()
        {
            if (settingsButton != null) settingsButton.onClick.RemoveListener(HandleSettingsClicked);
            if (replayButton != null) replayButton.onClick.RemoveListener(HandleReplayClicked);
        }

        public void SetCoins(int coins) => coinText.SetText("{0}", coins);

        public void SetLevel(string format, int level) => levelText.SetText(format, level);

        private void HandleSettingsClicked() => SettingsClicked?.Invoke();
        private void HandleReplayClicked() => ReplayClicked?.Invoke();
    }
}
