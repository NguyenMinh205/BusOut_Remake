using System.Collections.Generic;
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

    /// Menu bấm Play: chuyển sang màn chơi, chơi level đang lưu (SecureGameData.currentLevel). GameFlowController nghe.
    public struct PlayLevelRequestedEvent : IEvent
    {
    }

    /// Màn chơi xin về Menu (nhận thưởng thắng, đóng popup thua, rời ván). GameFlowController nghe.
    public struct GoHomeRequestedEvent : IEvent
    {
    }

    /// GameContext vừa bật XONG (GameFlowController phát sau SetActive). Bắt đầu ván ở đây, KHÔNG ở OnEnable:
    /// ⚠️ trong OnEnable cây cha còn đang bật dở, Unity cấm SetParent (pool xe / khách) — lệnh bị bỏ qua, pool lệch.
    public struct GameplayShownEvent : IEvent
    {
    }

    /// HomeContext vừa bật XONG (về từ màn chơi). Dựng lại Menu ở đây, không ở OnEnable (cùng lý do GameplayShownEvent).
    public struct HomeShownEvent : IEvent
    {
    }

    /// Màn chuyển cảnh bắt đầu mờ đi, lộ context vừa bật (sau GameplayShownEvent / HomeShownEvent, khi đã giữ đủ lâu).
    /// Quy ước: *ShownEvent = dựng dưới màn che; ScreenRevealedEvent = bắt đầu hoạt ảnh người chơi phải thấy (xe "bung" vào
    /// ván, xe chạy sang bệ mới). Không có màn che thì phát ngay sau *ShownEvent.
    public struct ScreenRevealedEvent : IEvent
    {
    }

    /// Các trang của màn Loading chuyển cảnh (hiện là các booster). Gameplay phát lúc khởi tạo (Core không đọc được
    /// GameplayConfig); GameFlowController nhận, đưa cho màn che. ⚠️ EventBus không giữ sự kiện cũ: người nhận phải đăng ký
    /// TRƯỚC lúc phát — GameFlowController đăng ký từ lúc nạp scene, Gameplay phát muộn hơn (màn Loading / lần Play đầu).
    public struct LoadingTipsChangedEvent : IEvent
    {
        public IReadOnlyList<LoadingTip> Tips;
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

    /// Các booster. Sort và Shuffle như gốc (vai trò tách rõ 27/10/2026); Undo và Crane là booster riêng của remake
    /// (thay VIP trực thăng và Turbo — cùng chỉ số 1 và 3). Thứ tự trên thanh / mở khoá do GameplayConfig.boosters quyết.
    /// ⚠️ Giá trị = chỉ số trong SecureGameData.boosterCounts (đã lưu): chỉ thêm vào cuối, không đổi thứ tự.
    public enum BoosterType
    {
        Sort,
        Undo,
        Shuffle,
        Crane,
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

    /// Người chơi đổi cài đặt âm thanh / rung (đã lưu). Hệ thống âm thanh / rung nghe sự kiện này.
    public struct SettingsChangedEvent : IEvent
    {
        public bool MusicOn;
        public bool SoundOn;
        public bool VibrationOn;
    }

    /// Người chơi đổi ngôn ngữ (đã lưu). Hệ thống đa ngôn ngữ (chưa có) nghe sự kiện này.
    public struct LanguageChangedEvent : IEvent
    {
        public string Code;
    }

    /// Yêu cầu rung camera.
    public struct CameraShakeEvent : IEvent
    {
        public float Duration;
        public float Intensity;
    }

    #endregion
}
