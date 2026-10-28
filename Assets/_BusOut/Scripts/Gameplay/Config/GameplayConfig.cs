using System;
using System.Collections.Generic;
using BusOut.Core;
using UnityEngine;

namespace BusOut.Gameplay
{
    [CreateAssetMenu(menuName = "BusOut/Gameplay Config", fileName = "GameplayConfig")]
    public class GameplayConfig : ScriptableObject
    {
        [Header("Hiệu năng")]
        [Tooltip("Khung hình / giây mục tiêu (Application.targetFrameRate). ⚠️ Không đặt thì điện thoại chạy 30 fps mặc định: " +
                 "xe chạy, khách đi trông giật dù máy còn dư sức. 60 = mượt, tốn pin hơn; ≤ 0 = để mặc định nền tảng.")]
        public int targetFrameRate = 60;

        [Header("Xe")]
        [Tooltip("Scale xe khi spawn.")]
        public float carScale = 0.85f;

        [Tooltip("Bảng màu xe theo mã màu trong level.")]
        public List<Color> carColors;

        [Header("Bến đỗ")]
        [Tooltip("Số ô đỗ mở sẵn (tính từ ô 1). Game gốc mở 4 ô, ô 5–7 mở bằng quảng cáo.")]
        public int openSlotCount = 4;

        [Tooltip("Ô trống cuối cùng nhấp nháy bao nhiêu lần lúc vừa thành ô cuối, rồi sáng đứng. Gốc: 4.")]
        public int warningBlinkCount = 4;

        [Tooltip("Thời gian một lần nhấp nháy (tắt + sáng), giây.")]
        public float warningBlinkDuration = 0.5f;

        [Header("Vào level")]
        [Tooltip("Mỗi xe phóng từ 0 lên cỡ thật trong bao lâu (giây).")]
        public float introCarDuration = 0.35f;

        [Tooltip("Xe xa tâm bãi nhất hiện trễ hơn xe gần tâm nhất bao lâu (giây). Chạm bị khoá tới khi xe cuối hiện xong " +
                 "(introSpread + introCarDuration).")]
        public float introSpread = 0.6f;

        [Header("Chạy lên bến")]
        [Tooltip("Tốc độ xe chạy từ bãi lên bến và rời bến (đơn vị/giây). Gốc: 30.")]
        public float carMoveSpeed = 30f;

        [Tooltip("Booster Crane: xe được nhấc cao bao nhiêu (đơn vị) trước khi bay sang bến.")]
        public float craneLiftHeight = 6f;

        [Tooltip("Booster Crane: thời gian nhấc lên (giây).")]
        public float craneRiseDuration = 0.25f;

        [Tooltip("Booster Crane: thời gian bay sang ô ở bến (giây).")]
        public float craneFlyDuration = 0.6f;

        [Tooltip("Booster Crane: thời gian hạ xuống ô (giây).")]
        public float craneDropDuration = 0.2f;

        [Tooltip("Nút booster đang \"giương\" (Crane chờ chọn xe) phóng to đứng yên bao nhiêu lần.")]
        public float boosterArmedScale = 1.15f;

        [Tooltip("Booster Undo: tốc độ xe tua ngược về chỗ cũ (đơn vị/giây). Nhanh hơn chạy thường để đọc là \"tua lại\".")]
        public float undoRewindSpeed = 60f;

        [Tooltip("Độ gắt khi xe bẻ lái theo hướng chạy (1/giây). Càng lớn xe quay đầu càng nhanh.")]
        public float carTurnSharpness = 20f;

        [Header("Kiểm tra bị chặn")]
        [Tooltip("Tia trái/phải lùi vào trong mép thân xe bao nhiêu (đơn vị). Bằng 0 thì xe đỗ sát cạnh nhau dễ bị tính là chặn.")]
        public float blockerRayInset = 0.08f;

        [Header("Bị chặn: lao tới rồi lùi về")]
        [Tooltip("Tốc độ lao tới xe chặn (đơn vị/giây).")]
        public float rammedMoveSpeed = 40f;

        [Tooltip("Thời gian lao tới tối thiểu (giây) — xe chặn ở rất gần vẫn thấy được cú đâm. Gốc: 0.125.")]
        public float rammedMinDuration = 0.1f;

        [Tooltip("Thời gian lùi về chỗ cũ (giây).")]
        public float rammedReturnDuration = 0.2f;

        [Tooltip("Khoảng hở còn lại giữa đầu xe và xe chặn khi lao tới (đơn vị).")]
        public float rammedGap = 0.05f;

        [Tooltip("Góc lắc của xe bị đâm (độ). Gốc: 4.")]
        public float rammedTiltDegrees = 4f;

        [Tooltip("Thời gian lắc của xe bị đâm (giây). Gốc: 0.15.")]
        public float rammedTiltDuration = 0.15f;

        [Tooltip("|dot(hướng đâm, hướng xe bị đâm)| lớn hơn số này thì coi là đâm dọc (xe chúi trước/sau), còn lại nghiêng ngang. Gốc: 0.9.")]
        public float rammedParallelThreshold = 0.9f;

        [Header("Hết ô đỗ")]
        [Tooltip("Biên độ rung ngang khi chạm xe mà hết ô đỗ (đơn vị).")]
        public float noSlotShakeDistance = 0.15f;

        [Tooltip("Thời gian rung (giây).")]
        public float noSlotShakeDuration = 0.25f;

        [Header("Hành khách")]
        [Tooltip("Số khách đứng trong hàng cùng lúc. Khoảng cách giữa 2 khách tự tính = độ dài đường hàng / (số khách − 1). " +
                 "Đường hiện dài 21 → 25 khách cách 0.875; khách scale 1.4 chiếm ~0.79 nên tối đa ~27.")]
        public int passengerQueueCount = 25;

        [Tooltip("Tốc độ khách mới đi từ SpawnPoint vào chỗ trong hàng (đơn vị/giây). Gốc: 10.")]
        public float passengerJoinSpeed = 10f;

        [Tooltip("Tốc độ cả hàng dồn lên khi người đầu rời đi (đơn vị/giây). Gốc: 15.")]
        public float passengerForwardSpeed = 15f;

        [Tooltip("Tốc độ khách rời hàng đi tới xe (đơn vị/giây). Gốc: 15.")]
        public float passengerToBusSpeed = 15f;

        [Tooltip("Độ gắt khi khách quay mặt theo hướng đi (1/giây).")]
        public float passengerTurnSharpness = 20f;

        [Tooltip("Số màu khách luôn được lên kế hoạch sẵn phía sau hàng (gốc: 24). Ít quá thì xe người chơi vừa lấy ra " +
                 "được ưu tiên sớm hơn; nhiều quá thì khách của xe đó phải chờ lâu.")]
        public int passengerPlanAheadCount = 24;

        [Header("Sinh khách: mix và hard point (đo bằng docs/tools/sim_board.py — đổi số thì sửa cả sim)")]
        [Tooltip("Nhóm khách cùng màu đứng liền nhau nhỏ nhất khi chia ghế một xe để trộn. GDD: 2.")]
        public int passengerGroupMin = 2;

        [Tooltip("Nhóm khách cùng màu đứng liền nhau lớn nhất. GDD: 4.")]
        public int passengerGroupMax = 4;

        [Tooltip("Hard point: số xe chặn (phải dọn trước) tối đa của xe được chọn, trước khi cộng bonus của level.")]
        public int hardPointBaseBlockers = 2;

        [Tooltip("Hard point: số ô mở phải chừa lại (1 cho chính xe khó + 1 dự phòng). Số xe chặn tối đa không vượt quá " +
                 "openSlotCount trừ số này. Đo 22/10/2026: chừa 2 → bot biết kế hoạch thắng 582/600 với 4 ô; chừa 1 → 549/600.")]
        public int hardPointSlotReserve = 2;

        [Tooltip("Cỡ khách khi ngồi trên xe so với khách gốc (mesh 1.48), tính trên màn hình. Gốc: 0.55 (nhìn bé). " +
                 "2 ghế cạnh nhau cách 0.425, khách rộng 0.66 × số này → trên ~0.64 thì vai 2 khách chạm nhau. " +
                 "Không phụ thuộc scale prefab khách hay scale xe.")]
        public float seatedPassengerScale = 0.65f;

        [Header("Lên xe")]
        [Tooltip("Thời gian giữa 2 khách liên tiếp rời hàng lên xe (giây). Gốc: 0.1.")]
        public float boardingInterval = 0.1f;

        [Tooltip("Xe đủ khách chờ bao lâu rồi mới rời bến (giây). Gốc: 0.5.")]
        public float carLeaveDelay = 0.5f;

        [Header("Chạm")]
        [Tooltip("Độ dài tia chạm từ camera (đơn vị). Phải lớn hơn khoảng cách camera tới bãi.")]
        public float tapRayDistance = 200f;

        [Header("Màn hình")]
        [Tooltip("Tỉ lệ rộng / cao của màn đã căn camera trong scene (1170×2532 → 0.462). Màn hẹp hơn thì tăng FOV giữ " +
                 "bề ngang, màn rộng hơn giữ FOV gốc. Căn lại camera ở máy khác thì đổi số này theo máy đó.")]
        public float cameraReferenceAspect = 1170f / 2532f;

        [Header("Level")]
        [Tooltip("Hết level cuối của file curve thì quay vòng từ level này (số level hiển thị vẫn tăng tiếp). Gốc để trên " +
                 "remote config, không tìm được; chọn 21 vì đã qua các level dễ đầu game.")]
        [Min(1)] public int loopStartLevel = 21;

        [Header("Kinh tế")]
        [Tooltip("Coin thưởng khi thắng một level thường. GDD: 20.")]
        public int winCoinReward = 20;

        [Tooltip("Nhận coin thắng bằng quảng cáo thì nhân lên mấy lần (gốc: x2).")]
        public int winAdCoinMultiplier = 2;

        [Tooltip("Giá coin hồi sinh +1 ô (popup Continue). GDD: 100.")]
        public int reviveCoinCost = 100;

        [Header("Mở thêm ô đỗ (chạm ô khoá ở bến)")]
        [Tooltip("Giá coin mở 1 ô khoá (chỉ cho level đang chơi). Bằng giá hồi sinh: cùng là +1 ô.")]
        public int slotUnlockCoinCost = 100;

        [Tooltip("Tiêu đề popup mở ô (dùng chung popup mua).")]
        public string slotUnlockName = "EXTRA SLOT";

        [Tooltip("Mô tả trong popup mở ô (tiếng Anh, như UI).")]
        [TextArea] public string slotUnlockDescription = "Unlock one more parking space for this level.";

        [Tooltip("Icon trong popup mở ô (gốc: icon_addoneslot). Để trống thì giữ icon đang có trên popup.")]
        public Sprite slotUnlockIcon;

        [Header("Giao diện trong ván")]
        [Tooltip("Chữ level trên thanh trên; {0} = số level.")]
        public string levelLabelFormat = "LEVEL {0}";

        [Tooltip("Booster theo thứ tự nút trên thanh dưới (trái → phải) = thứ tự mở khoá (27/10/2026: Shuffle, Sort, Undo, Crane). Số phần tử phải bằng số nút trong BoosterBarView.")]
        public List<BoosterDefinition> boosters = new List<BoosterDefinition>
        {
            new BoosterDefinition { type = BoosterType.Shuffle, displayName = "SHUFFLE", unlockLevel = 2,
                coinPrice = 120, buyAmount = 1, description = "Shuffle the queue to open up new moves." },
            new BoosterDefinition { type = BoosterType.Sort, displayName = "SORT", unlockLevel = 3,
                coinPrice = 150, buyAmount = 1, description = "Line up passengers so every car at the stop fills up and leaves." },
            new BoosterDefinition { type = BoosterType.Undo, displayName = "UNDO", unlockLevel = 4,
                coinPrice = 100, buyAmount = 1, description = "Send the last car you moved back to its parking spot." },
            new BoosterDefinition { type = BoosterType.Crane, displayName = "CRANE", unlockLevel = 5,
                coinPrice = 200, buyAmount = 1, description = "Lift any car, even a blocked one, straight to the stop." },
        };

        [Tooltip("Icon ổ khoá trên nút booster chưa mở. Để trống thì giữ icon đang có.")]
        public Sprite boosterLockIcon;

        [Tooltip("Nền nút booster đã mở (gốc: xanh dương). Để trống thì giữ nền đang có.")]
        public Sprite boosterUnlockedBackground;

        [Tooltip("Nền nút booster chưa mở (gốc: xám). Để trống thì giữ nền đang có.")]
        public Sprite boosterLockedBackground;

        [Tooltip("Sort / Shuffle: khách bị đổi màu nảy lên bao nhiêu (tỉ lệ cỡ) để người chơi thấy chỗ đổi.")]
        public float boosterRecolorPunch = 0.25f;

        [Tooltip("Sort / Shuffle: thời gian cú nảy khi khách đổi màu (giây).")]
        public float boosterRecolorDuration = 0.3f;

        [Tooltip("Nút booster rung ngang bao nhiêu (đơn vị UI) khi bấm mà không dùng được (không mất lượt).")]
        public float boosterDeniedShake = 18f;

        [Tooltip("Ngôn ngữ nút Language xoay vòng qua (code lưu vào save, displayName hiện trên nút). ⚠️ Font chữ phải có đủ " +
                 "glyph của displayName (vd. dấu tiếng Việt), không thì hiện ô vuông.")]
        public List<LanguageOption> languages = new List<LanguageOption>
        {
            new LanguageOption { code = "en", displayName = "English" },
            new LanguageOption { code = "vi", displayName = "Tiếng Việt" },
        };

        [Tooltip("Chữ trên nút booster chưa mở; {0} = level mở khoá.")]
        public string boosterLockedLabelFormat = "LEVEL {0}";

        [Tooltip("Thắng xong chờ bao lâu mới hiện bảng thắng (giây): để xe cuối chạy đi. Gốc: 1.4.")]
        public float winPopupDelay = 1.4f;

        [Tooltip("Thua xong chờ bao lâu mới hiện bảng thua (giây). Gốc: 1.25.")]
        public float losePopupDelay = 1.25f;

        [Tooltip("Mở popup: nền tối mờ dần vào trong bao lâu (giây).")]
        public float popupDimFadeDuration = 0.2f;

        [Tooltip("Mở popup: mỗi object hiện (phóng / rơi / trồi / hiện dần) trong bao lâu (giây).")]
        public float popupItemDuration = 0.35f;

        [Tooltip("Mở popup: object sau hiện trễ hơn object trước bao lâu (giây).")]
        public float popupItemInterval = 0.12f;

        [Tooltip("Mở popup: nút phụ (đóng, \"Tap to collect\") hiện trễ thêm bao lâu sau object cuối (giây). Để người " +
                 "chơi kịp nhìn nút chính trước.")]
        public float popupLateDelay = 0.8f;

        [Tooltip("Mở popup: quãng trượt của kiểu rơi xuống / trồi lên (đơn vị UI, theo Reference Resolution 1170×2532).")]
        public float popupSlideDistance = 160f;

        [Tooltip("WinPanel: số coin đếm từ 0 lên trong bao lâu (giây), bắt đầu lúc nút chứa số đó hiện.")]
        public float popupCoinCountDuration = 0.5f;

        [Tooltip("Chữ \"Hết chỗ!\" hiện đứng yên bao lâu (giây) trước khi mờ dần.")]
        public float noSlotMessageHold = 0.8f;

        [Tooltip("Thời gian chữ \"Hết chỗ!\" mờ dần (giây).")]
        public float noSlotMessageFade = 0.25f;

        public Color GetCarColor(int colorIndex)
        {
            if (carColors == null || carColors.Count == 0) return Color.white;
            if (colorIndex < 0 || colorIndex >= carColors.Count)
            {
                Debug.LogError($"[BusOut] Mã màu {colorIndex} vượt bảng màu ({carColors.Count} màu). Thêm màu vào GameplayConfig.carColors.");
                return Color.white;
            }
            return carColors[colorIndex];
        }

        public static GameplayConfig CreateDefault() => CreateInstance<GameplayConfig>();
    }

    /// Một ngôn ngữ trong cài đặt.
    [Serializable]
    public class LanguageOption
    {
        public string code;
        public string displayName;
    }

    /// Một booster trên thanh dưới: hiện gì, mở ở level nào.
    [Serializable]
    public class BoosterDefinition
    {
        public BoosterType type;
        public string displayName;
        public Sprite icon;
        [Tooltip("Level bắt đầu dùng được (remake: Shuffle 2, Sort 3, Undo 4, Crane 5).")]
        [Min(1)] public int unlockLevel = 1;
        [Tooltip("Giá coin mua thêm khi hết lượt (popup mua). GDD: Sort 150, Shuffle 120; Undo 100, Crane 200 (mạnh nhất).")]
        [Min(0)] public int coinPrice = 100;
        [Tooltip("Mỗi lần mua (coin hoặc quảng cáo) được bao nhiêu lượt.")]
        [Min(1)] public int buyAmount = 1;
        [Tooltip("Mô tả ngắn trong popup mua (tiếng Anh, như UI).")]
        [TextArea] public string description;
    }
}
