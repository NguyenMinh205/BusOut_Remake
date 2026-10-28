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
            GameData = new SecureGameData();
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

    [System.Serializable]
    public class SecureGameData
    {
        #region ATTRIBUTES
        /// Level đang chơi, tính từ 1 (= trường "index" của level trong file curve).
        public int currentLevel = 1;
        public int coinCount = 100;
        /// Số lượt mỗi booster, chỉ số = (int)BoosterType. Mỗi booster có sẵn 1 lượt (gốc: tặng 1 lượt khi mở khoá).
        public List<int> boosterCounts = new List<int> { 1, 1, 1, 1 };

        public bool musicOn = true;
        public bool sfxOn = true;
        public bool vibrationOn = true;
        /// Mã ngôn ngữ đang chọn (vd. "en", "vi"). Chưa có hệ thống đa ngôn ngữ: chỉ lưu lựa chọn.
        public string language = "en";
        #endregion

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
        /// out of range. Phần thêm vào lấy số lượt mặc định.
        private void EnsureBoosterSlots()
        {
            int boosterCount = System.Enum.GetValues(typeof(BoosterType)).Length;
            if (boosterCounts == null) boosterCounts = new List<int>();
            while (boosterCounts.Count < boosterCount) boosterCounts.Add(1);
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
            currentLevel = Mathf.Max(1, other.currentLevel);
            coinCount = other.coinCount;
            boosterCounts = other.boosterCounts != null ? new List<int>(other.boosterCounts) : new List<int>();
            EnsureBoosterSlots();
            musicOn = other.musicOn;
            sfxOn = other.sfxOn;
            vibrationOn = other.vibrationOn;
            language = string.IsNullOrEmpty(other.language) ? "en" : other.language;
        }

        public void ClearAllData()
        {
            SaveGameManager.DeleteSave(SaveFileKey);
            // Chép từ bản mới tạo: giá trị mặc định chỉ khai báo một chỗ (khởi tạo field ở trên).
            CopyFrom(new SecureGameData());
        }
        #endregion
    }
}
