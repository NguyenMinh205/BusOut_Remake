using System;
using System.Collections;
using BusOut.DesignPattern;
using UnityEngine;

namespace BusOut.Core
{
    /// Cổng DUY NHẤT code game gọi tới quảng cáo. SDK cụ thể (AdMob…) là một IAdProvider trong assembly riêng, tự đăng ký
    /// lúc khởi động (RegisterProvider). Chính sách nằm hết ở đây (GDD §9.2):
    /// - Rewarded: AdService.ShowRewarded(placement, onRewarded). Chưa sẵn sàng → toast (nút vẫn bấm được).
    /// - Interstitial: AdService.ShowInterstitial(placement, onDone) ở chỗ chuyển cảnh; tự quyết hiện hay bỏ qua theo quy tắc
    ///   (từ level N, giãn cách, không ngay sau rewarded, Remove Ads); có màn "Ad break" trước. onDone luôn được gọi.
    /// - Banner: tự hiện ở cả Menu lẫn màn chơi sau khi Loading xong (LoadingFinishedEvent — banner là UI native, hiện sớm là
    ///   đè lên màn Loading); báo chiều cao qua BannerChangedEvent.
    /// - App Open: tự hiện khi quay lại app sau một lúc ở nền (OnApplicationPause), không hiện khi "quay lại" là do vừa đóng
    ///   quảng cáo của chính mình.
    /// Mọi loại: tải sẵn 1 cái, hỏng thì tải lại giãn dần; một quảng cáo toàn màn tại một lúc; lúc che màn thì tắt tiếng +
    /// chặn chạm. Remove Ads (SecureGameData.noAds, đổi qua SetNoAds) tắt Interstitial / Banner / App Open.
    /// Sống suốt game: GameBootstrap dựng (lần đầu mở app: màn đồng ý GDPR hiện trong lúc Loading). Chạy thẳng GameScene thì
    /// lần gọi đầu mới dựng → lần đó quảng cáo chưa kịp tải.
    public class AdService : Singleton<AdService>
    {
        /// Trạng thái tải của một loại quảng cáo (mỗi loại một cái, dùng chung logic tải lại).
        private sealed class LoadSlot
        {
            public readonly AdType Type;
            public bool IsLoading;
            public int FailCount;
            public Coroutine Retry;
            public Action<bool> OnLoaded;

            public LoadSlot(AdType type) => Type = type;
        }

        private static IAdProvider registeredProvider;

        /// Chiều cao banner đang hiện (pixel màn hình; 0 = không có). Static để view đọc lúc bật mà không dựng AdService.
        public static float BannerHeightPixels { get; private set; }

        private AdsConfig config;
        private SecureGameData gameData;
        private IAdProvider provider;
        private bool isInitialized;
        private LoadSlot rewardedSlot, interstitialSlot, appOpenSlot, bannerSlot;

        // Một quảng cáo toàn màn tại một lúc → giữ thông tin lượt đang hiện ở field (không cấp phát closure mỗi lần).
        private bool isShowing;
        private AdType showingType;
        private AdPlacement showingPlacement;
        private bool rewardEarned;
        private Action pendingAction; // Rewarded: trao thưởng (chỉ khi xem đủ). Interstitial: đi tiếp (luôn gọi).
        private Action onRewardEarned;
        private Action onFullscreenClosed;

        // Giờ thật trong phiên (realtimeSinceStartup) — cho các quy tắc giãn cách (AdsConfig).
        private float sessionStartedAt;
        private float levelStartedAt = float.NegativeInfinity;
        private float lastInterstitialClosedAt = float.NegativeInfinity;
        private float lastRewardedClosedAt = float.NegativeInfinity;
        private float lastAppOpenClosedAt = float.NegativeInfinity;
        private float lastFullscreenClosedAt = float.NegativeInfinity;
        // Số lần kết thúc ván từ interstitial gần nhất (interstitialLevelEndInterval).
        private int levelEndsSinceInterstitial;

        private bool isInBackground;
        private bool pausedByOwnAd;
        private DateTime backgroundStartUtc;
        private bool isLoadingFinished;

        /// Đang có quảng cáo toàn màn (hoặc màn Ads Break) che màn hình.
        public bool IsShowing => isShowing;

        /// Rewarded sẵn sàng hiện ngay (để UI làm nút sáng / xám nếu sau này cần).
        public bool IsRewardedReady => provider != null && isInitialized && provider.IsReady(AdType.Rewarded);

        // Tắt Domain Reload (Enter Play Mode Options): biến static giữ giá trị của lần Play trước → reset.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => BannerHeightPixels = 0f;

        /// Nhà cung cấp quảng cáo gọi lúc khởi động (RuntimeInitializeOnLoadMethod BeforeSceneLoad), trước khi AdService dựng.
        public static void RegisterProvider(IAdProvider adProvider) => registeredProvider = adProvider;

        /// Mở rewarded cho <paramref name="placement"/>. <paramref name="onRewarded"/> chỉ được gọi khi người chơi xem đủ
        /// (lúc quảng cáo ĐÃ đóng). Không xem đủ / chưa sẵn sàng / đang mở QC khác → không gọi gì (chưa sẵn sàng thì có toast).
        /// ⚠️ Nơi gọi không tự trao thưởng trước: chỉ trao trong onRewarded.
        public static void ShowRewarded(AdPlacement placement, Action onRewarded)
        {
            AdService service = Instance;
            if (service != null) service.TryShowRewarded(placement, onRewarded);
        }

        /// Chỗ chuyển cảnh (đóng popup thắng / thua): hiện interstitial nếu quy tắc cho phép, rồi gọi <paramref name="onDone"/>
        /// (không hiện → gọi ngay). ⚠️ Đang mở quảng cáo khác (bấm đúp) → bỏ qua lời gọi này, KHÔNG gọi onDone (lượt đầu sẽ
        /// tự đi tiếp) — tránh chuyển cảnh 2 lần.
        public static void ShowInterstitial(AdPlacement placement, Action onDone)
        {
            AdService service = Instance;
            if (service == null) onDone?.Invoke();
            else service.TryShowInterstitial(placement, onDone);
        }

        /// Bật / tắt Remove Ads: lưu save, ẩn / bỏ banner ngay. (RemoveAdsPresenter gọi khi mua.)
        public static void SetNoAds(bool noAds)
        {
            AdService service = Instance;
            if (service != null) service.ApplyNoAds(noAds);
        }

        protected override void OnSingletonAwake()
        {
            config = Resources.Load<AdsConfig>(AdsConfig.ResourcePath);
            if (config == null)
            {
                Debug.LogError($"[BusOut] Không thấy Resources/{AdsConfig.ResourcePath}.asset: quảng cáo chạy số mặc định trong code " +
                               "(ad unit TEST). Tạo bằng Create → BusOut → Ads Config, đặt trong Assets/_BusOut/Resources.", this);
                config = AdsConfig.CreateDefault();
            }
            // Save đọc trước (GameBootstrap dựng DataManager trước; thiếu thì Instance tự dựng).
            gameData = DataManager.Instance.GameData;
            onRewardEarned = HandleRewardEarned;
            onFullscreenClosed = HandleFullscreenClosed;
            rewardedSlot = CreateSlot(AdType.Rewarded);
            interstitialSlot = CreateSlot(AdType.Interstitial);
            appOpenSlot = CreateSlot(AdType.AppOpen);
            bannerSlot = CreateSlot(AdType.Banner);
            EventBus.Subscribe<LoadingFinishedEvent>(HandleLoadingFinished);
            EventBus.Subscribe<LevelStartedEvent>(HandleLevelStarted);
            // GameBootstrap dựng lúc mở app → đây là lúc bắt đầu phiên (sessionGraceSeconds).
            sessionStartedAt = Time.realtimeSinceStartup;

            provider = registeredProvider;
            if (provider == null)
            {
                Debug.LogError("[BusOut] AdService: không có nhà cung cấp quảng cáo (assembly BusOut.Ads không biên dịch / thiếu " +
                               "Google Mobile Ads) → không có quảng cáo, nút quảng cáo chỉ báo \"chưa sẵn sàng\". Kiểm Console có " +
                               "lỗi biên dịch trong Scripts/Ads không.", this);
                return;
            }
            provider.Initialize(config, HandleInitialized);
        }

        protected override void OnDestroy()
        {
            EventBus.Unsubscribe<LoadingFinishedEvent>(HandleLoadingFinished);
            EventBus.Unsubscribe<LevelStartedEvent>(HandleLevelStarted);
            base.OnDestroy();
        }

        private void HandleLevelStarted(LevelStartedEvent e) => levelStartedAt = Time.realtimeSinceStartup;

        private LoadSlot CreateSlot(AdType type)
        {
            var slot = new LoadSlot(type);
            slot.OnLoaded = loaded => HandleLoaded(slot, loaded);
            return slot;
        }

        private void HandleInitialized(bool ready)
        {
            isInitialized = ready;
            if (!ready)
            {
                Debug.LogWarning("[BusOut] AdService: SDK quảng cáo không khởi tạo được (chưa đồng ý quyền riêng tư / lỗi SDK) → " +
                                 "không có quảng cáo phiên này.", this);
                return;
            }
            GameLog.Info("[BusOut] AdService: SDK sẵn sàng, tải sẵn các loại quảng cáo.");
            Load(rewardedSlot);
            Load(interstitialSlot);
            Load(appOpenSlot);
            // Banner tải sẵn (ẩn) để vào màn chơi là có ngay.
            Load(bannerSlot);
            ApplyBanner();
        }

        #region Rewarded

        private void TryShowRewarded(AdPlacement placement, Action onRewarded)
        {
            // Bấm đúp / bấm nút QC khác trong lúc QC đang mở: bỏ qua, không mở chồng.
            if (isShowing) return;
            if (!IsRewardedReady)
            {
                bool offline = Application.internetReachability == NetworkReachability.NotReachable;
                EventBus.Publish(new ToastRequestedEvent { Message = offline ? config.noInternetMessage : config.notReadyMessage });
                GameLog.Info($"[BusOut] AdService: rewarded {placement} chưa sẵn sàng{(offline ? " (mất mạng)" : "")}.");
                // Người chơi đang muốn xem: tải ngay, khỏi chờ hết lượt chờ tải lại.
                Load(rewardedSlot, true);
                return;
            }
            BeginFullscreen(AdType.Rewarded, placement, onRewarded);
            provider.Show(AdType.Rewarded, onRewardEarned, onFullscreenClosed);
        }

        private void HandleRewardEarned() => rewardEarned = true;

        #endregion

        #region Interstitial

        private void TryShowInterstitial(AdPlacement placement, Action onDone)
        {
            if (isShowing) return;
            // Đếm MỌI lần kết thúc ván (kể cả lần bị bỏ qua vì lý do khác): "mỗi N ván một lần" tính theo ván đã chơi.
            if (placement == AdPlacement.LevelEnd) levelEndsSinceInterstitial++;
            string skipReason = InterstitialSkipReason(placement);
            if (skipReason != null)
            {
                GameLog.Info($"[BusOut] AdService: bỏ qua interstitial {placement} ({skipReason}).");
                onDone?.Invoke();
                return;
            }
            StartCoroutine(ShowInterstitialAfterBreak(placement, onDone));
        }

        /// Lý do KHÔNG hiện (null = được hiện). Thứ tự = từ quy tắc "cứng" tới quy tắc thời gian, để log nói đúng lý do chính.
        private string InterstitialSkipReason(AdPlacement placement)
        {
            if (gameData.noAds) return "Remove Ads";
            if (provider == null || !isInitialized) return "SDK chưa sẵn sàng";
            if (gameData.currentLevel < config.interstitialStartLevel)
                return $"level {gameData.currentLevel} < {config.interstitialStartLevel}";
            float now = Time.realtimeSinceStartup;
            if (now - sessionStartedAt < config.sessionGraceSeconds)
                return $"mới {now - sessionStartedAt:0}s đầu phiên (< {config.sessionGraceSeconds:0}s)";
            if (placement == AdPlacement.LevelEnd)
            {
                if (now - levelStartedAt < config.interstitialMinLevelSeconds)
                    return $"ván ngắn {now - levelStartedAt:0}s (< {config.interstitialMinLevelSeconds:0}s)";
                if (levelEndsSinceInterstitial < config.interstitialLevelEndInterval)
                    return $"mới {levelEndsSinceInterstitial}/{config.interstitialLevelEndInterval} ván";
            }
            if (now - lastInterstitialClosedAt < config.interstitialCooldownSeconds)
                return $"mới {now - lastInterstitialClosedAt:0}s sau interstitial trước";
            float lastOptionalAd = Mathf.Max(lastRewardedClosedAt, lastAppOpenClosedAt);
            if (now - lastOptionalAd < config.interstitialAfterRewardedSeconds)
                return $"mới {now - lastOptionalAd:0}s sau rewarded / App Open";
            if (!provider.IsReady(AdType.Interstitial))
            {
                Load(interstitialSlot, true);
                return "chưa tải xong";
            }
            return null;
        }

        private IEnumerator ShowInterstitialAfterBreak(AdPlacement placement, Action onDone)
        {
            levelEndsSinceInterstitial = 0;
            BeginFullscreen(AdType.Interstitial, placement, onDone);
            // Báo trước 1–2s (gốc: _adsBreak): quảng cáo bật lên đột ngột ngay khi vừa bấm là người chơi giật mình / bấm nhầm.
            EventBus.Publish(new AdsBreakChangedEvent { IsShowing = true });
            yield return new WaitForSecondsRealtime(config.adsBreakSeconds);
            // Vừa hết hạn đúng lúc chờ: coi như đóng luôn (đi tiếp, tải cái mới).
            if (!provider.IsReady(AdType.Interstitial)) HandleFullscreenClosed();
            else provider.Show(AdType.Interstitial, null, onFullscreenClosed);
        }

        #endregion

        #region App Open

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                if (isInBackground) return;
                isInBackground = true;
                // Quảng cáo toàn màn của mình (Android) cũng làm app tạm dừng: lần "quay lại" sau đó không phải người chơi
                // quay lại app → không được hiện App Open (quảng cáo chồng quảng cáo).
                pausedByOwnAd = isShowing;
                backgroundStartUtc = DateTime.UtcNow;
                return;
            }
            if (!isInBackground) return;
            isInBackground = false;
            if (pausedByOwnAd)
            {
                pausedByOwnAd = false;
                return;
            }
            // Thời gian ở nền đo bằng giờ máy (realtimeSinceStartup lúc ở nền có đếm hay không tuỳ nền tảng).
            TryShowAppOpen((DateTime.UtcNow - backgroundStartUtc).TotalSeconds);
        }

        private void TryShowAppOpen(double secondsAway)
        {
            string skipReason = AppOpenSkipReason(secondsAway);
            if (skipReason != null)
            {
                GameLog.Info($"[BusOut] AdService: bỏ qua App Open ({skipReason}).");
                return;
            }
            if (!provider.IsReady(AdType.AppOpen))
            {
                Load(appOpenSlot, true);
                GameLog.Info("[BusOut] AdService: App Open chưa tải xong, bỏ lượt này.");
                return;
            }
            BeginFullscreen(AdType.AppOpen, AdPlacement.Resume, null);
            provider.Show(AdType.AppOpen, null, onFullscreenClosed);
        }

        /// Lý do KHÔNG hiện App Open (null = được hiện).
        private string AppOpenSkipReason(double secondsAway)
        {
            if (isShowing) return "đang mở quảng cáo khác";
            if (gameData.noAds) return "Remove Ads";
            if (provider == null || !isInitialized) return "SDK chưa sẵn sàng";
            if (secondsAway < config.appOpenMinBackgroundSeconds)
                return $"chỉ ở nền {secondsAway:0}s (< {config.appOpenMinBackgroundSeconds:0}s)";
            if (gameData.currentLevel < config.appOpenStartLevel) return $"level {gameData.currentLevel} < {config.appOpenStartLevel}";
            float now = Time.realtimeSinceStartup;
            if (now - sessionStartedAt < config.sessionGraceSeconds)
                return $"mới {now - sessionStartedAt:0}s đầu phiên (< {config.sessionGraceSeconds:0}s)";
            if (now - lastAppOpenClosedAt < config.appOpenCooldownSeconds)
                return $"mới {now - lastAppOpenClosedAt:0}s sau App Open trước (< {config.appOpenCooldownSeconds:0}s)";
            if (now - lastFullscreenClosedAt < config.appOpenAfterOtherAdSeconds)
                return $"mới {now - lastFullscreenClosedAt:0}s sau quảng cáo khác";
            return null;
        }

        #endregion

        #region Chung cho quảng cáo toàn màn

        private void BeginFullscreen(AdType type, AdPlacement placement, Action action)
        {
            showingType = type;
            showingPlacement = placement;
            pendingAction = action;
            rewardEarned = false;
            SetShowing(true);
            GameLog.Info($"[BusOut] AdService: mở {type} {placement}.");
        }

        private void HandleFullscreenClosed() => StartCoroutine(FinishFullscreen());

        private IEnumerator FinishFullscreen()
        {
            AdType type = showingType;
            if (type == AdType.Rewarded)
            {
                // Có nền tảng báo "được thưởng" SAU lúc báo "đã đóng" → chờ một chút (giờ thật: timeScale có thể đang 0).
                float deadline = Time.realtimeSinceStartup + config.rewardGraceSeconds;
                while (!rewardEarned && Time.realtimeSinceStartup < deadline) yield return null;
            }

            bool earned = type == AdType.Rewarded && rewardEarned;
            Action action = pendingAction;
            pendingAction = null;
            float now = Time.realtimeSinceStartup;
            lastFullscreenClosedAt = now;
            if (type == AdType.Rewarded) lastRewardedClosedAt = now;
            if (type == AdType.AppOpen) lastAppOpenClosedAt = now;
            if (type == AdType.Interstitial)
            {
                lastInterstitialClosedAt = now;
                EventBus.Publish(new AdsBreakChangedEvent { IsShowing = false });
            }
            SetShowing(false);
            GameLog.Info($"[BusOut] AdService: {type} {showingPlacement} đóng{(type == AdType.Rewarded ? earned ? ", có thưởng" : ", không thưởng (đóng sớm)" : "")}.");
            EventBus.Publish(new AdCompletedEvent { Type = type, Placement = showingPlacement, IsRewardEarned = earned });
            // Tải cái kế trước khi gọi tiếp: hàm gọi tiếp có lỗi (exception) cũng không làm mất lượt tải.
            Load(SlotOf(type));
            if (type == AdType.Rewarded)
            {
                if (earned) action?.Invoke();
            }
            else action?.Invoke();
        }

        private void SetShowing(bool showing)
        {
            isShowing = showing;
            // iOS: Unity vẫn chạy dưới quảng cáo → nhạc game chồng tiếng quảng cáo nếu không tắt.
            AudioService.SetPausedByAd(showing);
            EventBus.Publish(new AdShowingChangedEvent { IsShowing = showing });
        }

        #endregion

        #region Banner

        private void HandleLoadingFinished(LoadingFinishedEvent e)
        {
            isLoadingFinished = true;
            ApplyBanner();
        }

        /// Hiện banner sau màn Loading (cả Menu lẫn màn chơi) khi không Remove Ads; báo chiều cao cho UI dịch lên.
        /// Chạy thẳng GameScene (không có GameBootstrap) thì không có Loading để chờ.
        private void ApplyBanner()
        {
            bool canShow = provider != null && isInitialized && !gameData.noAds;
            bool visible = canShow && (isLoadingFinished || !GameBootstrap.IsActive);
            if (provider != null) provider.SetBannerVisible(visible);
            if (canShow && !provider.IsReady(AdType.Banner)) Load(bannerSlot);
            float height = visible && provider.IsReady(AdType.Banner) ? provider.BannerHeightPixels : 0f;
            if (Mathf.Approximately(height, BannerHeightPixels)) return;
            BannerHeightPixels = height;
            EventBus.Publish(new BannerChangedEvent { HeightPixels = height });
        }

        private void ApplyNoAds(bool noAds)
        {
            gameData.noAds = noAds;
            gameData.Save();
            if (noAds && provider != null) provider.DestroyBanner();
            GameLog.Info($"[BusOut] AdService: Remove Ads = {noAds}.");
            ApplyBanner();
        }

        #endregion

        #region Tải + tải lại

        private LoadSlot SlotOf(AdType type)
        {
            switch (type)
            {
                case AdType.Rewarded: return rewardedSlot;
                case AdType.Interstitial: return interstitialSlot;
                case AdType.AppOpen: return appOpenSlot;
                default: return bannerSlot;
            }
        }

        /// <paramref name="now"/> = bỏ lượt chờ tải lại đang đếm, tải ngay.
        private void Load(LoadSlot slot, bool now = false)
        {
            if (provider == null || !isInitialized || slot.IsLoading || provider.IsReady(slot.Type)) return;
            // Remove Ads: chỉ rewarded còn dùng → khỏi tải loại khác (tốn mạng, bộ nhớ).
            if (gameData.noAds && slot.Type != AdType.Rewarded) return;
            if (slot.Retry != null)
            {
                if (!now) return; // đang chờ tải lại: để lượt chờ tự chạy
                StopCoroutine(slot.Retry);
                slot.Retry = null;
            }
            slot.IsLoading = true;
            provider.Load(slot.Type, slot.OnLoaded);
        }

        private void HandleLoaded(LoadSlot slot, bool loaded)
        {
            slot.IsLoading = false;
            if (loaded)
            {
                slot.FailCount = 0;
                GameLog.Info($"[BusOut] AdService: {slot.Type} đã tải xong.");
                if (slot.Type == AdType.Banner) ApplyBanner();
                return;
            }
            slot.FailCount++;
            // 2, 4, 8… giây, tối đa retryMaxSeconds: mất mạng lâu không tải dồn dập (tốn pin, SDK có thể chặn).
            float wait = Mathf.Min(config.retryMaxSeconds, config.retryMinSeconds * Mathf.Pow(2f, Mathf.Min(slot.FailCount - 1, 10)));
            GameLog.Info($"[BusOut] AdService: tải {slot.Type} hỏng lần {slot.FailCount}, thử lại sau {wait:0.#}s.");
            slot.Retry = StartCoroutine(RetryLoad(slot, wait));
        }

        private IEnumerator RetryLoad(LoadSlot slot, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            slot.Retry = null;
            Load(slot);
        }

        #endregion

#if UNITY_EDITOR
        /// Tools → BusOut Tools: giả lập vừa quay lại app sau <paramref name="secondsAway"/> giây (Editor không vào nền được).
        /// Vẫn theo đủ quy tắc App Open (level, Remove Ads, giãn cách) → log cho biết vì sao không hiện.
        public void DebugSimulateResume(double secondsAway)
        {
            GameLog.Info($"[BusOut] AdService: giả lập quay lại app sau {secondsAway:0}s (level {gameData.currentLevel}, " +
                         $"noAds {gameData.noAds}, App Open {(provider != null && provider.IsReady(AdType.AppOpen) ? "đã" : "chưa")} tải).");
            TryShowAppOpen(secondsAway);
        }
#endif
    }
}
