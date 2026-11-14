using System;
using BusOut.Core;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Popup cuối ván: chờ một chút sau khi thắng / thua (gốc: 1.4s / 1.25s) rồi hiện.
    /// Thắng → WinPanel: nhận coin thưởng (thường hoặc nhân lên bằng quảng cáo) rồi về Menu (như gốc: xe chạy sang bệ
    /// level mới trên bản đồ). Thua → còn ô khoá thì ContinuePanel (hồi sinh bằng coin / quảng cáo; đóng → LosePanel),
    /// hết ô khoá thì LosePanel (Replay → chơi lại, đóng → về Menu).
    /// GameplayPresenter tạo, gọi Tick mỗi frame và Hide khi vào level.
    public sealed class LevelResultPresenter
    {
        private enum Pending { None, Win, Lose }

        private readonly GameplayConfig config;
        private readonly LevelResultView view;
        private readonly SecureGameData gameData;
        private readonly Func<bool> tryRevive;
        private readonly Action reloadLevel;
        private readonly CoinRewardPlayer playCoinReward;

        /// Hiệu ứng nhận coin: số coin trước / sau khi cộng, chỗ coin bay ra; xong (hoặc không bay được) thì gọi onDone.
        public delegate void CoinRewardPlayer(int coinsBefore, int coinsAfter, Vector3 from, Action onDone);

        private Pending pending;
        private float timer;
        private bool canRevive;
        private int passengersLeft;
        private float levelProgress;
        private int level;
        private bool winClaimed; // chặn bấm 2 lần (2 nút / bấm đúp) → cộng coin 2 lần
        // Callback trao thưởng sau quảng cáo: dựng sẵn một lần.
        private readonly Action claimWinWithAd;
        private readonly Action reviveAfterAd;
        // Rời popup kết quả (về Menu / chơi lại) đi qua interstitial: AdService tự quyết hiện hay bỏ qua, xong mới đi tiếp.
        private readonly Action goHomeAfterInterstitial;

        /// <paramref name="tryRevive"/>: mở thêm 1 ô và chơi tiếp; false khi không mở được.
        /// <paramref name="reloadLevel"/>: nạp lại level đang lưu (Replay ở popup thua).
        /// <paramref name="goHome"/>: về Menu (nhận thưởng thắng — save đã sang level sau; đóng popup thua).
        /// <paramref name="playCoinReward"/>: coin bay về thanh trên trước khi về Menu (null = về luôn).
        public LevelResultPresenter(GameplayConfig config, LevelResultView view, SecureGameData gameData,
            Func<bool> tryRevive, Action reloadLevel, Action goHome, CoinRewardPlayer playCoinReward)
        {
            this.playCoinReward = playCoinReward;
            this.config = config;
            this.view = view;
            this.gameData = gameData;
            this.tryRevive = tryRevive;
            this.reloadLevel = reloadLevel;
            claimWinWithAd = () => ClaimWin(true);
            reviveAfterAd = ReviveAfterAd;
            goHomeAfterInterstitial = () => AdService.ShowInterstitial(AdPlacement.LevelEnd, goHome);
            view.WinClaimed += HandleWinClaimed;
            view.ReviveWithCoinsClicked += HandleReviveWithCoins;
            view.ReviveWithAdClicked += HandleReviveWithAd;
            view.ContinueDeclined += HandleContinueDeclined;
            view.ReplayClicked += HandleReplay;
            view.LoseClosed += HandleLoseClosed;
        }

        /// Đang đếm ngược chờ hiện popup thắng / thua.
        public bool IsPending => pending != Pending.None;

        public void ShowWinLater(int levelNumber)
        {
            pending = Pending.Win;
            timer = config.winPopupDelay;
            level = levelNumber;
            winClaimed = false;
        }

        /// <paramref name="remaining"/>: số khách chưa lên xe; <paramref name="progress"/>: tỉ lệ đã lên (0..1). Popup hồi sinh
        /// hiện "sắp thắng rồi" để người chơi thấy đáng hồi sinh.
        public void ShowLoseLater(int levelNumber, bool revivable, int remaining, float progress)
        {
            pending = Pending.Lose;
            timer = config.losePopupDelay;
            level = levelNumber;
            canRevive = revivable;
            passengersLeft = remaining;
            levelProgress = progress;
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
                config.popupCoinCountDuration,
                FxConfig.Instance.winRaysTurnDuration, FxConfig.Instance.winConfettiScale);
            else if (canRevive) view.ShowContinue(config.reviveCoinCost, gameData.coinCount >= config.reviveCoinCost, levelProgress,
                passengersLeft == 1 ? config.continueProgressFormatOne
                    : string.Format(config.continueProgressFormat, passengersLeft));
            else view.ShowLose(config.levelLabelFormat, level);
            // Tiếng thua phát một lần lúc popup đầu tiên hiện (Continue hoặc Lose), không phát lại khi từ chối Continue.
            AudioService.Play(pending == Pending.Win ? SoundId.WinGame : SoundId.LoseGame);
            HapticService.Play(pending == Pending.Win ? HapticType.Success : HapticType.Failure);
            pending = Pending.None;
        }

        /// Nhận thưởng thường → nhận ngay. Nhận x2 → mở rewarded, chỉ nhận khi xem đủ (đóng sớm: popup vẫn đó, chọn lại được).
        private void HandleWinClaimed(bool withAd)
        {
            if (winClaimed) return;
            if (withAd) AdService.ShowRewarded(AdPlacement.WinX2, claimWinWithAd);
            else ClaimWin(false);
        }

        private void ClaimWin(bool withAd)
        {
            // Kiểm lại: bấm "Nhận" ngay trước khi quảng cáo kịp che màn thì thưởng thường đã nhận rồi.
            if (winClaimed) return;
            winClaimed = true;
            int reward = config.winCoinReward * (withAd ? config.winAdCoinMultiplier : 1);
            int coinsBefore = gameData.coinCount;
            // Cộng + lưu NGAY, coin bay chỉ là hình: tắt app giữa lúc bay không mất thưởng.
            gameData.AddCoins(reward);
            gameData.Save();
            AudioService.Play(withAd ? SoundId.BonusCoin : SoundId.Coin);
            // Interstitial SAU coin bay (người chơi thấy thưởng về túi trước), trước khi về Menu.
            if (playCoinReward != null)
                playCoinReward(coinsBefore, gameData.coinCount, view.GetRewardPosition(withAd), goHomeAfterInterstitial);
            else goHomeAfterInterstitial();
        }

        private void HandleReviveWithCoins()
        {
            // Kiểm lại lúc bấm: coin có thể đã đổi từ lúc hiện popup.
            if (gameData.coinCount < config.reviveCoinCost) return;
            if (!tryRevive()) return;
            gameData.TrySpendCoins(config.reviveCoinCost);
            gameData.Save();
        }

        /// Hồi sinh bằng quảng cáo: chỉ hồi sinh khi xem đủ; đóng sớm thì popup Continue vẫn đó.
        private void HandleReviveWithAd() => AdService.ShowRewarded(AdPlacement.Revive, reviveAfterAd);

        private void ReviveAfterAd()
        {
            if (!tryRevive())
                Debug.LogWarning("[BusOut] Hồi sinh thất bại: không còn ô khoá để mở.");
        }

        private void HandleContinueDeclined() => view.ShowLose(config.levelLabelFormat, level);

        private void HandleReplay() => AdService.ShowInterstitial(AdPlacement.LevelEnd, reloadLevel);

        private void HandleLoseClosed() => goHomeAfterInterstitial();
    }
}
