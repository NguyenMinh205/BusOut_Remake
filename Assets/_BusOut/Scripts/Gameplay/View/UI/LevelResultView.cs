using System;
using BusOut.Core;
using Coffee.UIExtensions;
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
        [Tooltip("Tia sáng sau phần thưởng (Image sprite UI_Fx_Light_white): xoay đều khi popup thắng hiện — thứ lặp DUY NHẤT " +
                 "lúc đó (quy tắc hiệu ứng). Trống = không có (báo lỗi).")]
        [SerializeField] private RectTransform winRays;
        [Tooltip("Pháo giấy: object có UI Particle (con: prefab WinConfetti). Bắn một lần khi popup thắng hiện. Trống = không có " +
                 "(báo lỗi).")]
        [SerializeField] private UIParticle winConfetti;

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
        [Tooltip("LosePopup/CloseBtn: về Menu.")]
        [SerializeField] private Button loseCloseButton;

        /// true = nhận thêm bằng quảng cáo.
        public event Action<bool> WinClaimed;
        public event Action ReviveWithCoinsClicked;
        public event Action ReviveWithAdClicked;
        public event Action ContinueDeclined;
        public event Action ReplayClicked;
        public event Action LoseClosed;

        private PanelAnimationSettings panelAnimation;
        private Tween raysTween;
        private Quaternion raysRestRotation;

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

            // Hiệu ứng thắng là phần thêm: thiếu thì báo nhưng popup vẫn chạy.
            if (winRays == null || winConfetti == null)
                Debug.LogError("[BusOut] LevelResultView thiếu Win Rays / Win Confetti: popup thắng không có tia sáng / pháo giấy. " +
                               "Kéo Image tia sáng và object UI Particle pháo giấy trong WinPanel vào.", this);
            if (winRays != null) raysRestRotation = winRays.localRotation;
            if (winConfetti != null)
            {
                // Gom mọi Particle System con (2 prefab pháo giấy lồng trong scene): không dựa vào danh sách lưu sẵn của UI
                // Particle — sửa prefab con (thêm / bớt hệ) là danh sách lưu cũ đi.
                winConfetti.RefreshParticles();
                winConfetti.Stop();
                winConfetti.Clear();
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
            loseCloseButton.onClick.AddListener(HandleLoseClosed);
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
            if (loseCloseButton != null) loseCloseButton.onClick.RemoveListener(HandleLoseClosed);
        }

        public void Hide()
        {
            StopWinEffects();
            winPanel.Hide();
            continuePanel.Hide();
            losePanel.Hide();
        }

        /// Số coin đếm từ 0 lên, bắt đầu đúng lúc nút chứa số đó hiện. Tia sáng xoay một vòng mỗi
        /// <paramref name="raysTurnDuration"/> giây tới khi popup ẩn; pháo giấy bắn một lần.
        public void ShowWin(int reward, int adReward, float coinCountDuration, float raysTurnDuration, float confettiScale)
        {
            if (winConfetti != null) winConfetti.scale3D = Vector3.one * confettiScale;
            continuePanel.Hide();
            losePanel.Hide();
            Sequence sequence = winPanel.Show(panelAnimation);
            AddCoinCount(sequence, winAdRewardText, adReward, (RectTransform)winAdClaimButton.transform, coinCountDuration);
            AddCoinCount(sequence, winRewardText, reward, (RectTransform)winClaimButton.transform, coinCountDuration);
            PlayWinEffects(raysTurnDuration);
        }

        private void PlayWinEffects(float raysTurnDuration)
        {
            StopWinEffects();
            if (winRays != null && raysTurnDuration > 0f)
                raysTween = winRays.DOLocalRotate(new Vector3(0f, 0f, -360f), raysTurnDuration, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Restart)
                    // Nhận thưởng → về Menu: cả GameContext tắt mà không gọi Hide — tạm dừng thay vì quay vô ích tới ván sau
                    // (ván sau LoadLevel → Hide → StopWinEffects huỷ hẳn).
                    .SetLink(winRays.gameObject, LinkBehaviour.PauseOnDisablePlayOnEnable)
                    .SetTarget(winRays);
            if (winConfetti != null) winConfetti.Play();
        }

        /// Dừng xoay (về góc dựng trong scene) + xoá pháo giấy. Gọi mỗi khi popup thắng ẩn / đổi sang popup khác.
        private void StopWinEffects()
        {
            if (raysTween != null && raysTween.IsActive()) raysTween.Kill();
            raysTween = null;
            if (winRays != null) winRays.localRotation = raysRestRotation;
            if (winConfetti == null) return;
            winConfetti.Stop();
            winConfetti.Clear();
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
            StopWinEffects();
            winPanel.Hide();
            losePanel.Hide();
            continueCostText.SetText("{0}", coinCost);
            continueCoinButton.interactable = canAfford;
            continuePanel.Show(panelAnimation);
        }

        public void ShowLose(string levelFormat, int level)
        {
            StopWinEffects();
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
        private void HandleLoseClosed() => LoseClosed?.Invoke();
    }
}
