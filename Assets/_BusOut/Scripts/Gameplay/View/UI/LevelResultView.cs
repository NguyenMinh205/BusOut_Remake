using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Gameplay
{
    /// Các popup cuối ván. Thắng → WinPanel (nhận coin thường / nhận thêm bằng quảng cáo).
    /// Thua → ContinuePanel (hồi sinh +1 ô bằng coin / quảng cáo, hoặc đóng) → đóng thì LosePanel (chơi lại).
    /// Gắn trên Popup/.../LevelResult (luôn bật); panel con bật / tắt theo kết quả.
    public class LevelResultView : MonoBehaviour
    {
        [Header("Thắng")]
        [SerializeField] private AnimatedPanel winPanel;
        [Tooltip("WatchAdsAddCoinBtn: nhận coin thưởng nhân lên bằng quảng cáo.")]
        [SerializeField] private Button winAdClaimButton;
        [Tooltip("WatchAdsAddCoinBtn/CoinContent/CoinAmountTxt: số coin nhận được khi xem quảng cáo.")]
        [SerializeField] private TMP_Text winAdRewardText;
        [Tooltip("WinContent/TapToCollectBtn (\"Tap to collect\"): nhận coin thưởng thường.")]
        [SerializeField] private Button winClaimButton;
        [Tooltip("TapToCollectBtn/CoinContent/CoinAmountTxt: số coin thưởng thường.")]
        [SerializeField] private TMP_Text winRewardText;

        [Header("Tiếp tục (hồi sinh)")]
        [SerializeField] private AnimatedPanel continuePanel;
        [Tooltip("ContinuePlayingBtn: trả coin để mở thêm 1 ô.")]
        [SerializeField] private Button continueCoinButton;
        [Tooltip("ContinuePlayingBtn/Cost/CostTxt: giá coin.")]
        [SerializeField] private TMP_Text continueCostText;
        [Tooltip("WatchAdsBtn: xem quảng cáo để mở thêm 1 ô.")]
        [SerializeField] private Button continueAdButton;
        [Tooltip("CloseBtn: không hồi sinh → sang LosePanel.")]
        [SerializeField] private Button continueCloseButton;

        [Header("Thua")]
        [SerializeField] private AnimatedPanel losePanel;
        [Tooltip("LosePopup/TitleTxt: \"LEVEL n\".")]
        [SerializeField] private TMP_Text loseLevelText;
        [SerializeField] private Button loseReplayButton;
        [Tooltip("LosePopup/CloseBtn: TẠM chơi lại (chưa có màn Home).")]
        [SerializeField] private Button loseCloseButton;

        /// true = nhận thêm bằng quảng cáo.
        public event Action<bool> WinClaimed;
        public event Action ReviveWithCoinsClicked;
        public event Action ReviveWithAdClicked;
        public event Action ContinueDeclined;
        public event Action ReplayClicked;

        private PanelAnimationSettings panelAnimation;

        public bool Init(PanelAnimationSettings animationSettings)
        {
            panelAnimation = animationSettings;
            string missing = "";
            CheckPanel(winPanel, "WinPanel", ref missing);
            CheckPanel(continuePanel, "ContinuePanel", ref missing);
            CheckPanel(losePanel, "LosePanel", ref missing);
            if (winAdClaimButton == null) missing += " WinAdClaimButton";
            if (winAdRewardText == null) missing += " WinAdRewardText";
            if (winClaimButton == null) missing += " WinClaimButton";
            if (winRewardText == null) missing += " WinRewardText";
            if (continueCoinButton == null) missing += " ContinueCoinButton";
            if (continueCostText == null) missing += " ContinueCostText";
            if (continueAdButton == null) missing += " ContinueAdButton";
            if (continueCloseButton == null) missing += " ContinueCloseButton";
            if (loseLevelText == null) missing += " LoseLevelText";
            if (loseReplayButton == null) missing += " LoseReplayButton";
            if (loseCloseButton == null) missing += " LoseCloseButton";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] LevelResultView thiếu tham chiếu:{missing}. Kéo object trong các popup vào.", this);
                return false;
            }
            // ⚠️ Panel không được là chính object này: tắt panel là tắt luôn script và mọi panel khác.
            if (winPanel.root == gameObject || continuePanel.root == gameObject || losePanel.root == gameObject)
            {
                Debug.LogError("[BusOut] LevelResultView: Root của panel phải là object CON (WinPanel…), không phải LevelResult.", this);
                return false;
            }

            winPanel.Init();
            continuePanel.Init();
            losePanel.Init();
            winAdClaimButton.onClick.AddListener(HandleWinAdClaimed);
            winClaimButton.onClick.AddListener(HandleWinClaimed);
            continueCoinButton.onClick.AddListener(HandleReviveWithCoins);
            continueAdButton.onClick.AddListener(HandleReviveWithAd);
            continueCloseButton.onClick.AddListener(HandleContinueDeclined);
            loseReplayButton.onClick.AddListener(HandleReplay);
            loseCloseButton.onClick.AddListener(HandleReplay);
            Hide();
            return true;
        }

        private void OnDestroy()
        {
            if (winAdClaimButton != null) winAdClaimButton.onClick.RemoveListener(HandleWinAdClaimed);
            if (winClaimButton != null) winClaimButton.onClick.RemoveListener(HandleWinClaimed);
            if (continueCoinButton != null) continueCoinButton.onClick.RemoveListener(HandleReviveWithCoins);
            if (continueAdButton != null) continueAdButton.onClick.RemoveListener(HandleReviveWithAd);
            if (continueCloseButton != null) continueCloseButton.onClick.RemoveListener(HandleContinueDeclined);
            if (loseReplayButton != null) loseReplayButton.onClick.RemoveListener(HandleReplay);
            if (loseCloseButton != null) loseCloseButton.onClick.RemoveListener(HandleReplay);
        }

        public void Hide()
        {
            winPanel.Hide();
            continuePanel.Hide();
            losePanel.Hide();
        }

        /// Số coin đếm từ 0 lên, bắt đầu đúng lúc nút chứa số đó hiện.
        public void ShowWin(int reward, int adReward, float coinCountDuration)
        {
            continuePanel.Hide();
            losePanel.Hide();
            Sequence sequence = winPanel.Show(panelAnimation);
            AddCoinCount(sequence, winAdRewardText, adReward, (RectTransform)winAdClaimButton.transform, coinCountDuration);
            AddCoinCount(sequence, winRewardText, reward, (RectTransform)winClaimButton.transform, coinCountDuration);
        }

        private void AddCoinCount(Sequence sequence, TMP_Text text, int target, RectTransform owner, float duration)
        {
            float at = winPanel.RevealTimeOf(owner);
            // Nút không nằm trong danh sách hiện lần lượt: hiện số luôn, không đếm.
            if (at < 0f || duration <= 0f)
            {
                text.SetText("{0}", target);
                return;
            }
            text.SetText("0");
            int shown = 0;
            sequence.Insert(at, DOTween.To(() => shown, value =>
            {
                shown = value;
                text.SetText("{0}", value);
            }, target, duration).SetEase(Ease.OutQuad));
        }

        /// <paramref name="canAfford"/> false → nút trả coin bấm không được (vẫn hiện giá để người chơi biết cần bao nhiêu).
        public void ShowContinue(int coinCost, bool canAfford)
        {
            winPanel.Hide();
            losePanel.Hide();
            continueCostText.SetText("{0}", coinCost);
            continueCoinButton.interactable = canAfford;
            continuePanel.Show(panelAnimation);
        }

        public void ShowLose(string levelFormat, int level)
        {
            winPanel.Hide();
            continuePanel.Hide();
            loseLevelText.SetText(levelFormat, level);
            losePanel.Show(panelAnimation);
        }

        private static void CheckPanel(AnimatedPanel panel, string label, ref string missing)
        {
            if (panel == null) { missing += $" {label}"; return; }
            if (!panel.IsValid(out string panelMissing)) missing += $" {label}:{panelMissing.Trim()}";
        }

        private void HandleWinAdClaimed() => WinClaimed?.Invoke(true);
        private void HandleWinClaimed() => WinClaimed?.Invoke(false);
        private void HandleReviveWithCoins() => ReviveWithCoinsClicked?.Invoke();
        private void HandleReviveWithAd() => ReviveWithAdClicked?.Invoke();
        private void HandleContinueDeclined() => ContinueDeclined?.Invoke();
        private void HandleReplay() => ReplayClicked?.Invoke();
    }
}
