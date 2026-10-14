using System.Collections.Generic;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Làn đường vòng quanh bãi đỗ, toạ độ thế giới. Xe ra khỏi bãi chạy thẳng tới làn, rồi đi theo làn.
    public struct LaneRing
    {
        public float LeftX, RightX, BottomZ, TopZ;
    }

    /// Hình học đường đi của xe (tương đương LevelPointsController của game gốc). Toán thuần, không đụng scene.
    public static class ParkingRoute
    {
        private enum Side { Left, Right, Bottom, Top }

        /// Quãng đường chạy thẳng từ <paramref name="from"/> theo <paramref name="direction"/> tới khi chạm làn.
        /// Cũng là độ dài tia kiểm tra bị chặn: xe chỉ cần đường trống tới làn, xa hơn không còn gì chặn.
        public static float DistanceToRing(Vector3 from, Vector3 direction, LaneRing ring)
        {
            return DistanceToRing(from, Flatten(direction), ring, out _);
        }

        /// Đường từ chỗ đỗ trong bãi tới ô đỗ ở bến: thẳng tới làn → theo làn lên làn trên → tới ngang EntryPoint
        /// → EntryPoint → ParkPoint. Xe đỗ với đầu xe theo hướng EntryPoint → ParkPoint.
        public static void BuildToSlot(Vector3 carPosition, Vector3 carForward, LaneRing ring,
            Vector3 slotEntry, Vector3 slotPark, List<Vector3> result)
        {
            result.Clear();
            float y = carPosition.y;
            Vector3 direction = Flatten(carForward);
            float distance = DistanceToRing(carPosition, direction, ring, out Side side);
            Vector3 exit = carPosition + direction * distance;
            result.Add(exit);

            switch (side)
            {
                case Side.Left:
                    result.Add(new Vector3(ring.LeftX, y, ring.TopZ));
                    break;
                case Side.Right:
                    result.Add(new Vector3(ring.RightX, y, ring.TopZ));
                    break;
                case Side.Bottom:
                    // Chọn bên vòng ngắn hơn: quãng ngang = (lối ra → mép) + (mép → ô đỗ).
                    // ⚠️ So exit.x với slotEntry.x là sai: lối ra x=2, ô x=5 sẽ vòng bên trái dài gấp đôi.
                    float viaLeft = (exit.x - ring.LeftX) + (slotEntry.x - ring.LeftX);
                    float viaRight = (ring.RightX - exit.x) + (ring.RightX - slotEntry.x);
                    float sideX = viaLeft <= viaRight ? ring.LeftX : ring.RightX;
                    result.Add(new Vector3(sideX, y, ring.BottomZ));
                    result.Add(new Vector3(sideX, y, ring.TopZ));
                    break;
            }

            result.Add(new Vector3(slotEntry.x, y, ring.TopZ));
            result.Add(new Vector3(slotEntry.x, y, slotEntry.z));
            result.Add(new Vector3(slotPark.x, y, slotPark.z));
        }

        /// Đường rời bến: LÙI theo trục ô (ParkPoint → EntryPoint, kéo dài nếu cần) tới khi z bằng z của điểm rời bến,
        /// rồi chạy tiến tới điểm rời bến. Trả về số điểm đầu phải chạy lùi (để View giữ đầu xe không quay).
        public static int BuildLeave(Vector3 slotPark, Vector3 slotEntry, Vector3 leavingPoint, List<Vector3> result)
        {
            result.Clear();
            float y = slotPark.y;
            Vector3 backDirection = slotEntry - slotPark;
            backDirection.y = 0f;
            float axisLength = backDirection.magnitude;
            backDirection = axisLength > 1e-4f ? backDirection / axisLength : Vector3.back;

            // Quãng lùi để z chạm z của điểm rời bến. Trục ô gần nằm ngang thì z không đổi được: lùi về EntryPoint.
            float reverseDistance = Mathf.Abs(backDirection.z) > 0.01f
                ? (leavingPoint.z - slotPark.z) / backDirection.z
                : axisLength;
            // Âm = điểm rời bến nằm phía đầu xe (đặt sai scene): không lùi, đi thẳng ra luôn.
            if (reverseDistance < 0f) reverseDistance = 0f;

            Vector3 reverseEnd = slotPark + backDirection * reverseDistance;
            result.Add(new Vector3(reverseEnd.x, y, reverseEnd.z));
            result.Add(new Vector3(leavingPoint.x, y, leavingPoint.z));
            return reverseDistance > 0f ? 1 : 0;
        }

        private static float DistanceToRing(Vector3 from, Vector3 direction, LaneRing ring, out Side side)
        {
            const float Epsilon = 1e-4f;
            float toX = float.PositiveInfinity, toZ = float.PositiveInfinity;
            if (direction.x > Epsilon) toX = (ring.RightX - from.x) / direction.x;
            else if (direction.x < -Epsilon) toX = (ring.LeftX - from.x) / direction.x;
            if (direction.z > Epsilon) toZ = (ring.TopZ - from.z) / direction.z;
            else if (direction.z < -Epsilon) toZ = (ring.BottomZ - from.z) / direction.z;

            float distance;
            if (toX < toZ)
            {
                distance = toX;
                side = direction.x > 0f ? Side.Right : Side.Left;
            }
            else
            {
                distance = toZ;
                side = direction.z > 0f ? Side.Top : Side.Bottom;
            }
            // Xe nằm ngoài làn (đặt sai level) thì coi như đã ở trên làn, không chạy lùi.
            return distance > 0f ? distance : 0f;
        }

        private static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
        }
    }
}
