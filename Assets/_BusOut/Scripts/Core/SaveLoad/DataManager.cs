using System.Collections.Generic;
using BusOut.DesignPattern;
using UnityEngine;

namespace BusOut.Core
{
    /// Giữ dữ liệu người chơi (SecureGameData) suốt phiên chơi; lưu khi app vào nền / tắt.
    /// Không cần đặt sẵn trong scene: gọi DataManager.Instance lần đầu sẽ tự dựng object và nạp save.
    public class DataManager : Singleton<DataManager>
    {
        public SecureGameData GameData { get; private set; }

        protected override void OnSingletonAwake()
        {
            NewPlayerConfig newPlayer = Resources.Load<NewPlayerConfig>(NewPlayerConfig.ResourcePath);
            if (newPlayer == null)
            {
                Debug.LogError($"[BusOut] Không thấy Resources/{NewPlayerConfig.ResourcePath}.asset: người chơi mới nhận số mặc định trong " +
                               "code. Tạo bằng Create → BusOut → New Player Config, đặt trong Assets/_BusOut/Resources.", this);
                newPlayer = NewPlayerConfig.CreateDefault();
            }
            // Save mới = giá trị ban đầu của NewPlayerConfig; có file save thì Load ghi đè.
            GameData = SecureGameData.CreateNew(newPlayer);
            GameData.Load();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // Điện thoại hay giết app khi đang ở nền mà không gọi OnApplicationQuit: phải lưu lúc vào nền.
            if (pauseStatus) GameData?.Save();
        }

        private void OnApplicationQuit()
        {
            GameData?.Save();
        }
    }

    /// Dữ liệu người chơi, lưu thành một file mã hoá (JsonUtility). Giá trị của người chơi MỚI lấy từ NewPlayerConfig
    /// (CreateNew) — khởi tạo field dưới đây chỉ là giá trị khi đọc save cũ THIẾU trường đó.
    /// ⚠️ Thêm trường mới: an toàn (save cũ thiếu → nhận khởi tạo field). Đổi tên / đổi kiểu trường: save cũ mất giá trị đó →
    /// tăng CurrentSaveVersion và chuyển đổi trong CopyFrom.
    [System.Serializable]
    public class SecureGameData
    {
        /// Phiên bản cấu trúc save hiện tại. Lịch sử: 1 (1/11/2026) — thêm saveVersion, dữ liệu giữ nguyên.
        public const int CurrentSaveVersion = 1;

        #region ATTRIBUTES
        /// Phiên bản cấu trúc của file save này. 0 = save có từ trước khi đánh số phiên bản.
        /// ⚠️ Không khởi tạo field: save cũ không có trường này phải đọc ra 0 (khởi tạo = 1 thì không phân biệt được).
        public int saveVersion;
        /// Level đang chơi, tính từ 1 (= trường "index" của level trong file curve).
        public int currentLevel = 1;
        public int coinCount;
        /// Số lượt mỗi booster, chỉ số = (int)BoosterType.
        public List<int> boosterCounts = new List<int>();
        /// Hướng dẫn booster, mỗi bit = (int)BoosterType. Tách 2 cờ: thoát giữa chừng thì chơi lại vẫn hướng dẫn tiếp mà không được tặng thêm.
        public int boosterLessonGiftMask;
        public int boosterLessonDoneMask;
        /// Màn giới thiệu cơ chế đã xem, mỗi bit = (int)LevelMechanic (Gameplay).
        public int mechanicIntroSeenMask;

        public bool musicOn = true;
        public bool sfxOn = true;
        public bool vibrationOn = true;
        /// Mã ngôn ngữ đang chọn (vd. "en", "vi"). Chưa có hệ thống đa ngôn ngữ: chỉ lưu lựa chọn.
        public string language = "";

        /// Giờ thật (UTC, ticks) lần cuối game thấy — TimeService ghi mỗi giây. Mở game mà giờ máy nhỏ hơn số này thì dùng
        /// số này: chặn lùi giờ máy để nhận lại quà ngày. 0 = chưa có (save cũ / người chơi mới).
        public long lastKnownUtcTicks;

        /// Đã mua Remove Ads: tắt interstitial, banner, App Open (rewarded vẫn còn — người chơi tự chọn xem). Đổi qua
        /// AdService.SetNoAds (còn ẩn banner ngay), không gán thẳng.
        public bool noAds;

        /// Quà hằng ngày: số ngày (TimeService.TodayIndex) lần nhận gần nhất; 0 = chưa nhận lần nào. Đây là "khoá đã nhận hôm
        /// nay": bằng hôm nay là đã nhận, sang ngày mới tự hết khoá mà không cần ai đặt lại.
        public int dailyLastClaimDay;
        /// Quà hằng ngày: số ô đã nhận trong chu kỳ này = ô sẽ nhận tiếp theo (0..7; 7 = đã nhận hết).
        public int dailyNextIndex;
        /// Quà hằng ngày: số ngày (TimeService.TodayIndex) chu kỳ 7 ngày hiện tại bắt đầu; 0 = chưa có chu kỳ.
        public int dailyCycleStartDay;
        #endregion

        /// Giá trị ban đầu (người chơi mới, booster thêm sau này). Không lưu: private không có SerializeField.
        [System.NonSerialized] private NewPlayerConfig defaults;

        /// Save mới theo NewPlayerConfig (chưa đọc file).
        public static SecureGameData CreateNew(NewPlayerConfig defaults)
        {
            var data = new SecureGameData
            {
                defaults = defaults,
                saveVersion = CurrentSaveVersion,
                currentLevel = Mathf.Max(1, defaults.startingLevel),
                coinCount = Mathf.Max(0, defaults.startingCoins),
                musicOn = defaults.musicOn,
                sfxOn = defaults.soundOn,
                vibrationOn = defaults.vibrationOn,
                language = defaults.language,
            };
            data.EnsureBoosterSlots();
            return data;
        }

        #region METHODS
        /// Thắng level hiện tại: sang level sau. Số level tăng mãi; hết file level thì Gameplay tự quay vòng layout.
        public void CompleteLevel()
        {
            currentLevel++;
        }

        public void AddCoins(int amount)
        {
            if (amount == 0) return;
            coinCount += amount;
            PublishCoins(amount);
        }

        /// Trừ coin nếu đủ. False (không trừ gì) khi thiếu.
        public bool TrySpendCoins(int amount)
        {
            if (amount < 0 || coinCount < amount) return false;
            coinCount -= amount;
            PublishCoins(-amount);
            return true;
        }

        public int GetBoosterCount(BoosterType type)
        {
            int index = (int)type;
            return index >= 0 && index < boosterCounts.Count ? boosterCounts[index] : 0;
        }

        public void AddBooster(BoosterType type, int amount)
        {
            EnsureBoosterSlots();
            boosterCounts[(int)type] += amount;
            PublishBooster(type);
        }

        /// Dùng một lượt booster. False khi hết lượt.
        public bool TryUseBooster(BoosterType type)
        {
            if (GetBoosterCount(type) <= 0) return false;
            boosterCounts[(int)type]--;
            PublishBooster(type);
            return true;
        }

        public bool IsBoosterLessonDone(BoosterType type) => (boosterLessonDoneMask & (1 << (int)type)) != 0;

        public void MarkBoosterLessonDone(BoosterType type) => boosterLessonDoneMask |= 1 << (int)type;

        public bool IsMechanicIntroSeen(int mechanic) => (mechanicIntroSeenMask & (1 << mechanic)) != 0;

        public void MarkMechanicIntroSeen(int mechanic) => mechanicIntroSeenMask |= 1 << mechanic;

        /// Tặng 1 lượt của bài hướng dẫn, mỗi booster đúng một lần trong cả save.
        public void GrantBoosterLessonGift(BoosterType type)
        {
            int bit = 1 << (int)type;
            if ((boosterLessonGiftMask & bit) != 0) return;
            boosterLessonGiftMask |= bit;
            AddBooster(type, 1);
        }

        private void PublishCoins(int delta)
        {
            EventBus.Publish(new CurrencyUpdatedEvent
            {
                Type = CurrencyType.SoftCurrency, NewTotalAmount = coinCount, DeltaAmount = delta,
            });
        }

        private void PublishBooster(BoosterType type)
        {
            EventBus.Publish(new BoosterCountChangedEvent { Type = type, Count = GetBoosterCount(type) });
        }

        /// ⚠️ Save cũ có thể ít phần tử hơn số booster hiện tại (thêm booster mới sau này): thiếu thì đọc là
        /// out of range. Phần thêm vào lấy số lượt ban đầu của booster đó (NewPlayerConfig).
        private void EnsureBoosterSlots()
        {
            int boosterCount = System.Enum.GetValues(typeof(BoosterType)).Length;
            if (boosterCounts == null) boosterCounts = new List<int>();
            while (boosterCounts.Count < boosterCount)
            {
                var type = (BoosterType)boosterCounts.Count;
                boosterCounts.Add(defaults != null ? defaults.StartingBoosterCount(type) : 1);
            }
        }
        #endregion

        #region SAVE/LOAD
        private const eData SaveFileKey = eData.SecureGameData;

        public void Save()
        {
            SaveGameManager.Save(SaveFileKey, this);
        }

        public void Load()
        {
            SecureGameData loadedData = SaveGameManager.Load(SaveFileKey, this);
            CopyFrom(loadedData);
        }

        private void CopyFrom(SecureGameData other)
        {
            if (other == null) return;
            // Chuyển đổi save cũ (other.saveVersion < CurrentSaveVersion) đặt ở đây khi đổi cấu trúc save.
            // 0 → 1: không cần chuyển gì.
            saveVersion = CurrentSaveVersion;
            currentLevel = Mathf.Max(1, other.currentLevel);
            coinCount = other.coinCount;
            boosterCounts = other.boosterCounts != null ? new List<int>(other.boosterCounts) : new List<int>();
            EnsureBoosterSlots();
            boosterLessonGiftMask = other.boosterLessonGiftMask;
            boosterLessonDoneMask = other.boosterLessonDoneMask;
            mechanicIntroSeenMask = other.mechanicIntroSeenMask;
            musicOn = other.musicOn;
            sfxOn = other.sfxOn;
            vibrationOn = other.vibrationOn;
            language = !string.IsNullOrEmpty(other.language) ? other.language
                : defaults != null ? defaults.language : "en";
            lastKnownUtcTicks = other.lastKnownUtcTicks;
            noAds = other.noAds;
            dailyLastClaimDay = other.dailyLastClaimDay;
            dailyNextIndex = other.dailyNextIndex;
            dailyCycleStartDay = other.dailyCycleStartDay;
        }

        public void ClearAllData()
        {
            SaveGameManager.DeleteSave(SaveFileKey);
            // Chép từ bản mới tạo: giá trị ban đầu chỉ khai báo một chỗ (NewPlayerConfig).
            CopyFrom(defaults != null ? CreateNew(defaults) : new SecureGameData());
        }
        #endregion
    }
}
