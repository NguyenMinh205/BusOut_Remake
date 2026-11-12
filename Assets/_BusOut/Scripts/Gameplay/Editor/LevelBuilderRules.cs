using System.Collections.Generic;
using UnityEngine;

namespace BusOut.Gameplay.Editor
{
    /// Kích thước thân một loại xe trong HỆ TOẠ ĐỘ LEVEL (đã nhân carScale, chưa nhân độ co bố cục: toạ độ level cũng chưa co).
    public struct CarShape
    {
        public float HalfWidth;
        public float HalfLength;
        /// Tâm thân lệch so với gốc xe, theo trục phải / trục trước của xe.
        public float CenterRight;
        public float CenterForward;
    }

    /// Hình chữ nhật thân xe đã đặt vào bãi.
    public struct CarFootprint
    {
        public Vector2 Center;
        public Vector2 Forward;
        public Vector2 Right;
        public float HalfWidth;
        public float HalfLength;

        public bool Contains(Vector2 point)
        {
            Vector2 offset = point - Center;
            return Mathf.Abs(Vector2.Dot(offset, Right)) <= HalfWidth && Mathf.Abs(Vector2.Dot(offset, Forward)) <= HalfLength;
        }

        public void GetCorners(Vector2[] corners)
        {
            Vector2 side = Right * HalfWidth;
            Vector2 along = Forward * HalfLength;
            corners[0] = Center + along - side;
            corners[1] = Center + along + side;
            corners[2] = Center - along + side;
            corners[3] = Center - along - side;
        }
    }

    /// Một bãi chờ để soát: CarIds theo thứ tự ra (phần tử 0 = xe đứng trước cổng), Building = hình cổng.
    public struct GarageLayout
    {
        public int[] CarIds;
        public CarFootprint Building;
    }

    /// Kết quả soát một level. Các tập chứa chỉ số xe.
    public sealed class LevelReport
    {
        public readonly HashSet<int> Overlapping = new HashSet<int>();
        public readonly HashSet<int> OutOfFrame = new HashSet<int>();
        /// Xe không bao giờ ra được, kể cả khi mọi xe khác đã đi hết phần có thể đi.
        public readonly HashSet<int> Deadlocked = new HashSet<int>();
        /// Xe chạm là chạy được ngay.
        public readonly HashSet<int> FreeNow = new HashSet<int>();
    }

    /// Luật hình học của Level Builder, tách khỏi cửa sổ để kiểm được ngoài Unity.
    public static class LevelBuilderRules
    {
        private static readonly int[] RaySides = { -1, 0, 1 };
        private static readonly Vector2[] Corners = new Vector2[4];

        public static CarFootprint BuildFootprint(float x, float z, float rotY, CarShape shape)
        {
            float radians = rotY * Mathf.Deg2Rad;
            var forward = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            // Khớp Vector3.Cross(up, forward) trong CarView.FindAllAhead: sai dấu là tâm thân lệch sang phía kia.
            var right = new Vector2(forward.y, -forward.x);
            return new CarFootprint
            {
                Center = new Vector2(x, z) + right * shape.CenterRight + forward * shape.CenterForward,
                Forward = forward,
                Right = right,
                HalfWidth = shape.HalfWidth,
                HalfLength = shape.HalfLength,
            };
        }

        public static bool Overlaps(CarFootprint a, CarFootprint b)
        {
            Vector2 offset = b.Center - a.Center;
            return !IsSeparated(a.Right) && !IsSeparated(a.Forward) && !IsSeparated(b.Right) && !IsSeparated(b.Forward);

            bool IsSeparated(Vector2 axis) => Mathf.Abs(Vector2.Dot(offset, axis)) > ProjectedRadius(a, axis) + ProjectedRadius(b, axis);
        }

        /// ⚠️ Tia xuất phát từ BÊN TRONG hình thì không tính là trúng: PhysX bỏ qua collider chứa điểm đầu tia. Tính là trúng
        /// thì hai xe chồng nhau sẽ báo chặn nhau, trong khi lúc chơi chúng không chặn.
        public static bool RayHits(Vector2 origin, Vector2 direction, CarFootprint box)
        {
            Vector2 offset = origin - box.Center;
            float startRight = Vector2.Dot(offset, box.Right);
            float startForward = Vector2.Dot(offset, box.Forward);
            if (Mathf.Abs(startRight) < box.HalfWidth && Mathf.Abs(startForward) < box.HalfLength) return false;

            float near = 0f, far = float.PositiveInfinity;
            return ClipAxis(startRight, Vector2.Dot(direction, box.Right), box.HalfWidth, ref near, ref far)
                   && ClipAxis(startForward, Vector2.Dot(direction, box.Forward), box.HalfLength, ref near, ref far);
        }

        /// Mỗi xe: các xe nằm trên đường ra của nó, bắn 3 tia như <c>CarView.FindAllAhead</c>.
        /// Không giới hạn quãng tia theo làn đường như lúc chơi: làn nằm ngoài khung bãi nên mọi xe trong khung đều đã tính
        /// (xe lố khung thì đã bị báo riêng). Độ lùi tia không chia độ co bố cục: lệch dưới 0.02 đơn vị, không đáng đọc thêm scene.
        public static List<int>[] BuildAhead(IReadOnlyList<CarFootprint> cars, float sideInset)
        {
            var ahead = new List<int>[cars.Count];
            for (int id = 0; id < cars.Count; id++)
            {
                var hits = new List<int>();
                for (int other = 0; other < cars.Count; other++)
                    if (other != id && PathHits(cars[id], cars[other], sideInset)) hits.Add(other);
                ahead[id] = hits;
            }
            return ahead;
        }

        /// <paramref name="other"/> có nằm trên đường ra của <paramref name="car"/> không.
        public static bool PathHits(CarFootprint car, CarFootprint other, float sideInset)
        {
            float offset = Mathf.Max(0f, car.HalfWidth - sideInset);
            foreach (int side in RaySides)
                if (RayHits(car.Center + car.Right * (offset * side), car.Forward, other)) return true;
            return false;
        }

        public static bool IsInsideFrame(CarFootprint car, Vector2 frameMin, Vector2 frameMax)
        {
            car.GetCorners(Corners);
            foreach (Vector2 corner in Corners)
                if (corner.x < frameMin.x || corner.x > frameMax.x || corner.y < frameMin.y || corner.y > frameMax.y) return false;
            return true;
        }

        /// Hình cổng bãi chờ: nằm sau gốc xe đứng trước cổng, cách <paramref name="offset"/>, dài <paramref name="length"/> (đơn vị level).
        public static CarFootprint BuildGarageBuilding(float x, float z, float rotY, float offset, float length, float width)
        {
            CarFootprint direction = BuildFootprint(0f, 0f, rotY, default);
            return new CarFootprint
            {
                Center = new Vector2(x, z) - direction.Forward * (offset + length * 0.5f),
                Forward = direction.Forward,
                Right = direction.Right,
                HalfWidth = width * 0.5f,
                HalfLength = length * 0.5f,
            };
        }

        /// Cạnh chặn của bãi chờ, giống LevelSpawner.BuildBlockingGraph: xe chờ thứ k đứng đúng chỗ xe đầu nên bị chắn như xe đầu
        /// + phải đợi xe k−1; xe nào bị xe đầu chắn thì cũng bị mọi xe chờ chắn (chúng lần lượt ra đứng đúng ô đó).
        public static void AddGarageEdges(List<int>[] ahead, IReadOnlyList<GarageLayout> garages)
        {
            if (garages == null) return;
            foreach (GarageLayout garage in garages)
            {
                int[] ids = garage.CarIds;
                if (ids == null || ids.Length < 2) continue;
                int front = ids[0];
                for (int k = 1; k < ids.Length; k++)
                {
                    var list = new List<int>(ahead[front]);
                    if (!list.Contains(ids[k - 1])) list.Add(ids[k - 1]);
                    list.Remove(ids[k]);
                    ahead[ids[k]] = list;
                }
                for (int i = 0; i < ahead.Length; i++)
                {
                    if (System.Array.IndexOf(ids, i) >= 0 || !ahead[i].Contains(front)) continue;
                    for (int k = 1; k < ids.Length; k++)
                        if (!ahead[i].Contains(ids[k])) ahead[i].Add(ids[k]);
                }
            }
        }

        /// <paramref name="lockEdges"/>: cạnh khoá (x = xe bị khoá, y = xe mang chìa), tính như xe y chắn xe x, giống đồ thị lúc chơi.
        /// <paramref name="garages"/>: bãi chờ. Xe chờ (sau xe đầu) không có hình trong bãi nên bỏ khỏi soát chồng / lố khung / tia;
        /// cổng không có collider lúc chơi, nên xe nào phải chạy xuyên cổng coi như kẹt.
        public static LevelReport Analyze(IReadOnlyList<CarFootprint> cars, Vector2 frameMin, Vector2 frameMax, float sideInset,
            IReadOnlyList<Vector2Int> lockEdges = null, IReadOnlyList<GarageLayout> garages = null)
        {
            var report = new LevelReport();
            var queued = new bool[cars.Count];
            if (garages != null)
                foreach (GarageLayout garage in garages)
                    for (int k = 1; k < garage.CarIds.Length; k++) queued[garage.CarIds[k]] = true;

            for (int a = 0; a < cars.Count; a++)
            {
                if (queued[a]) continue;
                if (!IsInsideFrame(cars[a], frameMin, frameMax)) report.OutOfFrame.Add(a);
                for (int b = a + 1; b < cars.Count; b++)
                {
                    if (queued[b] || !Overlaps(cars[a], cars[b])) continue;
                    report.Overlapping.Add(a);
                    report.Overlapping.Add(b);
                }
            }

            var ahead = new List<int>[cars.Count];
            for (int id = 0; id < cars.Count; id++)
            {
                ahead[id] = new List<int>();
                if (queued[id]) continue;
                for (int other = 0; other < cars.Count; other++)
                    if (other != id && !queued[other] && PathHits(cars[id], cars[other], sideInset)) ahead[id].Add(other);
            }
            AddGarageEdges(ahead, garages);
            if (lockEdges != null)
                foreach (Vector2Int edge in lockEdges)
                    if (!ahead[edge.x].Contains(edge.y)) ahead[edge.x].Add(edge.y);

            // Xe phải chạy xuyên cổng bãi chờ: không bao giờ ra được.
            var throughGarage = new HashSet<int>();
            if (garages != null)
                foreach (GarageLayout garage in garages)
                {
                    if (!IsInsideFrame(garage.Building, frameMin, frameMax)) report.OutOfFrame.Add(garage.CarIds[0]);
                    for (int i = 0; i < cars.Count; i++)
                    {
                        if (queued[i]) continue;
                        if (Overlaps(garage.Building, cars[i])) report.Overlapping.Add(i);
                        if (PathHits(cars[i], garage.Building, sideInset)) throughGarage.Add(i);
                    }
                }

            for (int id = 0; id < cars.Count; id++)
            {
                if (ahead[id].Count == 0 && !throughGarage.Contains(id)) report.FreeNow.Add(id);
                report.Deadlocked.Add(id);
            }
            // Bóc dần xe không còn gì chắn phía trước. Bóc hết = có thứ tự ra hết bãi; còn sót = các xe chắn nhau thành vòng,
            // hoặc xếp hàng sau một vòng như thế.
            bool removedAny = true;
            while (removedAny)
            {
                removedAny = false;
                for (int id = 0; id < cars.Count; id++)
                {
                    if (!report.Deadlocked.Contains(id) || throughGarage.Contains(id)) continue;
                    bool blocked = false;
                    foreach (int other in ahead[id])
                        if (report.Deadlocked.Contains(other)) { blocked = true; break; }
                    if (blocked) continue;
                    report.Deadlocked.Remove(id);
                    removedAny = true;
                }
            }
            return report;
        }

        private static float ProjectedRadius(CarFootprint box, Vector2 axis) =>
            box.HalfWidth * Mathf.Abs(Vector2.Dot(box.Right, axis)) + box.HalfLength * Mathf.Abs(Vector2.Dot(box.Forward, axis));

        private static bool ClipAxis(float start, float direction, float halfSize, ref float near, ref float far)
        {
            if (Mathf.Abs(direction) < 1e-6f) return Mathf.Abs(start) <= halfSize;
            float first = (-halfSize - start) / direction;
            float second = (halfSize - start) / direction;
            if (first > second) (first, second) = (second, first);
            near = Mathf.Max(near, first);
            far = Mathf.Min(far, second);
            return near <= far;
        }
    }
}
