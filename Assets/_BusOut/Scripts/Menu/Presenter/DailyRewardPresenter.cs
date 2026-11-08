using System;
using System.Collections.Generic;
using BusOut.Core;
using BusOut.DesignPattern;
using UnityEngine;

namespace BusOut.Menu
{
    /// Quà hằng ngày: chu kỳ 7 ngày, mỗi ngày mới mở thêm một ô, hết chu kỳ reset về ô 1 (đồng hồ đếm tới lúc reset);
    /// nhận thường hoặc nhân lên bằng quảng cáo.
    /// Mỗi lần vào Menu (từ level autoOpenFromLevel) mà hôm nay chưa nhận thì popup tự mở: MenuPresenter gọi TryAutoOpen đúng lúc;
    /// nhận rồi thì thôi tới ngày mai. Nút Daily Reward luôn mở được.
    /// Giờ lấy từ TimeService. Mất mạng vẫn nhận được bằng giờ máy (mức cơ bản: project CV, không lên store): tua giờ máy
    /// tới lúc mất mạng chỉ nhận SỚM được, có mạng lại là khoá tới đúng ngày đó (xem DailyRewardProgress).
    /// MenuPresenter tạo, gọi Dispose khi huỷ.
    public sealed class DailyRewardPresenter
    {
        private readonly DailyRewardConfig config;
        private readonly DailyRewardView view;
        private readonly SecureGameData gameData;
        private readonly DailyRewardProgress progress;
        private readonly Action claimAfterAd;

        public DailyRewardPresenter(DailyRewardConfig config, DailyRewardView view, SecureGameData gameData)
        {
            this.config = config;
            this.view = view;
            this.gameData = gameData;
            progress = new DailyRewardProgress(gameData, DailyRewardConfig.DayCount);
            claimAfterAd = () => Claim(config.adRewardMultiplier);
            FillRewards();
            view.OpenClicked += Open;
            view.CloseClicked += view.Hide;
            view.ClaimClicked += HandleClaim;
            view.ClaimWithAdClicked += HandleClaimWithAd;
            view.DayClicked += HandleDayClicked;
            view.SecondTicked += RefreshCountdown;
            EventBus.Subscribe<NewDayStartedEvent>(HandleNewDayStarted);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<NewDayStartedEvent>(HandleNewDayStarted);
        }

        private static int Today => TimeService.Instance.TodayIndex;

        private void FillRewards()
        {
            for (int day = 0; day < DailyRewardConfig.DayCount; day++)
            {
                view.SetDayLabel(day, string.Format(config.dayLabelFormat, day + 1));
                List<DailyRewardItem> rewards = config.days[day].rewards;
                int slots = view.SlotCountOf(day);
                if (rewards.Count > slots)
                    Debug.LogError($"[BusOut] DailyRewardConfig: Day {day + 1} có {rewards.Count} món nhưng ô chỉ hiện được {slots}: " +
                                   "món thừa vẫn được nhận nhưng không hiện. Bớt món trong config.");
                int used = Mathf.Min(rewards.Count, slots);
                for (int i = 0; i < used; i++)
                    view.SetReward(day, i, rewards[i].icon, AmountText(rewards[i]));
                view.HideUnusedSlots(day, used);
            }
        }

        private string AmountText(DailyRewardItem item) =>
            string.Format(item.kind == DailyRewardKind.Coin ? config.coinAmountFormat : config.boosterAmountFormat, item.amount);

        /// Vào Menu: còn quà chưa nhận hôm nay thì mở popup. Đã nhận (hoặc đủ 7 ô) thì không làm gì.
        public void TryAutoOpen()
        {
            int today = Today;
            progress.EnsureCycle(today);
            if (view.IsOpen || gameData.currentLevel < config.autoOpenFromLevel || !progress.CanClaim(today)) return;
            Open();
        }

        /// Qua 0h lúc đang mở popup: ô hôm nay đổi ngay (có thể sang cả chu kỳ mới).
        private void HandleNewDayStarted(NewDayStartedEvent _)
        {
            if (view.IsOpen) Refresh();
        }

        private void Open()
        {
            Refresh();
            view.Show();
        }

        private void Refresh()
        {
            int today = Today;
            progress.EnsureCycle(today);
            for (int day = 0; day < DailyRewardConfig.DayCount; day++)
                view.SetCellState(day, progress.StateOf(day, today), config.todayAmountColor);
            view.SetClaimButtonsVisible(progress.CanClaim(today));
            RefreshCountdown();
        }

        private void RefreshCountdown() =>
            view.SetCountdown(TimeFormat.DaysClock(TimeService.Instance.TimeUntilDay(progress.CycleEndDay)));

        private void HandleClaim() => Claim(1);

        private void HandleClaimWithAd()
        {
            if (progress.CanClaim(Today)) AdService.ShowRewarded(AdPlacement.DailyX2, claimAfterAd);
        }

        /// Chạm vào đúng ô hôm nay = nhận thường; chạm ô khác không làm gì.
        private void HandleDayClicked(int day)
        {
            if (day == progress.NextIndex) Claim(1);
        }

        private void Claim(int multiplier)
        {
            int today = Today;
            progress.EnsureCycle(today);
            // Chặn nhận 2 lần: bấm đúp, hoặc bấm Claim ngay trước khi quảng cáo x2 kịp che màn.
            if (!progress.CanClaim(today)) return;
            foreach (DailyRewardItem reward in config.days[progress.NextIndex].rewards)
            {
                int amount = reward.amount * multiplier;
                if (reward.kind == DailyRewardKind.Coin) gameData.AddCoins(amount);
                else gameData.AddBooster(reward.booster, amount);
            }
            GameLog.Info($"[BusOut] Quà ngày: nhận ô {progress.NextIndex + 1}, x{multiplier}.");
            progress.MarkClaimed(today);
            gameData.Save();
            AudioService.Play(multiplier > 1 ? SoundId.BonusCoin : SoundId.Coin);
            HapticService.Play(HapticType.Success);
            Refresh();
        }
    }
}
