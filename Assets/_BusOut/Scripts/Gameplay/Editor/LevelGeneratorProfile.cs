using System;
using UnityEngine;

namespace BusOut.Gameplay.Editor
{
    /// Thông số một mức khó của bộ sinh level. Lớp thường (không phải asset) để đo được ngoài Unity.
    [Serializable]
    public class LevelGeneratorProfile
    {
        public string displayName;

        [Header("Quy mô")]
        public int carCountMin;
        public int carCountMax;
        [Tooltip("Số màu xe. Nhiều màu hơn thì khách đầu hàng khó trùng xe đang ở bến hơn.")]
        public int colorCountMin;
        public int colorCountMax;
        [Tooltip("Tỉ trọng chọn xe 4 / 6 / 10 chỗ.")]
        public float weightFourSeat;
        public float weightSixSeat;
        public float weightTenSeat;

        [Header("Bố cục")]
        [Range(0f, 1f), Tooltip("Tỉ lệ xe đặt chéo 45°. Xe chéo khó nhìn đường ra hơn.")]
        public float diagonalChance;
        [Range(0f, 1f), Tooltip("Tỉ lệ xe xếp sát một xe có sẵn, cùng hướng: càng cao bãi càng thành hàng gọn.")]
        public float alignChance;
        [Range(0f, 1f), Tooltip("Tỉ lệ cả level đối xứng trái phải.")]
        public float mirrorChance;
        [Tooltip("Khe hở tối thiểu giữa hai xe (đơn vị level).")]
        public float carGap;
        [Tooltip("Lưới bắt toạ độ của xe không xếp sát xe khác.")]
        public float gridStep;

        [Header("Độ chặn")]
        [Range(0f, 1f), Tooltip("Tỉ lệ xe cố ý đặt chắn đường xe khác.")]
        public float blockChance;
        [Tooltip("Chuỗi chặn dài nhất mong muốn (xe A chặn B, B chặn C = 2).")]
        public int minChainDepth;
        public int maxChainDepth;
        [Tooltip("Số xe tối thiểu chạm là ra được ngay lúc vào ván.")]
        public int minFreeCars;
        [Range(0f, 1f), Tooltip("Tỉ lệ xe ra được ngay tối đa. Càng thấp người chơi càng phải tính thứ tự.")]
        public float maxFreeRatio;
        [Range(0f, 1f), Tooltip("Tỉ lệ xe cùng màu với xe chặn nó: gỡ xe chặn xong thì xe sau dùng được luôn, dễ hơn.")]
        public float sameColorAsBlockerChance;
        [Tooltip("Số màu khác nhau tối thiểu trong các xe ra được ngay. Bài học Sort / Undo cần xe ra được khác màu khách đầu hàng.")]
        public int minFreeColors;

        [Header("Cơ chế (chỉ áp khi bật trên Level Builder)")]
        [Range(0f, 1f), Tooltip("Xe ẩn màu: tỉ lệ xe đang bị chặn được ẩn màu. Xe ra được ngay không bao giờ ẩn (lộ ngay khi vào ván).")]
        public float hiddenCarChance;
        [Min(0), Tooltip("Khoá và chìa: số cặp mỗi level.")]
        public int lockPairCount;
        [Min(0), Tooltip("Ô công trường: số ô mở cuối bến bị chắn lúc đầu.")]
        public int constructionSlotCount;
        [Min(0), Tooltip("Ô công trường: mỗi ô cần thêm bấy nhiêu khách lên xe mới mở.")]
        public int constructionPassengers;
        [Min(0), Tooltip("Bãi chờ: số bãi mỗi level (đặt được ít hơn nếu bãi không còn chỗ trống phía sau xe nào).")]
        public int garageCount;
        [Min(1), Tooltip("Bãi chờ: số xe chờ trong mỗi bãi (không tính xe đứng trước cổng).")]
        public int garageQueueMin;
        public int garageQueueMax;

        [Header("Khách")]
        public MixData[] mix;
        public HardPointData[] hardPoints;

        [Header("Công sức tìm kiếm")]
        [Tooltip("Số vị trí thử cho mỗi xe.")]
        public int candidatesPerCar;
        [Tooltip("Số lần dựng lại bố cục để đạt mục tiêu.")]
        public int layoutAttempts;

        /// Giá trị khởi đầu của asset; đã đo bằng sim. Đổi số ở đây thì đổi cả asset LevelGeneratorConfig.
        /// Ô công trường đo 10/11: 1 ô × 20 khách gần như không đổi độ khó (bến ít ô thì kế hoạch khách cũng bớt điểm khó);
        /// 2 ô × 30 tạo áp lực đầu ván rõ; mức Khó 2 ô là quá (bot biết kế hoạch chỉ thắng 88%) nên dùng 1 ô × 60.
        public static LevelGeneratorProfile[] CreateDefaults() => new[]
        {
            new LevelGeneratorProfile
            {
                displayName = "Dễ",
                carCountMin = 18, carCountMax = 28,
                colorCountMin = 3, colorCountMax = 4,
                weightFourSeat = 5f, weightSixSeat = 3f, weightTenSeat = 1f,
                diagonalChance = 0f, alignChance = 0.8f, mirrorChance = 0.6f, carGap = 0.14f, gridStep = 0.5f,
                blockChance = 0.3f, minChainDepth = 1, maxChainDepth = 2, minFreeCars = 6, maxFreeRatio = 0.7f,
                sameColorAsBlockerChance = 0.6f,
                hiddenCarChance = 0.35f, lockPairCount = 2, constructionSlotCount = 2, constructionPassengers = 30, garageCount = 1, garageQueueMin = 2, garageQueueMax = 3,
                mix = new MixData[0],
                hardPoints = new HardPointData[0],
                candidatesPerCar = 60, layoutAttempts = 30,
            },
            new LevelGeneratorProfile
            {
                displayName = "Trung bình",
                carCountMin = 34, carCountMax = 46,
                colorCountMin = 4, colorCountMax = 6,
                weightFourSeat = 4f, weightSixSeat = 4f, weightTenSeat = 2f,
                diagonalChance = 0.15f, alignChance = 0.7f, mirrorChance = 0.4f, carGap = 0.12f, gridStep = 0.25f,
                blockChance = 0.55f, minChainDepth = 2, maxChainDepth = 4, minFreeCars = 5, maxFreeRatio = 0.5f,
                sameColorAsBlockerChance = 0.3f,
                hiddenCarChance = 0.35f, lockPairCount = 2, constructionSlotCount = 2, constructionPassengers = 30, garageCount = 1, garageQueueMin = 2, garageQueueMax = 3,
                mix = new[] { new MixData { at = 0f, size = 1, from = 10f, to = 35f } },
                hardPoints = new[] { new HardPointData { at = 45f, bonus = 0 } },
                candidatesPerCar = 80, layoutAttempts = 40,
            },
            new LevelGeneratorProfile
            {
                displayName = "Khó",
                carCountMin = 50, carCountMax = 64,
                colorCountMin = 6, colorCountMax = 8,
                weightFourSeat = 3f, weightSixSeat = 4f, weightTenSeat = 3f,
                diagonalChance = 0.3f, alignChance = 0.6f, mirrorChance = 0.2f, carGap = 0.1f, gridStep = 0.25f,
                blockChance = 0.8f, minChainDepth = 3, maxChainDepth = 6, minFreeCars = 4, maxFreeRatio = 0.4f,
                sameColorAsBlockerChance = 0.1f,
                hiddenCarChance = 0.35f, lockPairCount = 2, constructionSlotCount = 1, constructionPassengers = 60, garageCount = 1, garageQueueMin = 2, garageQueueMax = 3,
                mix = new[] { new MixData { at = 0f, size = 2, from = 25f, to = 55f } },
                hardPoints = new[] { new HardPointData { at = 30f, bonus = 0 }, new HardPointData { at = 65f, bonus = 1 } },
                candidatesPerCar = 100, layoutAttempts = 50,
            },
            new LevelGeneratorProfile
            {
                displayName = "Làm quen",
                carCountMin = 10, carCountMax = 16,
                colorCountMin = 2, colorCountMax = 3,
                weightFourSeat = 5f, weightSixSeat = 2f, weightTenSeat = 0f,
                diagonalChance = 0f, alignChance = 0.85f, mirrorChance = 0.5f, carGap = 0.16f, gridStep = 0.5f,
                blockChance = 0.35f, minChainDepth = 1, maxChainDepth = 2, minFreeCars = 4, maxFreeRatio = 0.8f,
                sameColorAsBlockerChance = 0.5f, minFreeColors = 2,
                hiddenCarChance = 0.35f, lockPairCount = 2, constructionSlotCount = 1, constructionPassengers = 20, garageCount = 1, garageQueueMin = 2, garageQueueMax = 3,
                mix = new MixData[0],
                hardPoints = new HardPointData[0],
                candidatesPerCar = 60, layoutAttempts = 40,
            },
        };
    }
}
