using System;
using BusOut.Core;
using BusOut.DesignPattern;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Giao diện trong ván: coin + level ở thanh trên, nút booster ở thanh dưới (khoá / mở theo level, số lượt).
    /// Đọc số từ SecureGameData, cập nhật theo sự kiện (CurrencyUpdatedEvent, BoosterCountChangedEvent, LevelStartedEvent)
    /// nên ai đổi coin / booster ở đâu thì giao diện cũng tự đúng. GameplayPresenter tạo và gọi Dispose khi huỷ.
    public sealed class HudPresenter : IDisposable
    {
        private readonly GameplayConfig config;
        private readonly SecureGameData gameData;
        private bool isCountingCoins;
        private readonly TopBarView topBar;         // null khi thiếu: phần còn lại vẫn chạy
        private readonly BoosterBarView boosterBar; // null khi thiếu
        private readonly Action replayRequested;
        private readonly Func<BoosterType, bool> boosterRequested;
        private readonly Func<BoosterType, bool> shopRequested;
        private readonly Action settingsRequested;

        // Dựng sẵn delegate: Subscribe / Unsubscribe phải cùng một instance.
        private readonly Action<CurrencyUpdatedEvent> onCurrencyUpdated;
        private readonly Action<BoosterCountChangedEvent> onBoosterCountChanged;
        private readonly Action<LevelStartedEvent> onLevelStarted;

        private int shownLevel = 1;

        /// <paramref name="boosterRequested"/>: dùng booster; false = không dùng được (nút rung, không mất lượt).
        /// <paramref name="shopRequested"/>: mở popup mua khi hết lượt; false = không mở được (nút rung).
        public HudPresenter(GameplayConfig config, SecureGameData gameData, TopBarView topBar, BoosterBarView boosterBar,
            Action replayRequested, Func<BoosterType, bool> boosterRequested, Func<BoosterType, bool> shopRequested,
            Action settingsRequested)
        {
            this.config = config;
            this.gameData = gameData;
            this.topBar = topBar;
            this.boosterBar = boosterBar;
            this.replayRequested = replayRequested;
            this.boosterRequested = boosterRequested;
            this.shopRequested = shopRequested;
            this.settingsRequested = settingsRequested;

            if (topBar != null)
            {
                topBar.ReplayClicked += HandleReplayClicked;
                topBar.SettingsClicked += HandleSettingsClicked;
            }
            if (boosterBar != null)
            {
                ValidateBoosterCount();
                for (int i = 0; i < boosterBar.Count; i++) boosterBar.GetButton(i).Clicked += HandleBoosterClicked;
            }

            onCurrencyUpdated = HandleCurrencyUpdated;
            onBoosterCountChanged = HandleBoosterCountChanged;
            onLevelStarted = HandleLevelStarted;
            EventBus.Subscribe(onCurrencyUpdated);
            EventBus.Subscribe(onBoosterCountChanged);
            EventBus.Subscribe(onLevelStarted);

            topBar?.SetCoins(gameData.coinCount);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe(onCurrencyUpdated);
            EventBus.Unsubscribe(onBoosterCountChanged);
            EventBus.Unsubscribe(onLevelStarted);
        }

        private void HandleLevelStarted(LevelStartedEvent e)
        {
            shownLevel = e.LevelIndex;
            topBar?.SetLevel(config.levelLabelFormat, shownLevel);
            RefreshBoosters();
        }

        private void HandleCurrencyUpdated(CurrencyUpdatedEvent e)
        {
            // Coin thưởng đang bay: số đếm theo coin tới nơi, không nhảy thẳng lên tổng mới.
            if (e.Type == CurrencyType.SoftCurrency && !isCountingCoins) topBar?.SetCoins(e.NewTotalAmount);
        }

        /// Coin thưởng bắt đầu bay: số trên thanh giữ ở <paramref name="shownCoins"/> (coin trong save đã cộng rồi).
        public void BeginCoinCountUp(int shownCoins)
        {
            isCountingCoins = true;
            topBar?.SetCoins(shownCoins);
        }

        public void ShowCountingCoins(int coins) => topBar?.SetCoins(coins);

        /// Bay xong: hiện đúng số trong save.
        public void EndCoinCountUp()
        {
            isCountingCoins = false;
            topBar?.SetCoins(gameData.coinCount);
        }

        private void HandleBoosterCountChanged(BoosterCountChangedEvent e)
        {
            if (boosterBar == null) return;
            int index = FindBoosterIndex(e.Type);
            if (index >= 0 && IsUnlocked(index)) boosterBar.GetButton(index).SetCount(e.Count);
        }

        /// Booster đổi trạng thái giương (Crane chờ chọn xe): nút phóng to / thu lại.
        public void SetBoosterArmed(BoosterType type, bool armed)
        {
            if (boosterBar == null) return;
            int index = FindBoosterIndex(type);
            if (index >= 0) boosterBar.GetButton(index).SetArmed(armed, config.boosterArmedScale);
        }

        /// Nút của một booster; null khi không có thanh booster hoặc không có nút cho loại đó.
        public RectTransform GetBoosterRect(BoosterType type)
        {
            if (boosterBar == null) return null;
            int index = FindBoosterIndex(type);
            return index >= 0 ? boosterBar.GetButton(index).transform as RectTransform : null;
        }

        private void RefreshBoosters()
        {
            if (boosterBar == null) return;
            int count = Mathf.Min(boosterBar.Count, config.boosters.Count);
            for (int i = 0; i < count; i++)
            {
                BoosterDefinition booster = config.boosters[i];
                BoosterButtonView button = boosterBar.GetButton(i);
                if (IsUnlocked(i))
                    button.ShowUnlocked(booster.icon, config.boosterUnlockedBackground, booster.displayName,
                        gameData.GetBoosterCount(booster.type));
                else
                    button.ShowLocked(config.boosterLockIcon, config.boosterLockedBackground,
                        config.boosterLockedLabelFormat, booster.unlockLevel);
            }
        }

        private void HandleBoosterClicked(BoosterButtonView button)
        {
            int index = IndexOf(button);
            if (index < 0 || index >= config.boosters.Count || !IsUnlocked(index)) return;
            BoosterDefinition booster = config.boosters[index];
            // Hết lượt (huy hiệu "+") → mở popup mua; còn lượt → dùng. Số lượt trên nút tự cập nhật qua
            // BoosterCountChangedEvent khi dùng / mua thành công.
            Func<BoosterType, bool> action = gameData.GetBoosterCount(booster.type) > 0 ? boosterRequested : shopRequested;
            if (action == null || !action(booster.type)) button.PlayDenied(config.boosterDeniedShake);
        }

        private void HandleReplayClicked() => replayRequested?.Invoke();

        private void HandleSettingsClicked() => settingsRequested?.Invoke();

        private bool IsUnlocked(int boosterIndex) => shownLevel >= config.boosters[boosterIndex].unlockLevel;

        private int FindBoosterIndex(BoosterType type)
        {
            for (int i = 0; i < config.boosters.Count && i < boosterBar.Count; i++)
                if (config.boosters[i].type == type) return i;
            return -1;
        }

        private int IndexOf(BoosterButtonView button)
        {
            for (int i = 0; i < boosterBar.Count; i++)
                if (boosterBar.GetButton(i) == button) return i;
            return -1;
        }

        private void ValidateBoosterCount()
        {
            if (boosterBar.Count == config.boosters.Count) return;
            Debug.LogError($"[BusOut] Có {boosterBar.Count} nút booster nhưng GameplayConfig.boosters có {config.boosters.Count} " +
                           "phần tử: nút thừa không được cập nhật. Sửa cho bằng nhau.", boosterBar);
        }
    }
}
