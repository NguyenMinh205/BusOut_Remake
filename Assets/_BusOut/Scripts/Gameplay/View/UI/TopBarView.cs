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
        [Tooltip("Icon coin (TopUI/Coin): đích coin bay về khi nhận thưởng thắng. Trống = bay về chữ số coin (báo lỗi).")]
        [SerializeField] private RectTransform coinIcon;
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
            if (coinIcon == null)
                Debug.LogError("[BusOut] TopBarView thiếu Coin Icon: coin thưởng bay về chữ số thay vì icon. Kéo TopUI/Coin vào.", this);
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

        /// Chỗ coin thưởng bay về (vị trí thế giới — Canvas Overlay: toạ độ màn hình).
        public Vector3 CoinTargetPosition => coinIcon != null ? coinIcon.position : coinText.transform.position;

        public void SetLevel(string format, int level) => levelText.SetText(format, level);

        private void HandleSettingsClicked() => SettingsClicked?.Invoke();
        private void HandleReplayClicked() => ReplayClicked?.Invoke();
    }
}
