using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Gameplay
{
    /// Một nút booster (prefab UI/GameContext/Booster): khoá thì hiện ổ khoá + "LEVEL n",
    /// mở thì hiện icon + tên + số lượt ở huy hiệu góc.
    public class BoosterButtonView : MonoBehaviour
    {
        [Tooltip("Button trên root Booster.")]
        [SerializeField] private Button button;
        [Tooltip("Booster/Background.")]
        [SerializeField] private Image background;
        [Tooltip("Booster/IconBooster.")]
        [SerializeField] private Image icon;
        [Tooltip("Booster/NameBooster.")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("Booster/NumOfBooster (huy hiệu số lượt, ẩn khi khoá).")]
        [SerializeField] private GameObject countBadge;
        [Tooltip("Booster/NumOfBooster/NumOfBoosterTxt.")]
        [SerializeField] private TMP_Text countText;

        public event Action<BoosterButtonView> Clicked;

        public bool Init()
        {
            string missing = "";
            if (button == null) missing += " Button";
            if (background == null) missing += " Background";
            if (icon == null) missing += " Icon";
            if (nameText == null) missing += " NameText";
            if (countBadge == null) missing += " CountBadge";
            if (countText == null) missing += " CountText";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] BoosterButtonView '{name}' thiếu tham chiếu:{missing}. Mở prefab Booster và kéo các object con vào.", this);
                return false;
            }
            button.onClick.AddListener(HandleClicked);
            return true;
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClicked);
        }

        /// Sprite null = giữ sprite đang có (chưa chọn sprite trong config thì nút vẫn hiện được).
        public void ShowLocked(Sprite lockIcon, Sprite lockedBackground, string labelFormat, int unlockLevel)
        {
            SetSprite(icon, lockIcon);
            SetSprite(background, lockedBackground);
            nameText.SetText(labelFormat, unlockLevel);
            countBadge.SetActive(false);
        }

        public void ShowUnlocked(Sprite boosterIcon, Sprite unlockedBackground, string displayName, int count)
        {
            SetSprite(icon, boosterIcon);
            SetSprite(background, unlockedBackground);
            nameText.SetText(displayName);
            countBadge.SetActive(true);
            SetCount(count);
        }

        /// Hết lượt thì hiện "+" (gốc: chạm để mua thêm bằng coin / quảng cáo).
        public void SetCount(int count)
        {
            if (count > 0) countText.SetText("{0}", count);
            else countText.SetText("+");
        }

        private static void SetSprite(Image image, Sprite sprite)
        {
            if (sprite != null) image.sprite = sprite;
        }

        private void HandleClicked() => Clicked?.Invoke(this);
    }
}
