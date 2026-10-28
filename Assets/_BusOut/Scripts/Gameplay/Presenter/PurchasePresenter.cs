using System;
using BusOut.Core;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Bán hàng trong ván qua popup mua chung (PurchasePopupView trên Popup/PurchasePanel), 2 món:
    /// - Thêm lượt booster: mở khi bấm booster đã mở khoá mà hết lượt. Mua xong chỉ cộng lượt — không tự dùng.
    /// - Mở thêm ô đỗ (A3): mở khi chạm ô khoá ở bến. Mua xong mở ô khoá gần nhất, chỉ cho level đang chơi.
    /// Mua bằng coin (trừ coin, lưu) hoặc quảng cáo (TẠM: nhận ngay).
    /// GameplayPresenter tạo; HudPresenter gọi Open, GameplayPresenter gọi OpenSlotUnlock; vào level / kết thúc ván thì Hide.
    public sealed class PurchasePresenter
    {
        private enum Offer { None, Booster, Slot }

        private readonly GameplayConfig config;
        private readonly SecureGameData gameData;
        private readonly PurchasePopupView view;
        private readonly Func<bool> canOpen;
        private readonly Func<bool> unlockSlot;

        private Offer offer;               // món đang mở popup; None = popup đang tắt
        private BoosterDefinition booster; // khi offer = Booster

        /// <paramref name="canOpen"/>: lúc này có được mở popup không (đang chơi, không trong hoạt ảnh vào level).
        /// <paramref name="unlockSlot"/>: mở ô khoá gần nhất; false khi không còn ô khoá.
        public PurchasePresenter(GameplayConfig config, SecureGameData gameData, PurchasePopupView view, Func<bool> canOpen,
            Func<bool> unlockSlot)
        {
            this.config = config;
            this.gameData = gameData;
            this.view = view;
            this.canOpen = canOpen;
            this.unlockSlot = unlockSlot;
            view.BuyWithCoinsClicked += HandleBuyWithCoins;
            view.BuyWithAdClicked += HandleBuyWithAd;
            view.CloseClicked += Hide;
        }

        /// Popup thêm lượt booster. False khi không mở được (sai thời điểm / booster không có trong config) — nút rung.
        public bool Open(BoosterType type)
        {
            if (!canOpen()) return false;
            BoosterDefinition definition = config.boosters.Find(b => b.type == type);
            if (definition == null)
            {
                Debug.LogError($"[BusOut] GameplayConfig.boosters thiếu {type}: không mở được popup mua.");
                return false;
            }
            offer = Offer.Booster;
            booster = definition;
            view.Show(definition.displayName, definition.icon, definition.description, definition.coinPrice,
                gameData.coinCount >= definition.coinPrice);
            return true;
        }

        /// Popup mở thêm ô đỗ (chạm ô khoá ở bến). False khi không mở được lúc này.
        public bool OpenSlotUnlock()
        {
            if (!canOpen()) return false;
            offer = Offer.Slot;
            booster = null;
            view.Show(config.slotUnlockName, config.slotUnlockIcon, config.slotUnlockDescription, config.slotUnlockCoinCost,
                gameData.coinCount >= config.slotUnlockCoinCost);
            return true;
        }

        public void Hide()
        {
            offer = Offer.None;
            booster = null;
            view.Hide();
        }

        private void HandleBuyWithCoins()
        {
            // Kiểm lại lúc bấm: coin có thể đã đổi từ lúc mở popup. Chặn bấm đúp: Hide đặt offer = None.
            int price = offer == Offer.Booster ? booster.coinPrice : config.slotUnlockCoinCost;
            if (offer == Offer.None || gameData.coinCount < price) return;
            // Ô: mở trước rồi mới trừ coin — không còn ô khoá (vd. vừa hồi sinh) thì không mất coin.
            if (offer == Offer.Slot && !unlockSlot())
            {
                Hide();
                return;
            }
            gameData.TrySpendCoins(price);
            Grant();
        }

        /// ⚠️ TẠM: nhận ngay (chưa có quảng cáo). Khi có: chỉ trao khi AdCompletedEvent có thưởng.
        private void HandleBuyWithAd()
        {
            if (offer == Offer.None) return;
            if (offer == Offer.Slot && !unlockSlot())
            {
                Hide();
                return;
            }
            Grant();
        }

        /// Ô đã mở trong HandleBuy*; ở đây cộng lượt booster (nếu có), lưu, đóng popup.
        private void Grant()
        {
            if (offer == Offer.Booster)
            {
                gameData.AddBooster(booster.type, booster.buyAmount);
                GameLog.Info($"[BusOut] Mua booster {booster.type} +{booster.buyAmount}.");
            }
            else
            {
                GameLog.Info("[BusOut] Mở thêm 1 ô đỗ cho level này.");
            }
            gameData.Save();
            Hide();
        }
    }
}
