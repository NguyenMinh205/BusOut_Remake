using BusOut.Core;
using UnityEngine.UI;

namespace BusOut.Menu
{
    /// Nút Remove Ads trên trang Home: mở popup mua chung với giá tiền thật (MUA GIẢ — bấm mua là có ngay, không IAP: project CV,
    /// không lên store). Mua xong AdService tắt banner / interstitial / App Open; rewarded vẫn còn. Đã mua thì ẩn nút.
    /// MenuPresenter tạo, gọi Refresh khi về Menu, Dispose khi huỷ.
    public sealed class RemoveAdsPresenter
    {
        private readonly MenuConfig config;
        private readonly Button button;
        private readonly PurchasePopupView popup;
        private readonly SecureGameData gameData;

        public RemoveAdsPresenter(MenuConfig config, Button button, PurchasePopupView popup, SecureGameData gameData)
        {
            this.config = config;
            this.button = button;
            this.popup = popup;
            this.gameData = gameData;
            button.onClick.AddListener(Open);
            popup.BuyWithMoneyClicked += HandlePurchased;
            Refresh();
        }

        public void Dispose()
        {
            button.onClick.RemoveListener(Open);
            popup.BuyWithMoneyClicked -= HandlePurchased;
        }

        public void Refresh() => button.gameObject.SetActive(!gameData.noAds);

        private void Open() =>
            popup.ShowMoneyOffer(config.removeAdsTitle, config.removeAdsIcon, config.removeAdsDescription, config.removeAdsPriceText);

        private void HandlePurchased()
        {
            popup.Hide();
            AdService.SetNoAds(true);
            AudioService.Play(SoundId.BonusCoin);
            HapticService.Play(HapticType.Success);
            Refresh();
        }
    }
}
