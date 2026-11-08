using System;
using System.Collections.Generic;
using BusOut.Core;
using UnityEngine;

namespace BusOut.Menu
{
    public enum DailyRewardKind
    {
        Coin,
        Booster,
    }

    /// Một món trong quà của một ngày.
    [Serializable]
    public class DailyRewardItem
    {
        public DailyRewardKind kind;
        [Tooltip("Chỉ dùng khi Kind = Booster.")]
        public BoosterType booster;
        [Min(1)] public int amount = 1;
        public Sprite icon;
    }

    /// Quà của một ngày: ngày 1–6 một món, ngày 7 tối đa hai món.
    [Serializable]
    public class DailyRewardDay
    {
        public List<DailyRewardItem> rewards = new List<DailyRewardItem>();
    }

    /// Số chỉnh được của quà hằng ngày. Presenter đọc qua Resources → asset phải nằm đúng Resources/DailyRewardConfig.asset.
    [CreateAssetMenu(menuName = "BusOut/Daily Reward Config", fileName = "DailyRewardConfig")]
    public class DailyRewardConfig : ScriptableObject
    {
        public const string ResourcePath = "DailyRewardConfig";
        public const int DayCount = 7;

        [Tooltip("Đúng 7 dòng, theo thứ tự ngày 1 → 7.")]
        public List<DailyRewardDay> days = new List<DailyRewardDay>
        {
            Day(Coin(50)),
            Day(Booster(BoosterType.Shuffle, 1)),
            Day(Coin(100)),
            Day(Booster(BoosterType.Sort, 1)),
            Day(Coin(150)),
            Day(Booster(BoosterType.Crane, 2)),
            Day(Booster(BoosterType.Shuffle, 3), Booster(BoosterType.Sort, 2)),
        };

        [Tooltip("Nhận bằng quảng cáo: mọi món nhân lên bấy nhiêu lần.")]
        [Min(1)] public int adRewardMultiplier = 2;

        [Tooltip("Từ level này, mỗi lần vào Menu mà hôm nay chưa nhận quà thì popup tự mở. Nút Daily Reward luôn mở được.")]
        [Min(1)] public int autoOpenFromLevel = 2;

        [Tooltip("Màu số lượng quà trên ô nhận được hôm nay (nền ô sáng màu, chữ trắng không đọc được).")]
        public Color todayAmountColor = Color.black;

        [Tooltip("{0} = số ngày.")]
        public string dayLabelFormat = "Day {0}";
        [Tooltip("{0} = số coin.")]
        public string coinAmountFormat = "{0}";
        [Tooltip("{0} = số lượt booster.")]
        public string boosterAmountFormat = "x{0}";

        public static DailyRewardConfig CreateDefault() => CreateInstance<DailyRewardConfig>();

        /// False khi sai số ngày hoặc một ngày không có quà: popup không dùng được.
        public bool IsValid(out string problem)
        {
            problem = "";
            if (days == null || days.Count != DayCount)
            {
                problem = $"Days phải có đúng {DayCount} dòng";
                return false;
            }
            for (int i = 0; i < DayCount; i++)
            {
                if (days[i] == null || days[i].rewards == null || days[i].rewards.Count == 0)
                {
                    problem = $"Day {i + 1} chưa có quà";
                    return false;
                }
            }
            return true;
        }

        private static DailyRewardDay Day(params DailyRewardItem[] items) =>
            new DailyRewardDay { rewards = new List<DailyRewardItem>(items) };

        private static DailyRewardItem Coin(int amount) => new DailyRewardItem { kind = DailyRewardKind.Coin, amount = amount };

        private static DailyRewardItem Booster(BoosterType type, int amount) =>
            new DailyRewardItem { kind = DailyRewardKind.Booster, booster = type, amount = amount };
    }
}
