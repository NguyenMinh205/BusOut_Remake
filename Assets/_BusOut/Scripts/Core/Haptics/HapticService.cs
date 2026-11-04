using BusOut.DesignPattern;
using UnityEngine;
#if UNITY_ANDROID || UNITY_IOS
using Haptics = CandyCoded.HapticFeedback.HapticFeedback;
#endif

namespace BusOut.Core
{
    /// Kiểu rung theo ý nghĩa (không theo cường độ): đổi cảm giác rung thì sửa ánh xạ trong HapticService, không sửa chỗ gọi.
    public enum HapticType
    {
        /// Va chạm / xác nhận ngắn: xe đâm xe chặn, vừa bật công tắc rung.
        Impact,
        /// Thắng.
        Success,
        /// Thua.
        Failure,
    }

    /// Rung phản hồi theo công tắc Vibration (đọc từ save lúc dựng, nghe SettingsChangedEvent khi đổi). Bọc package
    /// CandyCoded HapticFeedback (xyz.candycoded.hapticfeedback, 3/11/2026 thay package Vibration): rung phản hồi chạm của
    /// HỆ THỐNG — Android View.performHapticFeedback (không cần quyền VIBRATE), iOS UIImpactFeedbackGenerator. Chỉ có 3 mức
    /// Light / Medium / Heavy, không có số nào của mình để chỉnh.
    /// Sống suốt game: GameBootstrap dựng cùng các dịch vụ khác; chạy thẳng GameScene thì lần gọi đầu tự dựng.
    /// Gọi thẳng: HapticService.Play(HapticType.Impact). Editor / PC không rung: chỉ ghi log (GameLog) để soát gọi đúng chỗ.
    public class HapticService : Singleton<HapticService>
    {
        private bool vibrationOn;
        // Plugin rung lỗi (JNI ném lỗi: plugin bị ProGuard xoá, máy lạ…): tắt rung cả phiên, không thử lại mỗi lần.
        private bool isBroken;

        /// Rung một nhịp (đang tắt Vibration thì thôi).
        public static void Play(HapticType type)
        {
            // Lúc tắt game Instance trả null.
            HapticService service = Instance;
            if (service != null) service.PlayHaptic(type);
        }

        protected override void OnSingletonAwake()
        {
            vibrationOn = DataManager.Instance.GameData.vibrationOn;
            EventBus.Subscribe<SettingsChangedEvent>(HandleSettingsChanged);
        }

        protected override void OnDestroy()
        {
            EventBus.Unsubscribe<SettingsChangedEvent>(HandleSettingsChanged);
            base.OnDestroy();
        }

        private void HandleSettingsChanged(SettingsChangedEvent settings) => vibrationOn = settings.VibrationOn;

        private void PlayHaptic(HapticType type)
        {
            if (!vibrationOn || isBroken) return;
            GameLog.Info($"[BusOut] Rung: {type}");
            // ⚠️ Bắt lỗi plugin tại đây: chỗ gọi là giữa luồng game (xe đâm → xe bị đâm lắc; LevelResultPresenter.Tick hiện
            // popup) — lỗi bay ra đó là xe không lắc, hoặc Tick ném lỗi mỗi khung.
            try
            {
                Vibrate(type);
            }
            catch (System.Exception exception)
            {
                isBroken = true;
                Debug.LogError("[BusOut] Plugin rung (CandyCoded HapticFeedback) lỗi: tắt rung tới khi mở lại game. Bản build " +
                               "Android bật Minify thì thêm vào proguard-user.txt: " +
                               "-keep class com.candycoded.hapticfeedbacklibrary.** { *; }. " + $"Lỗi: {exception}", this);
            }
        }

        private static void Vibrate(HapticType type)
        {
            // ⚠️ Package chỉ biên dịch cho Android / iOS / Editor: gọi nó ngoài #if này là bản build PC không biên dịch
            // được. Trong Editor package tự bỏ qua.
#if UNITY_ANDROID || UNITY_IOS
            switch (type)
            {
                case HapticType.Impact: Haptics.MediumFeedback(); break;
                // Không có kiểu "thắng" / "thua" riêng: cả hai rung mạnh, âm thanh phân biệt.
                case HapticType.Success:
                case HapticType.Failure: Haptics.HeavyFeedback(); break;
            }
#endif
        }
    }
}
