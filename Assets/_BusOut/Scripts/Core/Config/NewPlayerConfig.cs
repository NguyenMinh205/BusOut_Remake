using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusOut.Core
{
    /// Giá trị ban đầu của người chơi mới (chưa có save) — chỗ cân bằng kinh tế lúc mới vào game.
    /// ⚠️ Chỉ áp cho save MỚI (lần đầu mở game / sau khi xoá save): đổi số ở đây không đổi save đang có.
    /// DataManager đọc qua Resources (tự dựng, không có ô kéo) → asset phải nằm đúng Resources/NewPlayerConfig.asset.
    [CreateAssetMenu(menuName = "BusOut/New Player Config", fileName = "NewPlayerConfig")]
    public class NewPlayerConfig : ScriptableObject
    {
        public const string ResourcePath = "NewPlayerConfig";

        [Tooltip("Level bắt đầu (≥ 1).")]
        [Min(1)] public int startingLevel = 1;

        [Tooltip("Coin ban đầu.")]
        [Min(0)] public int startingCoins = 100;

        [Tooltip("Số lượt ban đầu của booster không có trong danh sách dưới (kể cả booster thêm sau này, với save cũ).")]
        [Min(0)] public int defaultBoosterCount = 1;

        [Tooltip("Số lượt ban đầu riêng từng booster (ghi đè số mặc định ở trên).")]
        public List<StartingBooster> startingBoosters = new List<StartingBooster>
        {
            new StartingBooster { type = BoosterType.Sort, count = 1 },
            new StartingBooster { type = BoosterType.Undo, count = 1 },
            new StartingBooster { type = BoosterType.Shuffle, count = 1 },
            new StartingBooster { type = BoosterType.Crane, count = 1 },
        };

        [Header("Cài đặt ban đầu")]
        public bool musicOn = true;
        public bool soundOn = true;
        public bool vibrationOn = true;

        [Tooltip("Mã ngôn ngữ ban đầu (phải có trong UIConfig.languages). TẠM luôn \"en\" (bạn chốt 2/11); làm đa ngôn ngữ " +
                 "(Task 10) thì đổi sang theo ngôn ngữ máy.")]
        public string language = "en";

        public int StartingBoosterCount(BoosterType type)
        {
            StartingBooster entry = startingBoosters?.Find(b => b.type == type);
            return entry != null ? entry.count : defaultBoosterCount;
        }

        public static NewPlayerConfig CreateDefault() => CreateInstance<NewPlayerConfig>();
    }

    [Serializable]
    public class StartingBooster
    {
        public BoosterType type;
        [Min(0)] public int count = 1;
    }
}
