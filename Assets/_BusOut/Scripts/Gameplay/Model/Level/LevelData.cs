using System;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Thứ tự giữ đúng như game gốc vì file level dùng số nguyên này.
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
