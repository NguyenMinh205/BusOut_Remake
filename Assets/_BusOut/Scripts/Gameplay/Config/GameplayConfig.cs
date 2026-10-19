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

        [Tooltip("Ô trống cuối cùng nhấp nháy bao nhiêu lần lúc vừa thành ô cuối, rồi sáng đứng. Gốc: 4.")]
        public int warningBlinkCount = 4;

        [Tooltip("Thời gian một lần nhấp nháy (tắt + sáng), giây. Gốc: 0.5.")]
        public float warningBlinkDuration = 0.5f;

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
