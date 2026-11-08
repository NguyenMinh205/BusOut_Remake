using System;
using BusOut.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Menu
{
    /// Popup quà hằng ngày: 7 ô, nút Claim + x2, đồng hồ tới lúc reset chu kỳ, họa tiết xe trôi trên thẻ Day 7.
    /// Gắn trên HomeContext/UI/Popup/DailyRewardPanel. DailyRewardPresenter khởi tạo (object tắt sẵn nên không dùng Awake).
    public class DailyRewardView : MonoBehaviour
    {
        // Đồng hồ hiện tới giây: cập nhật mỗi giây là đủ, khỏi dựng chuỗi mỗi khung.
        private const float CountdownInterval = 1f;

        [SerializeField] private AnimatedPanel panel;
        [Tooltip("Nút Daily Reward trên trang Home.")]
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [Tooltip("ClaimButtons: chỉ hiện khi hôm nay còn quà.")]
        [SerializeField] private GameObject claimButtons;
        [SerializeField] private Button claimButton;
        [SerializeField] private Button claimWithAdButton;
        [Tooltip("Số đếm ngược trong ClockCountdown.")]
        [SerializeField] private TMP_Text countdownText;
        [Tooltip("NormalDayReward: các ô ngày 1–6 là con của nó, thứ tự trong Hierarchy = thứ tự ngày.")]
        [SerializeField] private Transform normalDaysRoot;
        [Tooltip("Thẻ ngày 7 (SpecialDayReward).")]
        [SerializeField] private DailyRewardItemView specialDay;
        [Tooltip("Image Tiled trong PatternMask của thẻ Day 7, rộng hơn mặt nạ đúng 1 ô lặp.")]
        [SerializeField] private Image pattern;

        private DailyRewardItemView[] days;
        private PanelAnimationSettings panelAnimation;
        private RectTransform patternRect;
        private float patternTileWidth;
        private float countdownTimer;

        public event Action OpenClicked;
        public event Action CloseClicked;
        public event Action ClaimClicked;
        public event Action ClaimWithAdClicked;
        public event Action<int> DayClicked;
        /// Mỗi giây lúc popup đang mở: Presenter cập nhật đồng hồ.
        public event Action SecondTicked;

        public bool IsOpen => panel.root.activeSelf;

        public bool Init(PanelAnimationSettings animationSettings, int dayCount)
        {
            panelAnimation = animationSettings;
            string missing = "";
            if (panel == null) missing += " Panel";
            else if (!panel.IsValid(out string panelMissing)) missing += $" Panel:{panelMissing.Trim()}";
            if (openButton == null) missing += " OpenButton";
            if (closeButton == null) missing += " CloseButton";
            if (claimButtons == null) missing += " ClaimButtons";
            if (claimButton == null) missing += " ClaimButton";
            if (claimWithAdButton == null) missing += " ClaimWithAdButton";
            if (countdownText == null) missing += " CountdownText";
            if (pattern == null) missing += " Pattern";
            if (specialDay == null) missing += " SpecialDay";
            if (normalDaysRoot == null) missing += " NormalDaysRoot";
            else
            {
                // true: các ô đang nằm trong popup tắt sẵn.
                DailyRewardItemView[] normalDays = normalDaysRoot.GetComponentsInChildren<DailyRewardItemView>(true);
                if (normalDays.Length != dayCount - 1) missing += $" NormalDays(có {normalDays.Length}, phải đủ {dayCount - 1} ô)";
                else if (specialDay != null)
                {
                    days = new DailyRewardItemView[dayCount];
                    normalDays.CopyTo(days, 0);
                    days[dayCount - 1] = specialDay;
                }
            }
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] DailyRewardView thiếu tham chiếu:{missing}. Kéo object trong DailyRewardPanel vào.", this);
                return false;
            }
            for (int i = 0; i < days.Length; i++)
            {
                if (!days[i].Init()) return false;
                int day = i;
                days[i].Clicked += () => DayClicked?.Invoke(day);
            }
            panel.Init();
            patternRect = pattern.rectTransform;
            openButton.onClick.AddListener(HandleOpen);
            closeButton.onClick.AddListener(HandleClose);
            claimButton.onClick.AddListener(HandleClaim);
            claimWithAdButton.onClick.AddListener(HandleClaimWithAd);
            panel.Hide();
            return true;
        }

        private void OnDestroy()
        {
            if (openButton != null) openButton.onClick.RemoveListener(HandleOpen);
            if (closeButton != null) closeButton.onClick.RemoveListener(HandleClose);
            if (claimButton != null) claimButton.onClick.RemoveListener(HandleClaim);
            if (claimWithAdButton != null) claimWithAdButton.onClick.RemoveListener(HandleClaimWithAd);
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            ScrollPattern(deltaTime);
            countdownTimer += deltaTime;
            if (countdownTimer < CountdownInterval) return;
            countdownTimer -= CountdownInterval;
            SecondTicked?.Invoke();
        }

        /// Dời sang trái, đủ một ô lặp thì nhảy về: hình trước và sau cú nhảy trùng khít nên nhìn như trôi liền.
        private void ScrollPattern(float deltaTime)
        {
            float speed = FxConfig.Instance.dailyPatternScrollSpeed;
            if (speed <= 0f) return;
            // ⚠️ Tính lúc đang bật: object tắt thì Image chưa thấy Canvas, pixelsPerUnit trả sai → ô lặp lệch, nhìn thấy nhảy.
            if (patternTileWidth <= 0f && pattern.sprite != null)
                patternTileWidth = pattern.sprite.rect.width / (pattern.pixelsPerUnit * pattern.pixelsPerUnitMultiplier);
            if (patternTileWidth <= 0f) return;
            Vector2 position = patternRect.anchoredPosition;
            position.x -= speed * deltaTime;
            if (position.x <= -patternTileWidth) position.x += patternTileWidth;
            patternRect.anchoredPosition = position;
        }

        public void Show() => panel.Show(panelAnimation);

        public void Hide() => panel.Hide();

        public int SlotCountOf(int day) => days[day].SlotCount;

        public void SetDayLabel(int day, string text) => days[day].SetDayLabel(text);

        public void SetReward(int day, int slot, Sprite icon, string amount) => days[day].SetReward(slot, icon, amount);

        public void HideUnusedSlots(int day, int usedSlots) => days[day].HideUnusedSlots(usedSlots);

        public void SetCellState(int day, DailyRewardCellState state, Color todayAmountColor) =>
            days[day].SetState(state, todayAmountColor);

        public void SetClaimButtonsVisible(bool visible) => claimButtons.SetActive(visible);

        public void SetCountdown(string text) => countdownText.SetText(text);

        private void HandleOpen() => OpenClicked?.Invoke();
        private void HandleClose() => CloseClicked?.Invoke();
        private void HandleClaim() => ClaimClicked?.Invoke();
        private void HandleClaimWithAd() => ClaimWithAdClicked?.Invoke();
    }
}
