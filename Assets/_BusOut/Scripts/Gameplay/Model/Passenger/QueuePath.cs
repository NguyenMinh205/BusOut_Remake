using System.Collections.Generic;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Đường xếp hàng gấp khúc. "Khoảng cách" đo dọc đường tính từ ĐẦU hàng (0) tới CUỐI hàng (Length).
    /// Toán thuần: điểm lấy từ scene truyền vào.
    public sealed class QueuePath
    {
        private readonly Vector3[] points;  // đầu → cuối
        private readonly float[] distances; // khoảng cách dọc đường tới từng điểm

        public float Length { get; }

        /// <paramref name="headToTail"/>: các điểm từ đầu hàng tới cuối hàng (≥ 2 điểm). Mọi điểm được hạ về độ cao
        /// của điểm đầu: khách đi trên mặt đất dù điểm trong scene đặt lệch độ cao.
        public QueuePath(IReadOnlyList<Vector3> headToTail)
        {
            int count = headToTail.Count;
            points = new Vector3[count];
            distances = new float[count];
            float y = headToTail[0].y;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = headToTail[i];
                points[i] = new Vector3(p.x, y, p.z);
                if (i > 0) distances[i] = distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }
            Length = distances[count - 1];
        }

        /// Khoảng cách giữa 2 khách để <paramref name="count"/> khách trải đều từ đầu tới cuối hàng.
        public float SpacingFor(int count) => count > 1 ? Length / (count - 1) : 0f;

        public Vector3 PositionAt(float distance)
        {
            int segment = FindSegment(distance, out float t);
            return Vector3.Lerp(points[segment], points[segment + 1], t);
        }

        /// Hướng khách đứng nhìn: về phía đầu hàng.
        public Vector3 FacingAt(float distance)
        {
            int segment = FindSegment(distance, out _);
            Vector3 toHead = points[segment] - points[segment + 1];
            return toHead.sqrMagnitude > 1e-6f ? toHead.normalized : Vector3.forward;
        }

        /// Khoảng cách dọc đường của điểm trên đường gần <paramref name="position"/> nhất.
        /// Dùng khi đổi đích của khách đang đi dở: đi tiếp dọc đường thay vì cắt chéo qua góc.
        public float ClosestDistance(Vector3 position)
        {
            float best = 0f, bestSqr = float.MaxValue;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 a = points[i], ab = points[i + 1] - a;
                float lengthSqr = ab.sqrMagnitude;
                float t = lengthSqr > 1e-6f ? Mathf.Clamp01(Vector3.Dot(position - a, ab) / lengthSqr) : 0f;
                Vector3 offset = a + ab * t - position;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = distances[i] + (distances[i + 1] - distances[i]) * t;
                }
            }
            return best;
        }

        /// Đường đi dọc hàng từ <paramref name="fromDistance"/> tới <paramref name="toDistance"/>: đi qua đúng các góc
        /// nằm giữa, rồi tới đích. Không gồm điểm xuất phát.
        public void AppendWalk(float fromDistance, float toDistance, List<Vector3> result)
        {
            if (fromDistance > toDistance)
            {
                for (int i = points.Length - 1; i >= 0; i--)
                    if (distances[i] < fromDistance && distances[i] > toDistance) result.Add(points[i]);
            }
            else
            {
                for (int i = 0; i < points.Length; i++)
                    if (distances[i] > fromDistance && distances[i] < toDistance) result.Add(points[i]);
            }
            result.Add(PositionAt(toDistance));
        }

        private int FindSegment(float distance, out float t)
        {
            distance = Mathf.Clamp(distance, 0f, Length);
            for (int i = 0; i < points.Length - 1; i++)
            {
                float segmentLength = distances[i + 1] - distances[i];
                if (distance <= distances[i + 1] || i == points.Length - 2)
                {
                    t = segmentLength > 1e-6f ? (distance - distances[i]) / segmentLength : 0f;
                    return i;
                }
            }
            t = 0f;
            return 0;
        }
    }
}
