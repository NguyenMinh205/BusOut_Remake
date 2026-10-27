using System;
using BusOut.Core;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Popup cuối ván: chờ một chút sau khi thắng / thua (gốc: 1.4s / 1.25s) rồi hiện.
    /// Thắng → WinPanel: nhận coin thưởng (thường hoặc nhân lên bằng quảng cáo) rồi sang level sau.
    /// Thua → còn ô khoá thì ContinuePanel (hồi sinh bằng coin / quảng cáo; đóng → LosePanel), hết ô khoá thì LosePanel.
    /// GameplayPresenter tạo, gọi Tick mỗi frame và Hide khi vào level.
    public sealed class LevelResultPresenter
    {
        private enum Pending { None, Win, Lose }

        private readonly GameplayConfig config;
        private readonly LevelResultView view;
        private readonly SecureGameData gameData;
        private readonly Func<bool> tryRevive;
        private readonly Action reloadLevel;

        private Pending pending;
        private float timer;
        private bool canRevive;
        private int level;
        private bool winClaimed; // chặn bấm 2 lần (2 nút / bấm đúp) → cộng coin 2 lần

        /// <paramref name="tryRevive"/>: mở thêm 1 ô và chơi tiếp; false khi không mở được.
        /// <paramref name="reloadLevel"/>: nạp level đang lưu (thắng thì save đã sang level sau).
        public LevelResultPresenter(GameplayConfig config, LevelResultView view, SecureGameData gameData,
            Func<bool> tryRevive, Action reloadLevel)
        {
            this.config = config;
            this.view = view;
            this.gameData = gameData;
            this.tryRevive = tryRevive;
            this.reloadLevel = reloadLevel;
            view.WinClaimed += HandleWinClaimed;
            view.ReviveWithCoinsClicked += HandleReviveWithCoins;
            view.ReviveWithAdClicked += HandleReviveWithAd;
            view.ContinueDeclined += HandleContinueDeclined;
            view.ReplayClicked += HandleReplay;
        }

        public void ShowWinLater(int levelNumber)
        {
            pending = Pending.Win;
            timer = config.winPopupDelay;
            level = levelNumber;
            winClaimed = false;
        }

        public void ShowLoseLater(int levelNumber, bool revivable)
        {
            pending = Pending.Lose;
            timer = config.losePopupDelay;
            level = levelNumber;
            canRevive = revivable;
        }

        /// Huỷ cả popup đang chờ hiện: chơi lại ngay trong lúc chờ thì popup của ván cũ không được bật lên.
        public void Hide()
        {
            pending = Pending.None;
            view.Hide();
        }

        public void Tick(float deltaTime)
        {
            if (pending == Pending.None) return;
            timer -= deltaTime;
            if (timer > 0f) return;
            if (pending == Pending.Win) view.ShowWin(config.winCoinReward, config.winCoinReward * config.winAdCoinMultiplier,
                config.popupCoinCountDuration);
            else if (canRevive) view.ShowContinue(config.reviveCoinCost, gameData.coinCount >= config.reviveCoinCost);
            else view.ShowLose(config.levelLabelFormat, level);
            pending = Pending.None;
        }

        /// ⚠️ TẠM: nhận bằng quảng cáo = nhận ngay (chưa có quảng cáo). Khi có: chỉ cộng khi AdCompletedEvent có thưởng.
        private void HandleWinClaimed(bool withAd)
        {
            if (winClaimed) return;
            winClaimed = true;
            int reward = config.winCoinReward * (withAd ? config.winAdCoinMultiplier : 1);
            gameData.AddCoins(reward);
            gameData.Save();
            reloadLevel();
        }

        private void HandleReviveWithCoins()
        {
            // Kiểm lại lúc bấm: coin có thể đã đổi từ lúc hiện popup.
            if (gameData.coinCount < config.reviveCoinCost) return;
            if (!tryRevive()) return;
            gameData.TrySpendCoins(config.reviveCoinCost);
            gameData.Save();
        }

        /// ⚠️ TẠM: hồi sinh ngay (chưa có quảng cáo). Khi có: chỉ hồi sinh khi AdCompletedEvent có thưởng.
        private void HandleReviveWithAd()
        {
            if (!tryRevive())
                Debug.LogWarning("[BusOut] Hồi sinh thất bại: không còn ô khoá để mở.");
        }

        private void HandleContinueDeclined() => view.ShowLose(config.levelLabelFormat, level);

        private void HandleReplay() => reloadLevel();
    }
}
