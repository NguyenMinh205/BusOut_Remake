using UnityEngine;

namespace BusOut.Core
{
    /// Số chỉnh được của HIỆU ỨNG (chỉ để nhìn — đổi không làm thay đổi luật chơi hay luồng màn hình): prefab hiệu ứng,
    /// cỡ, độ nảy / chúi / nhún, tia sáng, pháo giấy, hiệu ứng chạm, nút nhún. Tách khỏi GameplayConfig / MenuConfig /
    /// UIConfig (bạn chốt 4/11/2026) để chỉnh "game feel" ở MỘT chỗ, không đụng số cân bằng.
    /// Code tự nạp từ Resources (không có ô kéo) → asset phải nằm đúng Resources/FxConfig.asset.
    [CreateAssetMenu(menuName = "BusOut/Fx Config", fileName = "FxConfig")]
    public class FxConfig : ScriptableObject
    {
        public const string ResourcePath = "FxConfig";

        private static FxConfig cached;

        /// Config đang dùng (nạp một lần). Thiếu asset → báo lỗi + chạy số mặc định trong code.
        public static FxConfig Instance
        {
            get
            {
                if (cached != null) return cached;
                cached = Resources.Load<FxConfig>(ResourcePath);
                if (cached != null) return cached;
                Debug.LogError($"[BusOut] Không thấy Resources/{ResourcePath}.asset: hiệu ứng chạy số mặc định trong code. Tạo bằng " +
                               "Create → BusOut → Fx Config, đặt trong Assets/_BusOut/Resources, kéo các prefab hiệu ứng vào.");
                cached = CreateDefault();
                return cached;
            }
        }

        // Tắt Domain Reload (Enter Play Mode Options): biến static giữ bản của lần Play trước → reset.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => cached = null;

        public static FxConfig CreateDefault() => CreateInstance<FxConfig>();

        // ───────────────────────────── Menu ─────────────────────────────

        [Header("Menu: xe chạy sang bệ mới")]
        [Tooltip("Xe Menu phóng to bấy nhiêu lần lúc chạy (như xe trong ván: CAR_MOVE_TO_BUS_STOP_SCALE gốc 1.1).")]
        public float mapCarDriveScale = 1.1f;

        [Tooltip("Thời gian phóng to lúc xe bắt đầu chạy / thu về lúc tới bệ (giây). Như xe trong ván (carMoveScaleDuration).")]
        public float mapCarDriveScaleDuration = 0.15f;

        [Tooltip("Thân xe chúi tối đa bấy nhiêu độ: ngửa đầu khi tăng tốc, chúi đầu khi phanh (0 = không; âm = đảo chiều).")]
        public float mapCarTiltDegrees = 5f;

        [Tooltip("Thân xe nhún khi chạy: thấp xuống tối đa bấy nhiêu phần chiều cao (gốc Car-Menu.anim ≈ 0.09).")]
        public float mapCarBounceAmount = 0.08f;

        [Tooltip("Số lần nhún mỗi giây khi chạy (gốc Car-Menu.anim ≈ 4).")]
        public float mapCarBounceFrequency = 4f;

        [Tooltip("Tới bệ mới: thân xe nảy (đẩy scale) bấy nhiêu rồi về.")]
        public float mapCarArrivePunch = 0.12f;

        [Tooltip("Thời gian nảy khi tới bệ (giây).")]
        public float mapCarArrivePunchDuration = 0.3f;

        [Header("Menu: hiệu ứng (quy tắc: chỉ MỘT thứ chuyển động lặp trên màn — bạn chọn A 4/11)")]
        [Tooltip("Pháo sáng ở bệ vừa qua lúc xe rời bệ (prefab particle, tự trả về pool — AutoDespawn). Gốc: Finish + Glow + " +
                 "Explosive trên mỗi bệ. Local position / rotation của prefab = so với tâm bệ. Trống = không có (báo lỗi).")]
        public GameObject levelPassEffectPrefab;

        [Tooltip("Vòng tỏa dưới gầm xe Menu lúc xe đứng ở bệ level hiện tại (lặp; tắt khi xe chạy). Gốc: Bus/VFX/Idle. " +
                 "Local position / rotation của prefab = so với xe. Trống = không có (báo lỗi).")]
        public GameObject carIdleRingsPrefab;

        // ─────────────────────────── Trong ván ───────────────────────────

        [Header("Trong ván: xe chạy lên bến")]
        [Tooltip("Xe đang chạy từ bãi lên bến phóng to bấy nhiêu lần (tới nơi thì về cỡ cũ). Gốc: 1.1 " +
                 "(CAR_MOVE_TO_BUS_STOP_SCALE).")]
        public float carMoveScale = 1.1f;

        [Tooltip("Thời gian phóng to / thu về cỡ cũ khi bắt đầu chạy / tới bến (giây). Gốc không rõ — tự chọn.")]
        public float carMoveScaleDuration = 0.15f;

        [Tooltip("Ra khỏi bãi (tới làn trên / cần cẩu nhấc): to dần từ cỡ trong bãi (co theo bố cục cho vừa camera) lên cỡ " +
                 "ngoài bãi trong bấy nhiêu giây (giây). Bố cục không co thì không thấy gì.")]
        public float carLeaveLotScaleDuration = 0.25f;

        [Header("Trong ván: va chạm (xe đâm xe chặn)")]
        [Tooltip("Hiệu ứng va chạm (prefab particle, phát một lần, có AutoDespawn) sinh ở chỗ đâm. Xoay của prefab giữ nguyên " +
                 "khi sinh ra. Trống = không có (báo lỗi).")]
        public GameObject rammedEffectPrefab;

        [Tooltip("Số hiệu ứng va chạm tạo sẵn trong màn Loading (chạm liên tục nhiều xe bị chặn cùng lúc). Gốc: 10.")]
        [Min(0)] public int rammedEffectPrewarmCount = 10;

        [Tooltip("Hiệu ứng va chạm đặt trước đầu xe đâm bấy nhiêu (đơn vị, theo hướng đâm). Gốc: 0.3 " +
                 "(CAR_RAMMED_EFFECT_POINT_OFFSET — gốc dùng thế nào không rõ, đoán là lùi / tiến theo hướng đâm).")]
        public float rammedEffectForwardOffset = 0.3f;

        [Header("Trong ván: xe đủ khách (trước khi rời bến)")]
        [Tooltip("Xe nảy (đẩy scale) bấy nhiêu lần cỡ đỗ lúc khách cuối ngồi xuống.")]
        public float carFullPunch = 0.25f;

        [Tooltip("Thời gian nảy (giây). Nên ngắn hơn GameplayConfig › Car Leave Delay để xe nảy xong mới rời bến.")]
        public float carFullPunchDuration = 0.35f;

        [Tooltip("Hiệu ứng xe đủ khách — chọn NGẪU NHIÊN một cái mỗi lần (bạn chốt 4/11: Flash của package Lana, " +
                 "Prefab/VFX/CarFull/). Mỗi prefab tự trả về pool (AutoDespawn). Trống = không có (báo lỗi).")]
        public GameObject[] carFullEffectPrefabs = new GameObject[0];

        [Tooltip("Số bản tạo sẵn MỖI prefab trong màn Loading (nhiều xe đủ khách gần nhau thì dùng thêm).")]
        [Min(0)] public int carFullEffectPrewarmCount = 2;

        [Tooltip("Hiệu ứng đặt cao hơn tâm xe bấy nhiêu (đơn vị) — trên nóc xe.")]
        public float carFullEffectHeight = 2f;

        [Tooltip("Rồi kéo về phía camera bấy nhiêu (đơn vị): nằm TRƯỚC thân xe / xe bên cạnh, không bị che. Hiệu ứng luôn quay " +
                 "mặt về camera (prefab Lana dựng cho nhìn thẳng — để nguyên hướng thì camera nhìn xuống ~55° thấy dẹt).")]
        public float carFullEffectTowardCamera = 1f;

        [Tooltip("Cỡ hiệu ứng xe đủ khách = cỡ trong prefab × số này (1 = như prefab). Prefab Lana để Scaling Mode " +
                 "Hierarchy nên phóng cả khối: sao, chớp, tia.")]
        public float carFullEffectScale = 1.5f;

        [Header("Trong ván: popup thắng")]
        [Tooltip("Tia sáng sau phần thưởng xoay một vòng mỗi bấy nhiêu giây (chậm = sang; ≤ 0 = đứng yên).")]
        public float winRaysTurnDuration = 8f;

        [Tooltip("Cỡ pháo giấy (Scale của UI Particle trên WinConfettiFx — đặt mỗi lần popup thắng hiện, đè số trong scene). " +
                 "To hơn = hạt to hơn VÀ bay xa hơn.")]
        public float winConfettiScale = 60f;

        // ────────────────────────────── UI ──────────────────────────────

        [Header("UI: hiệu ứng chạm (TouchEffectView)")]
        [Tooltip("Độ phóng của UI Particle (đơn vị hạt → đơn vị UI 1170×2532). Chỉnh bằng mắt cho vòng tỏa cỡ một đầu ngón tay.")]
        public float touchEffectScale = 60f;

        [Tooltip("Số hiệu ứng chạm tạo sẵn (chạm dồn dập nhanh hơn thời gian một hiệu ứng ~0.4s thì dùng vòng lại cái cũ nhất).")]
        [Min(1)] public int touchEffectPoolSize = 6;

        [Header("UI: nút nhún khi bấm (ButtonPressFeedback, mọi Button)")]
        [Tooltip("Nhấn giữ: nút thu còn bấy nhiêu lần cỡ đang có (gốc AnimatedButton). 1 = không nhún.")]
        public float buttonPressScale = 0.92f;

        [Tooltip("Thời gian thu nhỏ khi nhấn / trở lại khi thả (giây).")]
        public float buttonPressDuration = 0.08f;
    }
}
