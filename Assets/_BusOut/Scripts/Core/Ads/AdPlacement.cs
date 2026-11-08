namespace BusOut.Core
{
    /// Chỗ đặt quảng cáo trong game (GDD §9.1 / docs P4_PLAN — bảng "Bốn loại quảng cáo"). Dùng để ghi log, thống kê,
    /// và để AdService biết đang mở QC cho việc gì. Thêm chỗ mới ở CUỐI (giá trị số có thể đã ghi vào log / thống kê).
    public enum AdPlacement
    {
        // Rewarded
        WinX2,          // popup thắng: nhận x2 coin
        Revive,         // popup Continue: +1 ô đỗ, chơi tiếp
        UnlockSlot,     // chạm ô khoá: mở ô cho level này
        BoosterRefill,  // hết lượt booster: +lượt
        DailyX2,        // quà hằng ngày: nhận x2 (P4.4)
        ShopCoin,       // Shop: +coin, giới hạn mỗi ngày (P4.5)

        // Interstitial / Banner / App Open (P4.3)
        LevelEnd,       // interstitial: đóng popup thắng / thua, trước khi về Menu / chơi lại
        GameBottom,     // banner: đáy màn chơi
        Resume,         // app open: quay lại app sau một lúc ở nền
    }
}
