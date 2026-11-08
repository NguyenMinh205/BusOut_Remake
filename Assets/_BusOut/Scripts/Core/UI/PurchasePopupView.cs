using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Core
{
    /// Popup mua CHUNG: một món hàng (tiêu đề, icon, mô tả, giá), mua bằng coin / quảng cáo / tiền thật / đóng. Chỉ hiển thị và
    /// báo nút bấm — bán gì, trao gì do presenter của nơi dùng quyết (trong ván: PurchasePresenter — lượt booster, ô đỗ;
    /// sau này shop ở Menu dùng lại chính View này). Đặt ở Core để mọi phần dùng được.
    /// Gắn trên SharedUI/PurchasePanel; SharedPopupsPresenter khởi tạo, nơi dùng lấy qua SharedPopupsPresenter.PurchasePopup. Panel có thể là chính object này (view không cần Update, tắt vẫn gọi được).
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
        [Tooltip("BuyBtn/Cost/IconCoin: ẩn khi bán bằng tiền thật.")]
        [SerializeField] private GameObject coinIcon;
        [Tooltip("Buttons/WatchAdsBtn: xem quảng cáo để nhận.")]
        [SerializeField] private Button adButton;
        [SerializeField] private Button closeButton;

        public event Action BuyWithCoinsClicked;
        public event Action BuyWithAdClicked;
        /// Bấm mua ở món bán bằng tiền thật (ShowMoneyOffer). Tách khỏi BuyWithCoinsClicked: PurchasePresenter trong ván nghe
        /// sự kiện coin, không được trừ coin / trao đồ của nó khi đang bán món khác.
        public event Action BuyWithMoneyClicked;
        public event Action CloseClicked;

        private bool isMoneyOffer;

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
            // Thiếu icon coin chỉ làm món tiền thật hiện thừa icon: báo nhưng popup vẫn chạy (đừng làm hỏng mua booster).
            if (coinIcon == null)
                Debug.LogError("[BusOut] PurchasePopupView thiếu Coin Icon: món bán bằng tiền thật vẫn hiện icon coin. " +
                               "Kéo PurchasePopup/Buttons/BuyBtn/Cost/IconCoin vào.", this);
            // Ô giá hẹp hơn chữ tiền ("$3.99"): không xuống dòng, chữ tràn đều hai bên vẫn nằm trong nút.
            costText.textWrappingMode = TextWrappingModes.NoWrap;
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
            SetMode(false);
            nameText.SetText(title);
            if (icon != null) iconImage.sprite = icon;
            descriptionText.SetText(description);
            costText.SetText("{0}", coinPrice);
            buyButton.interactable = canAfford;
            panel.Show(panelAnimation);
        }

        /// Món bán bằng tiền thật: nút mua hiện giá (<paramref name="priceText"/>, vd. "$3.99"), không icon coin, không nút quảng cáo.
        public void ShowMoneyOffer(string title, Sprite icon, string description, string priceText)
        {
            SetMode(true);
            nameText.SetText(title);
            if (icon != null) iconImage.sprite = icon;
            descriptionText.SetText(description);
            costText.SetText(priceText);
            buyButton.interactable = true;
            panel.Show(panelAnimation);
        }

        // Popup dùng chung: món coin mở sau món tiền thật phải có lại icon coin và nút quảng cáo.
        private void SetMode(bool moneyOffer)
        {
            isMoneyOffer = moneyOffer;
            if (coinIcon != null) coinIcon.SetActive(!moneyOffer);
            adButton.gameObject.SetActive(!moneyOffer);
        }

        public void Hide() => panel.Hide();

        private void HandleBuyWithCoins()
        {
            if (isMoneyOffer) BuyWithMoneyClicked?.Invoke();
            else BuyWithCoinsClicked?.Invoke();
        }

        private void HandleBuyWithAd() => BuyWithAdClicked?.Invoke();
        private void HandleClose()
        {
            // Món tiền thật mở từ Menu: tự đóng, không trông vào presenter trong ván (người nghe CloseClicked hiện có).
            if (isMoneyOffer) Hide();
            CloseClicked?.Invoke();
        }
    }
}
