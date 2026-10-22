using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Gameplay
{
    /// Bảng kết quả cuối ván: thắng (coin thưởng + Level tiếp) hoặc thua (Hồi sinh +1 ô / Chơi lại).
    /// Gắn trên object cha của 2 panel (luôn bật); 2 panel con tắt / bật theo kết quả.
    public class LevelResultView : MonoBehaviour
    {
        [Header("Thắng")]
        [SerializeField] private GameObject winPanel;
        [Tooltip("Chữ coin thưởng, vd. \"+20\".")]
        [SerializeField] private TMP_Text winRewardText;
        [SerializeField] private Button nextButton;

        [Header("Thua")]
        [SerializeField] private GameObject losePanel;
        [Tooltip("Nút hồi sinh +1 ô (ẩn khi không còn ô khoá để mở).")]
        [SerializeField] private Button reviveButton;
        [SerializeField] private Button replayButton;

        public event Action NextClicked;
        public event Action ReviveClicked;
        public event Action ReplayClicked;

        public bool Init()
        {
            string missing = "";
            if (winPanel == null) missing += " WinPanel";
            if (winRewardText == null) missing += " WinRewardText";
            if (nextButton == null) missing += " NextButton";
            if (losePanel == null) missing += " LosePanel";
            if (reviveButton == null) missing += " ReviveButton";
            if (replayButton == null) missing += " ReplayButton";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] LevelResultView thiếu tham chiếu:{missing}. Kéo panel / nút trong bảng kết quả vào.", this);
                return false;
            }
            // ⚠️ Panel không được là chính object này: tắt object mang script là tắt luôn cả bảng lẫn Init sau này.
            if (winPanel == gameObject || losePanel == gameObject)
            {
                Debug.LogError("[BusOut] LevelResultView: Win Panel / Lose Panel phải là object CON, không phải object gắn script.", this);
                return false;
            }
            nextButton.onClick.AddListener(HandleNextClicked);
            reviveButton.onClick.AddListener(HandleReviveClicked);
            replayButton.onClick.AddListener(HandleReplayClicked);
            Hide();
            return true;
        }

        private void OnDestroy()
        {
            if (nextButton != null) nextButton.onClick.RemoveListener(HandleNextClicked);
            if (reviveButton != null) reviveButton.onClick.RemoveListener(HandleReviveClicked);
            if (replayButton != null) replayButton.onClick.RemoveListener(HandleReplayClicked);
        }

        public void Hide()
        {
            winPanel.SetActive(false);
            losePanel.SetActive(false);
        }

        public void ShowWin(int coinReward)
        {
            losePanel.SetActive(false);
            winRewardText.SetText("+{0}", coinReward);
            winPanel.SetActive(true);
        }

        public void ShowLose(bool canRevive)
        {
            winPanel.SetActive(false);
            reviveButton.gameObject.SetActive(canRevive);
            losePanel.SetActive(true);
        }

        private void HandleNextClicked() => NextClicked?.Invoke();
        private void HandleReviveClicked() => ReviveClicked?.Invoke();
        private void HandleReplayClicked() => ReplayClicked?.Invoke();
    }
}
