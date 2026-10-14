using System.Collections.Generic;
using UnityEngine;

namespace BusOut.Gameplay
{
    [CreateAssetMenu(menuName = "BusOut/Gameplay Config", fileName = "GameplayConfig")]
    public class GameplayConfig : ScriptableObject
    {
        [Header("Xe")]
        [Tooltip("Scale xe khi spawn. Level gốc được xếp với xe scale 0.85 — đổi số này là xe chồng lên nhau hoặc hở ra.")]
        public float carScale = 0.85f;

        [Tooltip("Bảng màu xe theo mã màu trong level (0..7 = A..H của game gốc).")]
        public List<Color> carColors;

        [Header("Bến đỗ")]
        [Tooltip("Số ô đỗ mở sẵn (tính từ ô 1). Game gốc mở 4 ô, ô 5–7 mở bằng quảng cáo.")]
        public int openSlotCount = 4;

        [Header("Chạy lên bến")]
        [Tooltip("Tốc độ xe chạy từ bãi lên bến và rời bến (đơn vị/giây). Gốc: 30.")]
        public float carMoveSpeed = 30f;

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

        [Header("Chạm")]
        [Tooltip("Độ dài tia chạm từ camera (đơn vị). Phải lớn hơn khoảng cách camera tới bãi.")]
        public float tapRayDistance = 200f;

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
}
