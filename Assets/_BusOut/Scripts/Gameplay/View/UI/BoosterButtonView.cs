using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Gameplay
{
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

        private Vector3 baseScale = Vector3.one;

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
            // ⚠️ Nhớ cỡ gốc trong scene: SetArmed thu về cỡ này (thu về 1 là méo nút nếu prefab đặt scale khác 1).
            baseScale = transform.localScale;
            return true;
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClicked);
        }

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

        /// Đang "giương" (Crane chờ chọn xe): phóng to và đứng yên; thôi giương thì về cỡ gốc. Không lặp (không pulse).
        public void SetArmed(bool armed, float armedScale)
        {
            transform.DOKill();
            transform.DOScale(baseScale * (armed ? armedScale : 1f), 0.15f)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject)
                .SetTarget(transform);
        }

        /// Bấm mà không dùng được: rung ngang ngắn, không đổi gì khác.
        public void PlayDenied(float distance)
        {
            // Complete cú rung trước (nếu bấm liên tục) để nút về đúng chỗ rồi mới rung tiếp.
            transform.DOComplete();
            transform.DOPunchPosition(Vector3.right * distance, 0.3f, 10, 0.5f)
                .SetLink(gameObject)
                .SetTarget(transform);
        }

        private void HandleClicked() => Clicked?.Invoke(this);
    }
}
