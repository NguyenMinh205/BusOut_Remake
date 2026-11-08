using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Menu
{
    /// Một ô trong popup quà hằng ngày (prefab DailyRewardObj cho ngày 1–6, SpecialDayReward cho ngày 7).
    /// Ba trạng thái: chưa tới lượt / hôm nay (nền sáng + quầng vàng đứng yên phía sau) / đã nhận (phủ tối + dấu tích).
    public class DailyRewardItemView : MonoBehaviour
    {
        /// Chỗ hiện một món quà: icon + số lượng.
        [Serializable]
        public class RewardSlot
        {
            public Image icon;
            public TMP_Text amount;
        }

        [Tooltip("Có thể để trống (ngày 7 không bấm vào thẻ, chỉ dùng nút Claim).")]
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite todaySprite;
        [SerializeField] private GameObject highlight;
        [SerializeField] private GameObject claimedPanel;
        [SerializeField] private TMP_Text dayText;
        [Tooltip("Ngày 1–6: 1 chỗ. Ngày 7: 2 chỗ.")]
        [SerializeField] private RewardSlot[] rewardSlots;
        [Tooltip("Dấu \"+\" giữa 2 món (chỉ ngày 7).")]
        [SerializeField] private GameObject plusSign;

        public event Action Clicked;

        public int SlotCount => rewardSlots != null ? rewardSlots.Length : 0;

        private Color[] normalAmountColors;

        /// False khi thiếu tham chiếu (đã báo lỗi).
        public bool Init()
        {
            string missing = "";
            if (background == null) missing += " Background";
            if (normalSprite == null) missing += " NormalSprite";
            if (todaySprite == null) missing += " TodaySprite";
            if (highlight == null) missing += " Highlight";
            if (claimedPanel == null) missing += " ClaimedPanel";
            if (dayText == null) missing += " DayText";
            if (SlotCount == 0) missing += " RewardSlots";
            for (int i = 0; i < SlotCount; i++)
                if (rewardSlots[i] == null || rewardSlots[i].icon == null || rewardSlots[i].amount == null)
                    missing += $" RewardSlots[{i}]";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] DailyRewardItemView '{name}' thiếu tham chiếu:{missing}. Kéo object con vào.", this);
                return false;
            }
            // Nhớ màu dựng trong scene / prefab để trả lại khi ô hết là "hôm nay".
            normalAmountColors = new Color[SlotCount];
            for (int i = 0; i < SlotCount; i++) normalAmountColors[i] = rewardSlots[i].amount.color;
            if (button != null) button.onClick.AddListener(HandleClick);
            return true;
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
        }

        public void SetDayLabel(string text) => dayText.SetText(text);

        public void SetReward(int slot, Sprite icon, string amount)
        {
            if (slot < 0 || slot >= SlotCount) return;
            RewardSlot target = rewardSlots[slot];
            target.icon.gameObject.SetActive(true);
            target.amount.gameObject.SetActive(true);
            if (icon != null) target.icon.sprite = icon;
            target.amount.SetText(amount);
        }

        /// Ẩn các chỗ từ <paramref name="usedSlots"/> trở đi; dấu "+" chỉ hiện khi có từ 2 món.
        public void HideUnusedSlots(int usedSlots)
        {
            for (int i = usedSlots; i < SlotCount; i++)
            {
                rewardSlots[i].icon.gameObject.SetActive(false);
                rewardSlots[i].amount.gameObject.SetActive(false);
            }
            if (plusSign != null) plusSign.SetActive(usedSlots > 1);
        }

        public void SetState(DailyRewardCellState state, Color todayAmountColor)
        {
            bool isToday = state == DailyRewardCellState.Today;
            background.sprite = isToday ? todaySprite : normalSprite;
            highlight.SetActive(isToday);
            for (int i = 0; i < SlotCount; i++)
                rewardSlots[i].amount.color = isToday ? todayAmountColor : normalAmountColors[i];
            claimedPanel.SetActive(state == DailyRewardCellState.Claimed);
        }

        private void HandleClick() => Clicked?.Invoke();
    }
}
