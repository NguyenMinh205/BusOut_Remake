using System;
using System.Threading;
using BusOut.Core;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace BusOut.Ads
{
    /// Nhà cung cấp quảng cáo AdMob (Google Mobile Ads Unity 11.6) cho AdService: Rewarded, Interstitial, App Open, Banner.
    /// Assembly riêng (BusOut.Ads, chỉ tham chiếu Core): đổi sang LevelPlay / MAX = thay thư mục này, không sửa code game.
    /// Không ai tham chiếu assembly này: lớp tự đăng ký vào AdService lúc khởi động (BeforeSceneLoad).
    /// Khởi động: UMP (xin đồng ý GDPR — chỉ hiện form khi luật yêu cầu, vd. châu Âu) → MobileAds.Initialize.
    /// Trong Editor plugin tự hiện quảng cáo GIẢ (PlaceholderAds) với ad unit test → thử được không cần máy.
    internal sealed class AdMobProvider : IAdProvider
    {
        // Hạn dùng của quảng cáo đã tải (SDK): Rewarded / Interstitial 1 giờ, App Open 4 giờ → coi như hết sớm hơn chút.
        private const float FullscreenLifetimeSeconds = 55f * 60f;
        private const float AppOpenLifetimeSeconds = 3.9f * 3600f;

        private AdsConfig config;
        private SynchronizationContext mainThread;
        private bool isInitStarted;
        private Action<bool> initDone;

        private RewardedAd rewarded;
        private float rewardedLoadedAt;
        private InterstitialAd interstitial;
        private float interstitialLoadedAt;
        private AppOpenAd appOpen;
        private float appOpenLoadedAt;

        private BannerView banner;
        private bool isBannerLoaded;
        private bool isBannerWanted;
        private Action<bool> pendingBannerDone; // báo kết quả tải banner cho AdService, đúng một lần mỗi lượt tải

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => AdService.RegisterProvider(new AdMobProvider());

        #region Khởi động + đồng ý GDPR

        public void Initialize(AdsConfig adsConfig, Action<bool> onDone)
        {
            if (isInitStarted) return;
            isInitStarted = true;
            config = adsConfig;
            initDone = onDone;
            // Gọi từ main thread (AdService.Awake): giữ lại để đưa MỌI callback của SDK về main thread (mặc định SDK gọi từ
            // thread khác → đụng API Unity là lỗi). Không dùng MobileAds.RaiseAdEventsOnUnityMainThread: 11.6 đã bỏ.
            mainThread = SynchronizationContext.Current;

            var request = new ConsentRequestParameters();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (config.debugConsentAsEea)
                request.ConsentDebugSettings = new ConsentDebugSettings { DebugGeography = DebugGeography.EEA };
#endif
            ConsentInformation.Update(request, error => Post(() => HandleConsentInfoUpdated(error)));
        }

        private void HandleConsentInfoUpdated(FormError error)
        {
            if (error != null)
            {
                // Mất mạng lúc mở app: không lấy được trạng thái mới. Vẫn có thể xin QC nếu lần trước đã đồng ý.
                GameLog.Info($"[BusOut] AdMob: UMP không cập nhật được ({error.Message}).");
                InitializeSdkIfAllowed();
                return;
            }
            // Chỉ hiện form khi cần (người chơi ở vùng có luật, chưa trả lời). Không cần thì gọi lại ngay.
            ConsentForm.LoadAndShowConsentFormIfRequired(formError => Post(() =>
            {
                if (formError != null) GameLog.Info($"[BusOut] AdMob: form đồng ý lỗi ({formError.Message}).");
                InitializeSdkIfAllowed();
            }));
        }

        private void InitializeSdkIfAllowed()
        {
            bool allowed = ConsentInformation.CanRequestAds();
#if UNITY_EDITOR
            // Editor: quảng cáo giả, không có người dùng thật → không để UMP của Editor chặn việc thử.
            if (!allowed) GameLog.Info("[BusOut] AdMob: UMP chưa cho phép xin QC — Editor vẫn khởi tạo để thử.");
            allowed = true;
#endif
            if (!allowed)
            {
                FinishInit(false);
                return;
            }
            MobileAds.Initialize(status => Post(() => FinishInit(true)));
        }

        private void FinishInit(bool ready)
        {
            Action<bool> callback = initDone;
            initDone = null;
            callback?.Invoke(ready);
        }

        #endregion

        #region Tải

        public bool IsReady(AdType type)
        {
            switch (type)
            {
                case AdType.Rewarded: return rewarded != null && rewarded.CanShowAd() && IsFresh(rewardedLoadedAt, FullscreenLifetimeSeconds);
                case AdType.Interstitial:
                    return interstitial != null && interstitial.CanShowAd() && IsFresh(interstitialLoadedAt, FullscreenLifetimeSeconds);
                case AdType.AppOpen: return appOpen != null && appOpen.CanShowAd() && IsFresh(appOpenLoadedAt, AppOpenLifetimeSeconds);
                case AdType.Banner: return banner != null && isBannerLoaded;
                default: return false;
            }
        }

        private static bool IsFresh(float loadedAt, float lifetime) => Time.realtimeSinceStartup - loadedAt < lifetime;

        public void Load(AdType type, Action<bool> onDone)
        {
            if (IsReady(type))
            {
                onDone(true);
                return;
            }
            string unitId = config.UnitId(type);
            // Cái cũ (hết hạn / đã dùng) huỷ trước để SDK giải phóng bộ nhớ native.
            switch (type)
            {
                case AdType.Rewarded:
                    rewarded?.Destroy();
                    rewarded = null;
                    RewardedAd.Load(unitId, new AdRequest(), (ad, error) => Post(() =>
                    {
                        if (!LoadSucceeded(type, ad, error, onDone)) return;
                        rewarded = ad;
                        rewardedLoadedAt = Time.realtimeSinceStartup;
                        onDone(true);
                    }));
                    break;
                case AdType.Interstitial:
                    interstitial?.Destroy();
                    interstitial = null;
                    InterstitialAd.Load(unitId, new AdRequest(), (ad, error) => Post(() =>
                    {
                        if (!LoadSucceeded(type, ad, error, onDone)) return;
                        interstitial = ad;
                        interstitialLoadedAt = Time.realtimeSinceStartup;
                        onDone(true);
                    }));
                    break;
                case AdType.AppOpen:
                    appOpen?.Destroy();
                    appOpen = null;
                    AppOpenAd.Load(unitId, new AdRequest(), (ad, error) => Post(() =>
                    {
                        if (!LoadSucceeded(type, ad, error, onDone)) return;
                        appOpen = ad;
                        appOpenLoadedAt = Time.realtimeSinceStartup;
                        onDone(true);
                    }));
                    break;
                case AdType.Banner:
                    LoadBanner(unitId, onDone);
                    break;
                default:
                    onDone(false);
                    break;
            }
        }

        private static bool LoadSucceeded(AdType type, object ad, LoadAdError error, Action<bool> onDone)
        {
            if (error == null && ad != null) return true;
            GameLog.Info($"[BusOut] AdMob: tải {type} hỏng: {error?.GetMessage()}");
            onDone(false);
            return false;
        }

        #endregion

        #region Hiện quảng cáo toàn màn

        public void Show(AdType type, Action onEarned, Action onClosed)
        {
            if (!IsReady(type))
            {
                onClosed();
                return;
            }
            // Một quảng cáo chỉ hiện được một lần: nhả ra ngay, cái kế AdService tự tải.
            switch (type)
            {
                case AdType.Rewarded:
                    RewardedAd rewardedAd = rewarded;
                    rewarded = null;
                    Action closeRewarded = CloseOnce(rewardedAd.Destroy, onClosed);
                    rewardedAd.OnAdFullScreenContentClosed += () => Post(closeRewarded);
                    rewardedAd.OnAdFullScreenContentFailed += error => Post(() => ShowFailed(type, error, closeRewarded));
                    rewardedAd.Show(reward => Post(onEarned));
                    break;
                case AdType.Interstitial:
                    InterstitialAd interstitialAd = interstitial;
                    interstitial = null;
                    Action closeInterstitial = CloseOnce(interstitialAd.Destroy, onClosed);
                    interstitialAd.OnAdFullScreenContentClosed += () => Post(closeInterstitial);
                    interstitialAd.OnAdFullScreenContentFailed += error => Post(() => ShowFailed(type, error, closeInterstitial));
                    interstitialAd.Show();
                    break;
                case AdType.AppOpen:
                    AppOpenAd appOpenAd = appOpen;
                    appOpen = null;
                    Action closeAppOpen = CloseOnce(appOpenAd.Destroy, onClosed);
                    appOpenAd.OnAdFullScreenContentClosed += () => Post(closeAppOpen);
                    appOpenAd.OnAdFullScreenContentFailed += error => Post(() => ShowFailed(type, error, closeAppOpen));
                    appOpenAd.Show();
                    break;
                default:
                    onClosed();
                    break;
            }
        }

        /// Có SDK báo cả Failed lẫn Closed: chỉ báo AdService một lần, huỷ quảng cáo đã dùng.
        private static Action CloseOnce(Action destroy, Action onClosed)
        {
            bool isClosed = false;
            return () =>
            {
                if (isClosed) return;
                isClosed = true;
                destroy();
                onClosed();
            };
        }

        private static void ShowFailed(AdType type, AdError error, Action close)
        {
            Debug.LogWarning($"[BusOut] AdMob: {type} không hiện được: {error?.GetMessage()}");
            close();
        }

        #endregion

        #region Banner

        public float BannerHeightPixels => banner != null && isBannerLoaded ? banner.GetHeightInPixels() : 0f;

        private void LoadBanner(string unitId, Action<bool> onDone)
        {
            DestroyBanner();
            // Adaptive: cao theo bề ngang máy (khoảng 50–90dp), neo đáy vùng an toàn.
            AdSize size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
            BannerView view = new BannerView(unitId, size, AdPosition.Bottom);
            banner = view;
            pendingBannerDone = onDone;
            // SDK tự làm mới banner định kỳ → Loaded / LoadFailed còn được gọi nhiều lần sau: chỉ báo AdService lần đầu
            // (pendingBannerDone dùng một lần).
            view.OnBannerAdLoaded += () => Post(() =>
            {
                if (banner != view) return;
                isBannerLoaded = true;
                ApplyBannerVisibility();
                ReportBannerLoad(true);
            });
            view.OnBannerAdLoadFailed += error => Post(() =>
            {
                // Làm mới hỏng khi đã có banner: SDK giữ banner cũ, không cần làm gì.
                if (banner != view || isBannerLoaded) return;
                GameLog.Info($"[BusOut] AdMob: tải Banner hỏng: {error?.GetMessage()}");
                DestroyBanner(); // báo hỏng cho AdService trong này
            });
            // Banner hiện ngay khi tạo: ẩn liền nếu chưa muốn hiện (vd. đang ở Menu).
            ApplyBannerVisibility();
            view.LoadAd(new AdRequest());
        }

        public void SetBannerVisible(bool visible)
        {
            isBannerWanted = visible;
            ApplyBannerVisibility();
        }

        private void ApplyBannerVisibility()
        {
            if (banner == null) return;
            if (isBannerWanted) banner.Show();
            else banner.Hide();
        }

        /// ⚠️ Huỷ lúc banner còn đang tải (vd. bật Remove Ads) phải báo "hỏng" cho AdService: banner đã huỷ không gọi
        /// callback nữa → không báo là AdService kẹt cờ "đang tải", tắt Remove Ads sau đó banner không bao giờ tải lại.
        public void DestroyBanner()
        {
            banner?.Destroy();
            banner = null;
            isBannerLoaded = false;
            ReportBannerLoad(false);
        }

        private void ReportBannerLoad(bool loaded)
        {
            Action<bool> done = pendingBannerDone;
            pendingBannerDone = null;
            done?.Invoke(loaded);
        }

        #endregion

        /// Đưa về main thread của Unity (callback UMP / quảng cáo có thể tới từ thread khác tuỳ nền tảng).
        private void Post(Action action)
        {
            if (action == null) return;
            if (mainThread == null) action();
            else mainThread.Post(_ => action(), null);
        }
    }
}
