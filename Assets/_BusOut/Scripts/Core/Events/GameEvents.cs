using BusOut.DesignPattern;

namespace BusOut.Core
{
    // Sự kiện dùng chung giữa các phần (Core, Home, Gameplay). Sự kiện chỉ một phần dùng thì đặt trong phần đó.
    // Mọi sự kiện là struct + IEvent: Publish không cấp phát rác.

    #region Vòng đời game

    public enum GameState
    {
        None,
        Loading,
        MainMenu,
        Gameplay,
        Paused,
        GameOver,
        Victory,
    }

    /// Trạng thái game đổi (vd. MainMenu → Gameplay).
    public struct GameStateChangedEvent : IEvent
    {
        public GameState PreviousState;
        public GameState NewState;
    }

    /// Bắt đầu một level.
    public struct LevelStartedEvent : IEvent
    {
        public int LevelIndex;
        public string LevelName;
    }

    /// Thắng level.
    public struct LevelCompletedEvent : IEvent
    {
        public int LevelIndex;
        public int Score;
        public int Stars;
        public bool IsNewBestScore;
    }

    /// Thua level.
    public struct LevelFailedEvent : IEvent
    {
        public int LevelIndex;
        public string Reason; // vd. "Out of slots"
    }

    #endregion

    #region Kinh tế

    public enum CurrencyType
    {
        SoftCurrency, // coin kiếm trong game
        HardCurrency, // gem, nạp tiền
        Energy,       // mạng / thể lực
    }

    /// Số dư một loại tiền thay đổi.
    public struct CurrencyUpdatedEvent : IEvent
    {
        public CurrencyType Type;
        public int NewTotalAmount;
        public int DeltaAmount; // âm = trừ, dương = cộng
    }

    /// Booster theo thứ tự trên thanh dưới màn chơi (gốc: Sort, VIP trực thăng, Shuffle, Turbo).
    /// ⚠️ Giá trị = chỉ số trong SecureGameData.boosterCounts (đã lưu): chỉ thêm vào cuối, không đổi thứ tự.
    public enum BoosterType
    {
        Arrange,
        VipSlot,
        Jumble,
        Turbo,
    }

    /// Số lượt một booster thay đổi.
    public struct BoosterCountChangedEvent : IEvent
    {
        public BoosterType Type;
        public int Count;
    }

    /// Người chơi dùng một vật phẩm / booster.
    public struct ItemConsumedEvent : IEvent
    {
        public string ItemId;
        public int RemainingQuantity;
    }

    #endregion

    #region Quảng cáo

    public enum AdType
    {
        Banner,
        Interstitial,
        Rewarded,
    }

    /// Quảng cáo xem xong (dùng để trao thưởng).
    public struct AdCompletedEvent : IEvent
    {
        public AdType Type;
        public bool IsRewardEarned; // true nếu xem hết rewarded
    }

    #endregion

    #region Âm thanh, phản hồi

    /// Yêu cầu phát âm thanh: Gameplay không cần biết tới AudioManager.
    public struct PlayAudioEvent : IEvent
    {
        public string SoundId;
        public float Volume;
    }

    /// Yêu cầu rung camera.
    public struct CameraShakeEvent : IEvent
    {
        public float Duration;
        public float Intensity;
    }

    #endregion
}
