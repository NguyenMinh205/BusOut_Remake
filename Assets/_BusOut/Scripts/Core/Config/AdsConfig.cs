using UnityEngine;

namespace BusOut.Core
{
    /// Số chỉnh được của quảng cáo: ad unit theo nền tảng, quy tắc giãn cách, tải lại khi hỏng, chữ báo.
    /// AdService đọc qua Resources (tự dựng, không có ô kéo) → asset phải nằm đúng Resources/AdsConfig.asset.
    /// ⚠️ Ad unit mặc định là ID TEST của Google (luôn có quảng cáo thử, không tính tiền). Đổi sang ID thật CHỈ khi lên
    /// store: bấm quảng cáo thật của chính mình khi thử là tài khoản AdMob có thể bị khoá.
    [CreateAssetMenu(menuName = "BusOut/Ads Config", fileName = "AdsConfig")]
    public class AdsConfig : ScriptableObject
    {
        public const string ResourcePath = "AdsConfig";

        [Header("Rewarded — ad unit")]
        public string androidRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";
        public string iosRewardedUnitId = "ca-app-pub-3940256099942544/1712485313";

        [Header("Interstitial — ad unit")]
        public string androidInterstitialUnitId = "ca-app-pub-3940256099942544/1033173712";
        public string iosInterstitialUnitId = "ca-app-pub-3940256099942544/4411468910";

        [Header("Banner — ad unit")]
        public string androidBannerUnitId = "ca-app-pub-3940256099942544/6300978111";
        public string iosBannerUnitId = "ca-app-pub-3940256099942544/2934735716";

        [Header("App Open — ad unit")]
        public string androidAppOpenUnitId = "ca-app-pub-3940256099942544/9257395921";
        public string iosAppOpenUnitId = "ca-app-pub-3940256099942544/5575463023";

        [Header("Interstitial — quy tắc (GDD §9.2)")]
        [Tooltip("Chỉ hiện interstitial từ level này (tính theo level đang lưu: thắng level 4 xong là 5). Người chơi mới chưa " +
                 "quen game thì không ép xem quảng cáo.")]
        [Min(1)] public int interstitialStartLevel = 5;

        [Tooltip("Hai interstitial cách nhau ít nhất bấy nhiêu giây.")]
        [Min(0f)] public float interstitialCooldownSeconds = 90f;

        [Tooltip("Không hiện interstitial trong bấy nhiêu giây sau khi xem xong một rewarded (vừa tự nguyện xem rồi) hoặc một " +
                 "App Open (2 quảng cáo toàn màn sát nhau).")]
        [Min(0f)] public float interstitialAfterRewardedSeconds = 60f;

        [Tooltip("Chỉ hiện interstitial sau mỗi bấy nhiêu lần kết thúc ván (thắng hoặc thua đều tính). 1 = ván nào cũng có thể " +
                 "hiện (vẫn theo giãn cách giây); 2 = cách 1 ván. Game casual trên store thường 2–3.")]
        [Min(1)] public int interstitialLevelEndInterval = 2;

        [Tooltip("Ván kéo dài dưới bấy nhiêu giây (thua nhanh, chơi lại liền) thì không hiện interstitial: tránh vòng " +
                 "\"thua → quảng cáo → thua → quảng cáo\" làm người chơi bỏ game.")]
        [Min(0f)] public float interstitialMinLevelSeconds = 20f;

        [Header("Đầu phiên")]
        [Tooltip("Bấy nhiêu giây đầu sau khi mở app: không interstitial, không App Open. Người chơi quay lại chơi nhanh một ván " +
                 "không bị quảng cáo ngay; ấn tượng đầu phiên tốt hơn.")]
        [Min(0f)] public float sessionGraceSeconds = 120f;

        [Tooltip("Màn \"Ad break\" hiện bấy nhiêu giây trước interstitial: báo trước để người chơi không giật mình.")]
        [Min(0f)] public float adsBreakSeconds = 1.5f;

        [Header("App Open — quy tắc")]
        [Tooltip("Chỉ hiện App Open từ level này.")]
        [Min(1)] public int appOpenStartLevel = 5;

        [Tooltip("Quay lại app sau khi ở nền ít nhất bấy nhiêu giây mới hiện (chuyển app chớp nhoáng thì không).")]
        [Min(0f)] public float appOpenMinBackgroundSeconds = 30f;

        [Tooltip("Không hiện App Open trong bấy nhiêu giây sau khi một quảng cáo toàn màn khác vừa đóng.")]
        [Min(0f)] public float appOpenAfterOtherAdSeconds = 60f;

        [Tooltip("Hai lần App Open cách nhau ít nhất bấy nhiêu giây: chuyển qua lại giữa các app nhiều lần không bị quảng cáo " +
                 "liên tục.")]
        [Min(0f)] public float appOpenCooldownSeconds = 240f;

        [Header("Tải lại khi hỏng")]
        [Tooltip("Tải hỏng lần đầu thì chờ bấy nhiêu giây rồi tải lại; mỗi lần hỏng tiếp chờ gấp đôi (2, 4, 8…).")]
        [Min(0.5f)] public float retryMinSeconds = 2f;

        [Tooltip("Chờ tối đa bấy nhiêu giây giữa 2 lần tải lại (mất mạng lâu thì không tải dồn dập).")]
        [Min(1f)] public float retryMaxSeconds = 60f;

        [Tooltip("Rewarded đóng xong thì đợi thêm bấy nhiêu giây mới coi là \"không có thưởng\". Có nền tảng báo thưởng SAU " +
                 "lúc báo đóng — không chờ là người chơi xem hết mà không nhận được gì.")]
        [Min(0f)] public float rewardGraceSeconds = 0.3f;

        [Header("Chữ báo (toast)")]
        public string notReadyMessage = "Ad not ready. Please try again later.";
        public string noInternetMessage = "No internet connection.";

        [Header("Thử nghiệm (chỉ Editor / Development Build)")]
        [Tooltip("Giả như người chơi ở châu Âu → màn xin đồng ý GDPR (UMP) hiện ra để thử. Đã đồng ý một lần thì không " +
                 "hiện lại (SDK nhớ).")]
        public bool debugConsentAsEea;

        /// Ad unit của loại quảng cáo theo nền tảng đang build (Editor theo nền tảng đang chọn trong Build Settings).
        public string UnitId(AdType type)
        {
            switch (type)
            {
                case AdType.Rewarded: return PerPlatform(androidRewardedUnitId, iosRewardedUnitId);
                case AdType.Interstitial: return PerPlatform(androidInterstitialUnitId, iosInterstitialUnitId);
                case AdType.Banner: return PerPlatform(androidBannerUnitId, iosBannerUnitId);
                case AdType.AppOpen: return PerPlatform(androidAppOpenUnitId, iosAppOpenUnitId);
                default: return "";
            }
        }

        private static string PerPlatform(string android, string ios)
        {
#if UNITY_IOS
            return ios;
#else
            return android;
#endif
        }

        public static AdsConfig CreateDefault() => CreateInstance<AdsConfig>();
    }
}
