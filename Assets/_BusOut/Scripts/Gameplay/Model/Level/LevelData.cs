using System;
using UnityEngine;

namespace BusOut.Gameplay
{
    public enum VehicleType { FourSeat = 0, SixSeat = 1, TenSeat = 2, Vip = 3 }

    /// Một file curve_*.json (level gốc đã giải mã). Tên field phải trùng tên khoá JSON — JsonUtility đọc theo tên.
    [Serializable]
    public class LevelCurveData
    {
        public string curve;
        public LevelData[] levels;

        public static LevelCurveData Parse(string json) => JsonUtility.FromJson<LevelCurveData>(json);

        public LevelData FindLevel(int levelNumber)
        {
            if (levels == null) return null;
            for (int i = 0; i < levels.Length; i++)
                if (levels[i].index == levelNumber) return levels[i];
            return null;
        }

        /// Số level hiển thị (tăng mãi) → số level trong file: quá level cuối thì quay vòng từ <paramref name="loopStart"/>.
        /// Vd. file 1..120, loopStart 21: 120 → 120, 121 → 21, 220 → 120, 221 → 21.
        public int ResolveLayoutNumber(int levelNumber, int loopStart)
        {
            int last = LastLevelNumber;
            if (levelNumber <= last || last <= 0) return levelNumber;
            int start = Math.Max(1, Math.Min(loopStart, last));
            int cycle = last - start + 1;
            return start + (levelNumber - start) % cycle;
        }

        /// Số level lớn nhất trong file (0 khi file rỗng).
        public int LastLevelNumber
        {
            get
            {
                int last = 0;
                if (levels != null)
                    for (int i = 0; i < levels.Length; i++) last = Math.Max(last, levels[i].index);
                return last;
            }
        }
    }

    [Serializable]
    public class LevelData
    {
        public int index;
        public VehicleData[] vehicles;
        /// Tỉ lệ trộn nhóm khách theo tiến độ (ProgressMixData của gốc).
        public MixData[] mix;
        /// Các điểm khó theo tiến độ (ProgressHardPointData của gốc).
        public HardPointData[] hardPoints;
    }

    /// Từ <see cref="at"/>% tiến độ: tỉ lệ trộn nội suy <see cref="from"/> → <see cref="to"/> (%) tới mốc sau (hoặc 100%).
    [Serializable]
    public class MixData
    {
        public float at;
        /// MixGroupSizeType gốc (1 Easy, 2 Medium, 3 Hard). Remake hiểu là: trộn thêm tối đa <c>size</c> xe khác (ĐOÁN).
        public int size;
        public float from;
        public float to;
    }

    /// Tới <see cref="at"/>% tiến độ: cố ý chọn một xe đang bị chặn.
    [Serializable]
    public class HardPointData
    {
        public float at;
        /// harderBlockingPointsBonus gốc. Remake hiểu là: cộng vào số xe chặn tối đa được phép (ĐOÁN).
        public int bonus;
        /// SeriesPlanningType gốc — remake luôn lên kế hoạch liền chuỗi xe chặn, chưa phân biệt giá trị này.
        public int series;
    }

    /// Toạ độ x/z nằm trong hệ toạ độ bãi đỗ gốc (x ±10.5, z -14.87..10.6) — đặt ParkingLot đúng gốc là khớp.
    [Serializable]
    public class VehicleData
    {
        public int type;
        public int color;
        public float x;
        public float z;
        public float rotY;
        public bool hidden;
        public bool autoMove;
        public int tool;

        public VehicleType Type => (VehicleType)type;

        /// Xe nằm trong garage / băng chuyền / bàn xoay (chưa hỗ trợ). -1 = đỗ tự do.
        public bool IsInTool => tool != -1;
    }
}
