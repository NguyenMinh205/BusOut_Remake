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

        [Header("Menu: popup quà hằng ngày")]
        [Tooltip("Họa tiết xe trên thẻ Day 7 trôi ngang bao nhiêu đơn vị UI / giây (0 = đứng yên).")]
        [Min(0f)] public float dailyPatternScrollSpeed = 30f;

        [Header("Trong ván: xe chạy lên bến")]
        [Tooltip("Xe đang chạy từ bãi lên bến phóng to bấy nhiêu lần (tới nơi thì về cỡ cũ). Gốc: 1.1 " +
                 "(CAR_MOVE_TO_BUS_STOP_SCALE).")]
        public float carMoveScale = 1.1f;

        [Tooltip("Thời gian phóng to / thu về cỡ cũ khi bắt đầu chạy / tới bến (giây). Gốc không rõ — tự chọn.")]
        public float carMoveScaleDuration = 0.15f;

        [Tooltip("Ra khỏi bãi (tới làn trên / cần cẩu nhấc): to dần từ cỡ trong bãi (co theo bố cục cho vừa camera) lên cỡ " +
                 "ngoài bãi trong bấy nhiêu giây (giây). Bố cục không co thì không thấy gì.")]
        public float carLeaveLotScaleDuration = 0.25f;

        [Header("Trong ván: xe vào ô đỗ / khách ngồi / Undo")]
        [Tooltip("Xe tới ô đỗ (chạy tới hay cần cẩu hạ): thu về cỡ đỗ rồi nảy (đẩy scale) bấy nhiêu lần cỡ. 0 = không nảy.")]
        public float slotArrivePunch = 0.12f;

        [Tooltip("Thời gian nảy khi vào ô (giây).")]
        public float slotArrivePunchDuration = 0.25f;

        [Tooltip("Vòng sáng phẳng tỏa trên mặt ô khi xe vào ô và khi ô vừa mở (Prefab/VFX/SlotRing, tự trả về pool — " +
                 "AutoDespawn). Xoay của prefab giữ nguyên (nằm phẳng). Trống = không có (báo lỗi).")]
        public GameObject slotRingPrefab;

        [Tooltip("Số vòng tạo sẵn trong màn Loading (nhiều xe vào ô gần nhau).")]
        [Min(0)] public int slotRingPrewarmCount = 4;

        [Tooltip("Hiệu ứng ô đỗ đặt cao hơn điểm đỗ bấy nhiêu (đơn vị): nằm sát mặt đường mà không chớp (z-fighting).")]
        public float slotEffectHeight = 0.05f;

        [Tooltip("Mở ô (mua / hồi sinh): pháo sáng phát thêm ở ô vừa mở, cùng vòng sáng (dùng lại Prefab/VFX/LevelPassEffect " +
                 "của Menu). Trống = chỉ có vòng (báo lỗi).")]
        public GameObject slotUnlockEffectPrefab;

        [Tooltip("Cỡ pháo sáng mở ô = cỡ trong prefab × số này (prefab dựng cho bệ ở Menu).")]
        public float slotUnlockEffectScale = 0.8f;

        [Tooltip("Mỗi khách ngồi xuống: xe nhún (dẹt xuống) bấy nhiêu phần chiều cao. 0 = không. Khách lên dồn dập thì cú nhún " +
                 "đang chạy dở không bị làm lại.")]
        public float seatBouncePunch = 0.06f;

        [Tooltip("Thời gian một cú nhún khi khách ngồi (giây).")]
        public float seatBounceDuration = 0.15f;

        [Tooltip("Undo: xe về đúng chỗ cũ trong bãi thì nảy bấy nhiêu lần cỡ. 0 = không.")]
        public float undoArrivePunch = 0.1f;

        [Tooltip("Thời gian nảy khi về chỗ (giây).")]
        public float undoArrivePunchDuration = 0.25f;

        [Header("Trong ván: va chạm (xe đâm xe chặn)")]
        [Tooltip("Hiệu ứng va chạm (prefab particle, phát một lần, có AutoDespawn) sinh ở chỗ đâm. Xoay của prefab giữ nguyên " +
                 "khi sinh ra. Trống = không có (báo lỗi).")]
        public GameObject rammedEffectPrefab;

        [Tooltip("Số hiệu ứng va chạm tạo sẵn trong màn Loading (chạm liên tục nhiều xe bị chặn cùng lúc). Gốc: 10.")]
        [Min(0)] public int rammedEffectPrewarmCount = 10;

        [Tooltip("Hiệu ứng va chạm đặt trước đầu xe đâm bấy nhiêu (đơn vị, theo hướng đâm). Gốc: 0.3 " +
                 "(CAR_RAMMED_EFFECT_POINT_OFFSET — gốc dùng thế nào không rõ, đoán là lùi / tiến theo hướng đâm).")]
        public float rammedEffectForwardOffset = 0.3f;

        [Tooltip("Rung camera khi xe đâm xe chặn: lệch tối đa bấy nhiêu đơn vị (camera cách mặt đất ~45). 0 = không rung.")]
        public float ramShakeStrength = 0.15f;

        [Tooltip("Thời gian rung camera khi đâm (giây).")]
        public float ramShakeDuration = 0.15f;

        [Tooltip("Số lần lắc trong một lần rung (DOShakePosition vibrato).")]
        [Min(1)] public int cameraShakeVibrato = 12;

        [Header("Trong ván: cần cẩu (Crane)")]
        [Tooltip("Bóng tròn mờ bám dưới xe khi cần cẩu nhấc bay (Prefab/VFX/CraneShadow: SpriteRenderer nằm phẳng, KHÔNG gắn " +
                 "AutoDespawn — code trả về pool lúc đáp). Cỡ = cỡ prefab × cỡ xe lúc đó. Trống = không có bóng (báo lỗi).")]
        public GameObject craneShadowPrefab;

        [Tooltip("Bóng nằm cao hơn mặt đất bấy nhiêu (đơn vị): sát đất mà không chớp (z-fighting).")]
        public float craneShadowGroundOffset = 0.03f;

        [Tooltip("Xe ở độ cao nhất: bóng còn bấy nhiêu lần cỡ lúc chạm đất (nhỏ = cảm giác cao hơn).")]
        [Range(0f, 1f)] public float craneShadowMinScale = 0.55f;

        [Tooltip("Xe ở độ cao nhất: bóng còn bấy nhiêu lần độ đậm lúc chạm đất.")]
        [Range(0f, 1f)] public float craneShadowMinAlpha = 0.35f;

        [Tooltip("Bụi lúc xe đáp xuống ô (prefab particle tự trả về pool — mặc định dùng lại VehicleRammedEffect). " +
                 "Trống = không có bụi.")]
        public GameObject craneDustPrefab;

        [Tooltip("Cỡ bụi = cỡ trong prefab × số này.")]
        public float craneDustScale = 1.3f;

        [Header("Trong ván: Sort / Shuffle — cột sáng quét hàng")]
        [Tooltip("Cột sáng chạy dọc hàng khách từ đầu tới cuối khi dùng Sort / Shuffle (Prefab/VFX/QueueSweep, Vertical " +
                 "Billboard, mô phỏng Local, AutoDespawn ≥ thời gian quét). Trống = không có cột (báo lỗi); khách vẫn đổi màu lần lượt.")]
        public GameObject queueSweepPrefab;

        [Tooltip("Cột quét hết hàng trong bấy nhiêu giây; khách đổi màu + nảy đúng lúc cột đi qua (0 = cả hàng đổi cùng lúc).")]
        public float queueSweepDuration = 0.8f;

        [Tooltip("Tâm cột cao hơn mặt đường bấy nhiêu (đơn vị) — đặt = nửa chiều cao cột (3D Start Size Y) để đáy chạm đất.")]
        public float queueSweepHeight = 2f;

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

        [Header("UI: coin thưởng bay về thanh trên (CoinFlyView)")]
        [Tooltip("Icon coin bay (Prefab/VFX/UI/CoinFly: Image, Raycast Target tắt, KHÔNG AutoDespawn — code trả về pool). " +
                 "Trống = nhận thưởng về Menu ngay, không bay.")]
        public GameObject coinFlyPrefab;

        [Tooltip("Số coin bay mỗi lần nhận thưởng (số trên thanh đếm lên theo từng coin tới).")]
        [Min(1)] public int coinFlyCount = 8;

        [Tooltip("Coin sau bay trễ hơn coin trước bấy nhiêu giây.")]
        public float coinFlyInterval = 0.05f;

        [Tooltip("Mỗi coin bay hết đường trong bấy nhiêu giây.")]
        public float coinFlyDuration = 0.55f;

        [Tooltip("Coin xuất phát rải ngẫu nhiên trong bán kính bấy nhiêu quanh số thưởng (đơn vị UI, màn 1170×2532).")]
        public float coinFlySpread = 80f;

        [Tooltip("Đường bay cong lệch ngang tối đa bấy nhiêu (đơn vị UI) — mỗi coin một phía ngẫu nhiên.")]
        public float coinFlyCurve = 200f;

        [Tooltip("Tới nơi coin còn bấy nhiêu lần cỡ (thu dần trên đường bay).")]
        public float coinFlyEndScale = 0.7f;

        [Tooltip("Coin cuối tới nơi rồi chờ bấy nhiêu giây (nhìn số coin mới) mới chuyển về Menu.")]
        public float coinFlyEndDelay = 0.25f;

        [Header("UI: nút nhún khi bấm (ButtonPressFeedback, mọi Button)")]
        [Tooltip("Nhấn giữ: nút thu còn bấy nhiêu lần cỡ đang có (gốc AnimatedButton). 1 = không nhún.")]
        public float buttonPressScale = 0.92f;

        [Tooltip("Thời gian thu nhỏ khi nhấn / trở lại khi thả (giây).")]
        public float buttonPressDuration = 0.08f;

        [Header("UI: bàn tay hướng dẫn (TutorialHandView)")]
        [Tooltip("Lúc nhấn, bàn tay thu còn bấy nhiêu lần cỡ gốc.")]
        public float tutorialHandPressScale = 0.82f;

        [Tooltip("Thời gian một lần nhấn xuống (giây); nhả ra cũng bằng chừng đó.")]
        public float tutorialHandPressDuration = 0.45f;

        [Header("Trong ván: lộ màu (xe ẩn, khách ẩn)")]
        [Tooltip("Lúc lộ màu, xe / khách nảy thêm bấy nhiêu lần cỡ.")]
        public float hiddenRevealPunch = 0.15f;

        [Tooltip("Thời gian nảy khi lộ màu (giây).")]
        public float hiddenRevealDuration = 0.3f;

        [Header("Trong ván: khoá và chìa")]
        [Tooltip("Xe vừa được mở khoá nảy thêm bấy nhiêu lần cỡ.")]
        public float lockOpenPunch = 0.15f;

        [Tooltip("Thời gian nảy khi mở khoá (giây).")]
        public float lockOpenDuration = 0.3f;

        [Tooltip("Chạm xe đang bị khoá: ổ khoá nảy thêm bấy nhiêu lần cỡ.")]
        public float lockRejectPunch = 0.35f;

        [Header("Trong ván: bãi chờ")]
        [Tooltip("Xe trước rời chỗ bấy nhiêu giây thì xe kế mới lăn ra khỏi cổng.")]
        public float garageRollOutDelay = 0.3f;

        [Tooltip("Thời gian xe lăn ra khỏi cổng (giây). Đang lăn thì chưa chạm được.")]
        public float garageRollOutDuration = 0.35f;

        [Tooltip("Xe lăn ra từ trong cổng, cách chỗ đỗ bấy nhiêu (đơn vị thế giới).")]
        public float garageRollOutDistance = 2.5f;
    }
}
