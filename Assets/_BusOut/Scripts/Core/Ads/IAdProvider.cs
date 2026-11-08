using System;

namespace BusOut.Core
{
    /// Một SDK quảng cáo (AdMob…). AdService là chỗ DUY NHẤT gọi vào đây; code game chỉ gọi AdService.
    /// Nhà cung cấp nằm trong assembly riêng (vd. BusOut.Ads cho AdMob) và tự đăng ký bằng AdService.RegisterProvider lúc
    /// khởi động → đổi SDK = thay assembly đó, không sửa Core hay code game.
    /// Quy ước: mọi callback gọi trên main thread của Unity (nhà cung cấp tự lo việc chuyển thread).
    /// Chính sách (tải lại khi hỏng, khi nào được hiện, giãn cách) nằm ở AdService, không ở đây.
    public interface IAdProvider
    {
        /// Xin đồng ý quyền riêng tư (nếu SDK cần) + khởi tạo SDK. onDone(true) = sẵn sàng tải quảng cáo.
        void Initialize(AdsConfig config, Action<bool> onDone);

        /// Đã tải xong (và chưa hết hạn), hiện được ngay. Banner: đã có banner tải xong.
        bool IsReady(AdType type);

        /// Tải một quảng cáo loại <paramref name="type"/>. onDone(true) = tải được. AdService không gọi chồng khi đang tải.
        /// Banner: tạo banner đáy màn (ẩn / hiện theo SetBannerVisible) và tải.
        void Load(AdType type, Action<bool> onDone);

        /// Hiện quảng cáo toàn màn (Rewarded / Interstitial / AppOpen) đã tải.
        /// onEarned: chỉ Rewarded — người chơi xem đủ (có thể gọi TRƯỚC hoặc SAU onClosed tuỳ nền tảng).
        /// onClosed: đã đóng — hoặc không hiện được (lỗi) — luôn được gọi đúng một lần.
        void Show(AdType type, Action onEarned, Action onClosed);

        /// Hiện / ẩn banner (nhớ lựa chọn; banner tải xong sau đó cũng theo lựa chọn này).
        void SetBannerVisible(bool visible);

        /// Chiều cao banner (pixel màn hình) khi đã tải; 0 khi chưa có.
        float BannerHeightPixels { get; }

        /// Bỏ hẳn banner (Remove Ads).
        void DestroyBanner();
    }
}
