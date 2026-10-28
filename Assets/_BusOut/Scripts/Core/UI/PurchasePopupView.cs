using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Core
{
    /// Popup mua CHUNG: một món hàng (tiêu đề, icon, mô tả, giá coin), mua bằng coin / quảng cáo / đóng. Chỉ hiển thị và
    /// báo nút bấm — bán gì, trao gì do presenter của nơi dùng quyết (trong ván: PurchasePresenter — lượt booster, ô đỗ;
    /// sau này shop ở Menu dùng lại chính View này). Đặt ở Core để mọi phần dùng được.
    /// Gắn trên Popup/PurchasePanel. Panel có thể là chính object này (view không cần Update, tắt vẫn gọi được).
    public class PurchasePopupView : MonoBehaviour
    {
        [SerializeField] private AnimatedPanel panel;
        [Tooltip("PurchasePopup/TitleTxt: tên món hàng.")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("Description/IconImg.")]
        [SerializeField] private Image iconImage;
        [Tooltip("Description/DescriptionTxt.")]
        [SerializeField] private TMP_Text descriptionText;
        [Tooltip("Buttons/BuyBtn: mua bằng coin.")]
        [SerializeField] private Button buyButton;
        [Tooltip("BuyBtn/Cost/CostTxt: giá coin.")]
        [SerializeField] private TMP_Text costText;
        [Tooltip("Buttons/WatchAdsBtn: xem quảng cáo để nhận.")]
        [SerializeField] private Button adButton;
        [SerializeField] private Button closeButton;

        public event Action BuyWithCoinsClicked;
        public event Action BuyWithAdClicked;
        public event Action CloseClicked;

        private PanelAnimationSettings panelAnimation;

        public bool Init(PanelAnimationSettings animationSettings)
        {
            panelAnimation = animationSettings;
            string missing = "";
            if (panel == null) missing += " Panel";
            else if (!panel.IsValid(out string panelMissing)) missing += $" Panel:{panelMissing.Trim()}";
            if (nameText == null) missing += " NameText";
            if (iconImage == null) missing += " IconImage";
            if (descriptionText == null) missing += " DescriptionText";
            if (buyButton == null) missing += " BuyButton";
            if (costText == null) missing += " CostText";
            if (adButton == null) missing += " AdButton";
            if (closeButton == null) missing += " CloseButton";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] PurchasePopupView thiếu tham chiếu:{missing}. Kéo object trong PurchasePanel vào.", this);
                return false;
            }
            panel.Init();
            buyButton.onClick.AddListener(HandleBuyWithCoins);
            adButton.onClick.AddListener(HandleBuyWithAd);
            closeButton.onClick.AddListener(HandleClose);
            Hide();
            return true;
        }

        private void OnDestroy()
        {
            if (buyButton != null) buyButton.onClick.RemoveListener(HandleBuyWithCoins);
            if (adButton != null) adButton.onClick.RemoveListener(HandleBuyWithAd);
            if (closeButton != null) closeButton.onClick.RemoveListener(HandleClose);
        }

        /// <paramref name="canAfford"/> false → nút mua bằng coin bấm không được (vẫn hiện giá để biết cần bao nhiêu).
        /// Icon null → giữ icon đang có.
        public void Show(string title, Sprite icon, string description, int coinPrice, bool canAfford)
        {
            nameText.SetText(title);
            if (icon != null) iconImage.sprite = icon;
            descriptionText.SetText(description);
            costText.SetText("{0}", coinPrice);
            buyButton.interactable = canAfford;
            panel.Show(panelAnimation);
        }

        public void Hide() => panel.Hide();

        private void HandleBuyWithCoins() => BuyWithCoinsClicked?.Invoke();
        private void HandleBuyWithAd() => BuyWithAdClicked?.Invoke();
        private void HandleClose() => CloseClicked?.Invoke();
    }
}
