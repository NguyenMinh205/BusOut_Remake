using System;
using System.Collections;
using System.Globalization;
using BusOut.DesignPattern;
using UnityEngine;
using UnityEngine.Networking;

namespace BusOut.Core
{
    /// Giờ thật của game (UTC) cho những thứ tính theo ngày / giờ: quà hằng ngày, giới hạn quảng cáo mỗi ngày, cooldown.
    /// Gọi: TimeService.UtcNow, TimeService.Instance.TodayIndex / TimeUntilNextDay; sang ngày mới → NewDayStartedEvent.
    ///
    /// Giờ lấy từ đâu (giờ lai — bạn chốt 8/11/2026):
    /// - Trong một phiên: mốc neo + thời gian chạy đơn điệu (realtimeSinceStartup) → chỉnh giờ máy giữa chừng không ảnh hưởng.
    /// - Mở game: mốc neo = giờ máy UTC, nhưng KHÔNG nhỏ hơn mốc lần cuối đã lưu (chặn lùi giờ máy để nhận quà lại).
    /// - Có mạng: hỏi giờ máy chủ (header "Date") lúc mở game và khi quay lại sau ≥ resyncAfterBackgroundSeconds ở nền →
    ///   neo lại theo giờ máy chủ (chặn luôn tua giờ máy tới trước). Mất mạng thì đi tiếp bằng giờ máy, không thử lại mãi.
    /// - Quay lại từ nền: cộng thêm thời gian đã ở nền đo bằng giờ máy, tối thiểu 0 (lùi giờ máy lúc ở nền không lùi được).
    /// Còn hở (chấp nhận): mất mạng + tua giờ máy tới trước; đổi múi giờ để sang ngày sớm vài giờ.
    ///
    /// Sống suốt game: GameBootstrap dựng sau save (đọc mốc lần cuối) và cho màn Loading chờ giờ máy chủ tối đa
    /// bootMaxWaitSeconds. Chạy thẳng GameScene trong Editor thì lần gọi đầu tự dựng.
    public class TimeService : Singleton<TimeService>
    {
        // Số của vòng kiểm, không phải số chơi game: sang ngày trễ tối đa 1 giây là đủ, và ghi mốc vào save mỗi giây.
        private const float TickInterval = 1f;

        private TimeConfig config;
        private SecureGameData gameData;

        // Giờ thật (CHƯA cộng độ lệch thử trong Editor) tại thời điểm anchorRealtime.
        private DateTime anchorUtc;
        private double anchorRealtime;

        private bool isSyncing;
        private bool isInBackground;
        private DateTime backgroundStartDeviceUtc;
        private DateTime backgroundStartRealUtc;
        private int lastDayIndex;
        private float tickTimer;

        /// Đang neo theo giờ máy chủ (false = giờ máy, chưa lấy được / mất mạng).
        public bool IsServerTime { get; private set; }

        /// Lần hỏi giờ máy chủ đầu tiên đã xong (được hoặc hỏng). Màn Loading chờ cờ này.
        public bool IsFirstSyncDone { get; private set; }

        public float BootMaxWaitSeconds => config.bootMaxWaitSeconds;

        /// Giờ thật hiện tại (UTC). Lúc tắt game (không còn dịch vụ) trả giờ máy.
        public static DateTime UtcNow
        {
            get
            {
                TimeService service = Instance;
                return service != null ? service.NowUtc : DateTime.UtcNow;
            }
        }

        public DateTime NowUtc => RealUtcNow() + CurrentDebugOffset;

        /// Giờ địa phương của máy, tính từ giờ thật (giờ trên tường của người chơi, không phải đồng hồ máy).
        public DateTime LocalNow => ToLocal(NowUtc);

        /// Số thứ tự của ngày hôm nay theo lịch ĐỊA PHƯƠNG: sang ngày lúc 0h máy người chơi (không phải 0h UTC = 7h sáng VN).
        /// So sánh / lưu số này để biết "đã nhận hôm nay chưa".
        public int TodayIndex => DayIndexOf(NowUtc);

        /// Còn bao lâu tới 0h ngày mai (giờ địa phương) — cho đồng hồ đếm ngược.
        public TimeSpan TimeUntilNextDay
        {
            get
            {
                DateTime local = LocalNow;
                return local.Date.AddDays(1) - local;
            }
        }

        public static int DayIndexOf(DateTime utc) => (int)(ToLocal(utc).Ticks / TimeSpan.TicksPerDay);

        private static DateTime ToLocal(DateTime utc) =>
            new DateTime(utc.Ticks, DateTimeKind.Unspecified) + TimeZoneInfo.Local.GetUtcOffset(utc);

        protected override void OnSingletonAwake()
        {
            config = Resources.Load<TimeConfig>(TimeConfig.ResourcePath);
            if (config == null)
            {
                Debug.LogError($"[BusOut] Không thấy Resources/{TimeConfig.ResourcePath}.asset: TimeService chạy số mặc định trong " +
                               "code. Tạo bằng Create → BusOut → Time Config, đặt trong Assets/_BusOut/Resources.", this);
                config = TimeConfig.CreateDefault();
            }
            // Save đọc trước (GameBootstrap dựng DataManager trước; thiếu thì Instance tự dựng).
            gameData = DataManager.Instance.GameData;

            DateTime deviceUtc = DateTime.UtcNow;
            DateTime start = deviceUtc;
            if (TryGetLastKnownUtc(out DateTime lastKnown) && lastKnown > deviceUtc)
            {
                GameLog.Info($"[BusOut] TimeService: giờ máy ({deviceUtc:u}) nhỏ hơn lần chơi trước ({lastKnown:u}) → dùng mốc cũ.");
                start = lastKnown;
            }
            SetAnchor(start);
            lastDayIndex = TodayIndex;
            StartServerSync();
        }

        private void Update()
        {
            tickTimer += Time.unscaledDeltaTime;
            if (tickTimer < TickInterval) return;
            tickTimer = 0f;
            // Ghi mốc vào save MỖI GIÂY (chỉ gán số, không ghi file): DataManager lưu file lúc app vào nền, mà thứ tự
            // OnApplicationPause giữa các object không cố định → không trông vào việc mình ghi trước lúc đó.
            RememberNow();
            CheckNewDay();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                if (isInBackground) return;
                isInBackground = true;
                backgroundStartDeviceUtc = DateTime.UtcNow;
                backgroundStartRealUtc = RealUtcNow();
                RememberNow();
                return;
            }

            if (!isInBackground) return;
            isInBackground = false;
            // Không trông vào realtimeSinceStartup lúc ở nền (tuỳ nền tảng có đếm hay không): đo bằng giờ máy rồi neo lại.
            TimeSpan away = DateTime.UtcNow - backgroundStartDeviceUtc;
            if (away < TimeSpan.Zero)
            {
                GameLog.Info($"[BusOut] TimeService: giờ máy bị lùi {-away.TotalSeconds:0}s lúc app ở nền → coi như 0s.");
                away = TimeSpan.Zero;
            }
            SetAnchor(backgroundStartRealUtc + away);
            CheckNewDay();
            // ⚠️ Quảng cáo toàn màn cũng làm app tạm dừng: không có ngưỡng thì mỗi lần đóng quảng cáo lại gửi request.
            if (away.TotalSeconds >= config.resyncAfterBackgroundSeconds) StartServerSync();
        }

        private DateTime RealUtcNow() => anchorUtc.AddSeconds(Time.realtimeSinceStartupAsDouble - anchorRealtime);

        private void SetAnchor(DateTime realUtc)
        {
            anchorUtc = realUtc;
            anchorRealtime = Time.realtimeSinceStartupAsDouble;
            RememberNow();
        }

        // Lưu giờ THẬT (không cộng độ lệch thử): bỏ lệch thử xong không bị chặn-lùi giữ lại ở giờ tương lai.
        private void RememberNow() => gameData.lastKnownUtcTicks = RealUtcNow().Ticks;

        private bool TryGetLastKnownUtc(out DateTime utc)
        {
            long ticks = gameData.lastKnownUtcTicks;
            // Save cũ thiếu trường = 0; số hỏng ngoài khoảng DateTime thì new DateTime văng lỗi.
            bool valid = ticks > 0 && ticks <= DateTime.MaxValue.Ticks;
            utc = valid ? new DateTime(ticks, DateTimeKind.Utc) : default;
            return valid;
        }

        private void CheckNewDay()
        {
            int today = TodayIndex;
            if (today > lastDayIndex)
            {
                lastDayIndex = today;
                GameLog.Info($"[BusOut] TimeService: sang ngày mới {LocalNow:yyyy-MM-dd}.");
                EventBus.Publish(new NewDayStartedEvent { DayIndex = today });
            }
            // Giờ máy chủ kéo lùi về hôm qua (giờ máy đang chạy nhanh): không báo gì, sang ngày thật thì báo lại.
            else if (today < lastDayIndex) lastDayIndex = today;
        }

        private void StartServerSync()
        {
            if (isSyncing) return;
            StartCoroutine(SyncWithServer());
        }

        private IEnumerator SyncWithServer()
        {
            isSyncing = true;
            bool synced = false;
            // Biết chắc không có mạng thì thôi luôn, khỏi chờ timeout từng trang.
            if (Application.internetReachability != NetworkReachability.NotReachable)
            {
                foreach (string url in config.serverUrls)
                {
                    if (string.IsNullOrWhiteSpace(url)) continue;
                    // HEAD: chỉ cần header, không tải trang.
                    using (UnityWebRequest request = UnityWebRequest.Head(url))
                    {
                        request.timeout = config.requestTimeoutSeconds;
                        double sentAt = Time.realtimeSinceStartupAsDouble;
                        yield return request.SendWebRequest();
                        // Lỗi HTTP (405, 403…) vẫn có header Date hợp lệ → chỉ bỏ khi không kết nối được.
                        if (request.result != UnityWebRequest.Result.ConnectionError
                            && TryParseHttpDate(request.GetResponseHeader("Date"), out DateTime serverUtc))
                        {
                            // Header là lúc máy chủ trả lời ≈ giữa đường đi-về.
                            double halfRoundTrip = (Time.realtimeSinceStartupAsDouble - sentAt) * 0.5;
                            ApplyServerTime(serverUtc.AddSeconds(halfRoundTrip), url);
                            synced = true;
                            break;
                        }
                    }
                }
            }
            if (!synced) GameLog.Info("[BusOut] TimeService: không lấy được giờ máy chủ (mất mạng?) → dùng giờ máy.");
            isSyncing = false;
            IsFirstSyncDone = true;
        }

        private void ApplyServerTime(DateTime serverUtc, string url)
        {
            double deviceDrift = (DateTime.UtcNow - serverUtc).TotalSeconds;
            GameLog.Info($"[BusOut] TimeService: giờ máy chủ {serverUtc:u} ({url}), giờ máy lệch {deviceDrift:+0;-0;0}s.");
            // Giờ máy chủ là chuẩn, kể cả khi nhỏ hơn mốc đã lưu (lần trước tua giờ máy tới trước lúc mất mạng).
            SetAnchor(serverUtc);
            IsServerTime = true;
            CheckNewDay();
        }

        private static bool TryParseHttpDate(string value, out DateTime utc)
        {
            // Header Date theo RFC 1123: "Sun, 08 Nov 2026 08:49:37 GMT" = định dạng "r". Invariant: máy tiếng Hindi / Thái…
            // parse theo văn hoá máy là hỏng.
            if (!string.IsNullOrEmpty(value)
                && DateTime.TryParseExact(value, "r", CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc))
                return true;
            utc = default;
            return false;
        }

        #region Tua thời gian để thử (chỉ Editor — Tools → BusOut Tools)
#if UNITY_EDITOR
        private const string DebugOffsetPrefKey = "BusOut.DebugTimeOffsetTicks";
        private static TimeSpan debugOffset;
        private static bool isDebugOffsetLoaded;

        /// Độ lệch cộng vào giờ game để thử quà ngày mà không đổi giờ máy. Nhớ qua các lần Play (PlayerPrefs của Editor).
        public static TimeSpan DebugOffset
        {
            get
            {
                if (!isDebugOffsetLoaded)
                {
                    isDebugOffsetLoaded = true;
                    long.TryParse(PlayerPrefs.GetString(DebugOffsetPrefKey, "0"), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out long ticks);
                    debugOffset = TimeSpan.FromTicks(ticks);
                }
                return debugOffset;
            }
        }

        public static void SetDebugOffset(TimeSpan offset)
        {
            debugOffset = offset;
            isDebugOffsetLoaded = true;
            PlayerPrefs.SetString(DebugOffsetPrefKey, offset.Ticks.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            // Đang Play: báo sang ngày ngay, không chờ vòng kiểm.
            if (HasInstance) Instance.CheckNewDay();
        }

        private static TimeSpan CurrentDebugOffset => DebugOffset;
#else
        private static TimeSpan CurrentDebugOffset => TimeSpan.Zero;
#endif
        #endregion
    }
}
