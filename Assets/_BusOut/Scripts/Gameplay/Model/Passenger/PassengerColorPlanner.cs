using System;
using System.Collections.Generic;

namespace BusOut.Gameplay
{
    /// Số chỉnh được của bộ lên kế hoạch màu (lấy từ GameplayConfig; Model không giữ ScriptableObject).
    public struct PassengerPlanSettings
    {
        public int GroupMin;
        public int GroupMax;
        /// Số xe chặn tối đa của xe hard point, đã trừ ô chừa lại (= openSlotCount - hardPointSlotReserve).
        public int HardPointSlotLimit;
        public int HardPointBaseBlockers;
    }

    /// Lên kế hoạch màu hành khách theo xe để level luôn giải được (thuật toán "Puzzle" của gốc, bản remake):
    /// mỗi lần chọn một xe chắc chắn lấy ra được, sinh đủ số ghế của nó theo màu của nó.
    /// Tổng khách mỗi màu = tổng ghế xe màu đó. Độ khó:
    /// - Mix: theo tỉ lệ của level, trộn thêm 1..size xe khác (cũng lấy ra được), chia ghế mỗi xe thành nhóm 2–4 người,
    ///   xen kẽ nhóm sao cho 2 nhóm cùng màu không liền nhau → người chơi phải có nhiều xe ở bến cùng lúc.
    /// - Hard point: tới mốc tiến độ, cố ý chọn xe ĐANG BỊ CHẶN (chuỗi chặn không vòng, số xe chặn có giới hạn), rồi lên
    ///   kế hoạch LIỀN ngay sau nó các xe chặn (theo thứ tự gỡ được) → người chơi phải đưa xe chặn lên bến chờ trước.
    ///   ⚠️ Không lên kế hoạch liền chuỗi thì khách của xe chặn tới sau ~24 khách xe khác: bến kẹt cứng
    ///   (đo 22/10/2026, chỉ hard point không mix: bot biết kế hoạch chỉ thắng 438/600 thay vì 573/600).
    /// Đo bằng docs/tools/sim_board.py (chép đúng logic này).
    public sealed class PassengerColorPlanner
    {
        private const int MaxMixedCars = 4; // xe chính + tối đa 3 xe trộn (size 3 = Hard)

        private readonly IReadOnlyList<CarModel> cars;
        private readonly BlockingGraph graph;
        private readonly Random random;
        private readonly PassengerPlanSettings settings;
        private readonly MixData[] mixEntries;        // đã sắp theo at
        private readonly HardPointData[] hardPoints;
        private readonly bool[] hardPointFired;
        private readonly bool[] planned;
        private readonly int totalSeats;
        private int plannedSeats;
        private int unplannedCount;

        private readonly Queue<int> series = new Queue<int>();
        private readonly List<CarModel> candidates = new List<CarModel>();
        private readonly List<CarModel> chosen = new List<CarModel>(MaxMixedCars);
        private readonly List<int>[] groupSizes = new List<int>[MaxMixedCars];
        private readonly int[] groupCursor = new int[MaxMixedCars];
        private readonly List<int> order = new List<int>(MaxMixedCars);
        // Bộ đệm tìm chuỗi chặn.
        private readonly HashSet<int> closure = new HashSet<int>();
        private readonly HashSet<int> remaining = new HashSet<int>();
        private readonly Stack<int> stack = new Stack<int>();
        private readonly List<int> layer = new List<int>();
        private readonly List<int> extractOrder = new List<int>();
        // ⚠️ Dựng sẵn một lần: truyền method group (PickRandom(IsReachable)) là mỗi lần gọi cấp phát một delegate mới
        // (C# của Unity chưa cache), mỗi lượt lên kế hoạch gọi 2–8 lần → rác GC giữa ván.
        private readonly Func<CarModel, bool> hasLeftLot;
        private readonly Func<CarModel, bool> isReachable;

        public bool HasMore => unplannedCount > 0;

        /// Lần chọn gần nhất phải chọn bừa (không còn xe chắc chắn lấy ra được): Presenter nên cảnh báo.
        public bool LastPickWasForced { get; private set; }

        /// Lần chọn gần nhất là một hard point (Presenter ghi log để thử).
        public bool LastPickWasHardPoint { get; private set; }

        /// Số khách chưa lên kế hoạch (cho biển đếm: cộng thêm số khách đang chờ trong hàng).
        public int UnplannedSeats { get; private set; }

        public PassengerColorPlanner(IReadOnlyList<CarModel> cars, BlockingGraph graph, Random random,
            LevelData level, PassengerPlanSettings settings)
        {
            this.cars = cars;
            this.graph = graph;
            this.random = random;
            this.settings = settings;
            planned = new bool[cars.Count];
            unplannedCount = cars.Count;
            foreach (CarModel car in cars) totalSeats += car.SeatCount;
            UnplannedSeats = totalSeats;

            mixEntries = level?.mix != null ? (MixData[])level.mix.Clone() : new MixData[0];
            Array.Sort(mixEntries, (a, b) => a.at.CompareTo(b.at));
            hardPoints = level?.hardPoints ?? new HardPointData[0];
            hardPointFired = new bool[hardPoints.Length];
            for (int i = 0; i < MaxMixedCars; i++) groupSizes[i] = new List<int>(8);
            hasLeftLot = HasLeftLot;
            isReachable = IsReachable;
        }

        /// Lên kế hoạch lượt tiếp theo, thêm màu khách vào <paramref name="colors"/>. False khi đã hết xe.
        /// Thứ tự chọn xe chính: chuỗi xe chặn đang chờ → hard point (nếu tới mốc) → xe an toàn → chọn bừa.
        /// Xe an toàn = ưu tiên 1: xe đã rời bãi mà chưa có kế hoạch (để lâu là chiếm ô); ưu tiên 2: xe trong bãi mà
        /// mọi xe chặn phía trước đều đã có kế hoạch hoặc đã rời bãi. Xe rời bãi không bao giờ quay lại nên xe đã
        /// "thoát được" thì mãi thoát được.
        ///   "Đã đi hẳn mà chưa có kế hoạch" xảy ra khi người chơi lấy xe C cùng màu với xe P đã có kế hoạch: khách của P
        ///   lên C và C đi luôn. Khách tính theo TỔNG GHẾ TỪNG MÀU nên phần ghế của C giờ thành khách cho P — vẫn đúng.
        public bool TryPlanNext(List<int> colors)
        {
            if (!HasMore) return false;
            float progress = totalSeats > 0 ? 100f * plannedSeats / totalSeats : 100f;
            LastPickWasForced = false;
            LastPickWasHardPoint = false;

            CarModel primary = null;
            while (primary == null && series.Count > 0)
            {
                int id = series.Dequeue();
                if (!planned[id]) primary = cars[id];
            }
            bool inSeries = primary != null;
            if (primary == null) primary = TryPickHardPoint(progress);
            if (primary == null) primary = PickSafe();
            // ⚠️ Không còn xe nào chắc chắn lấy ra được: level có xe chặn vòng nhau (vd. 2 xe đâm đầu vào nhau).
            // Lấy xe ít bị chặn nhất để không đứng hình; level này có thể không giải được.
            if (primary == null)
            {
                primary = LeastBlocked();
                LastPickWasForced = primary != null;
            }
            if (primary == null)
            {
                // Không còn xe nào để chọn dù đếm còn (danh sách xe đã bị thay ngoài ý muốn): dừng hẳn thay vì lỗi null.
                unplannedCount = 0;
                UnplannedSeats = 0;
                return false;
            }

            chosen.Clear();
            MarkPlanned(primary);
            // Không trộn khi đang đi chuỗi hard point: chuỗi đó đã đủ khó, trộn thêm là bến kẹt.
            if (!inSeries && series.Count == 0) AddMixedCars(progress);
            for (int i = 0; i < chosen.Count; i++) plannedSeats += chosen[i].SeatCount;
            EmitInterleaved(colors);
            return true;
        }

        private void MarkPlanned(CarModel car)
        {
            planned[car.Id] = true;
            unplannedCount--;
            UnplannedSeats -= car.SeatCount;
            chosen.Add(car);
        }

        #region Hard point

        /// Mỗi mốc hard point chỉ nổ một lần (lượt đầu tiên tiến độ vượt mốc). Không có xe hợp lệ thì bỏ mốc đó.
        private CarModel TryPickHardPoint(float progress)
        {
            for (int h = 0; h < hardPoints.Length; h++)
            {
                if (hardPointFired[h] || progress < hardPoints[h].at) continue;
                hardPointFired[h] = true;
                int allowed = Math.Max(1, Math.Min(settings.HardPointSlotLimit,
                    settings.HardPointBaseBlockers + hardPoints[h].bonus));
                CarModel pick = PickMostBlocked(allowed);
                if (pick == null) return null;
                // Lên kế hoạch liền ngay sau xe khó: các xe chặn nó, theo thứ tự gỡ được.
                BuildClosure(pick.Id);
                BuildExtractOrder();
                for (int i = 0; i < extractOrder.Count; i++) series.Enqueue(extractOrder[i]);
                LastPickWasHardPoint = true;
                return pick;
            }
            return null;
        }

        /// Xe trong bãi có nhiều xe chặn nhất (1..allowed), chuỗi chặn không vòng; ngẫu nhiên giữa các xe bằng điểm.
        private CarModel PickMostBlocked(int allowed)
        {
            candidates.Clear();
            int best = 0;
            for (int i = 0; i < cars.Count; i++)
            {
                if (planned[i] || cars[i].State != CarState.Parked) continue;
                if (!BuildClosure(i)) continue;
                int count = closure.Count;
                if (count < 1 || count > allowed || count < best) continue;
                if (count > best)
                {
                    best = count;
                    candidates.Clear();
                }
                candidates.Add(cars[i]);
            }
            return candidates.Count > 0 ? candidates[random.Next(candidates.Count)] : null;
        }

        /// Gom mọi xe phải dọn trước khi <paramref name="carId"/> ra được (trong bãi, chưa có kế hoạch) vào closure.
        /// False nếu có vòng chặn (không gỡ hết được).
        private bool BuildClosure(int carId)
        {
            closure.Clear();
            stack.Clear();
            PushBlockers(carId);
            while (stack.Count > 0)
            {
                int id = stack.Pop();
                if (id == carId) return false;
                if (!closure.Add(id)) continue;
                PushBlockers(id);
            }
            return BuildExtractOrder();
        }

        /// Thứ tự gỡ closure: lần lượt các xe không còn bị xe nào trong tập chặn. False nếu kẹt (vòng chặn).
        private bool BuildExtractOrder()
        {
            extractOrder.Clear();
            remaining.Clear();
            remaining.UnionWith(closure);
            while (remaining.Count > 0)
            {
                layer.Clear();
                foreach (int id in remaining)
                    if (!IsBlockedWithin(id, remaining)) layer.Add(id);
                if (layer.Count == 0) return false;
                layer.Sort(); // thứ tự cố định: cùng hạt giống ra cùng kết quả
                for (int i = 0; i < layer.Count; i++)
                {
                    remaining.Remove(layer[i]);
                    extractOrder.Add(layer[i]);
                }
            }
            return true;
        }

        private bool IsBlockedWithin(int carId, HashSet<int> set)
        {
            IReadOnlyList<int> ahead = graph.AheadOf(carId);
            for (int i = 0; i < ahead.Count; i++)
                if (set.Contains(ahead[i]) && IsPendingBlocker(ahead[i])) return true;
            return false;
        }

        private void PushBlockers(int carId)
        {
            IReadOnlyList<int> ahead = graph.AheadOf(carId);
            for (int i = 0; i < ahead.Count; i++)
                if (IsPendingBlocker(ahead[i])) stack.Push(ahead[i]);
        }

        /// Xe còn đứng trong bãi và chưa có kế hoạch: còn phải dọn.
        private bool IsPendingBlocker(int carId) => !planned[carId] && cars[carId].State == CarState.Parked;

        #endregion

        #region Mix

        private void AddMixedCars(float progress)
        {
            if (!TryGetMix(progress, out float percent, out int size)) return;
            if (random.NextDouble() * 100.0 >= percent) return;
            int extra = random.Next(1, Math.Min(size, MaxMixedCars - 1) + 1);
            for (int i = 0; i < extra; i++)
            {
                CarModel car = PickSafe();
                if (car == null) break;
                MarkPlanned(car);
            }
        }

        /// Mốc mix cuối cùng có at ≤ tiến độ; tỉ lệ nội suy from → to tới mốc sau (hoặc 100%).
        private bool TryGetMix(float progress, out float percent, out int size)
        {
            percent = 0f;
            size = 1;
            int current = -1;
            for (int i = 0; i < mixEntries.Length; i++)
                if (progress >= mixEntries[i].at) current = i;
            if (current < 0) return false;
            MixData entry = mixEntries[current];
            float end = current + 1 < mixEntries.Length ? mixEntries[current + 1].at : 100f;
            float t = end > entry.at ? Math.Min(1f, (progress - entry.at) / (end - entry.at)) : 0f;
            percent = entry.from + (entry.to - entry.from) * t;
            size = Math.Max(1, entry.size);
            return true;
        }

        #endregion

        #region Chia nhóm và xen kẽ

        /// Một xe → một khối đủ ghế. Nhiều xe → chia mỗi xe thành nhóm GroupMin..GroupMax người, lần lượt lấy nhóm
        /// của xe ngẫu nhiên, tránh 2 nhóm cùng màu liền nhau (cùng màu khác xe vẫn có thể liền nếu không còn cách).
        private void EmitInterleaved(List<int> colors)
        {
            if (chosen.Count == 1)
            {
                for (int i = 0; i < chosen[0].SeatCount; i++) colors.Add(chosen[0].ColorIndex);
                return;
            }
            for (int c = 0; c < chosen.Count; c++)
            {
                SplitSeats(chosen[c].SeatCount, groupSizes[c]);
                groupCursor[c] = 0;
            }
            int lastColor = -1;
            while (true)
            {
                order.Clear();
                for (int c = 0; c < chosen.Count; c++)
                    if (groupCursor[c] < groupSizes[c].Count) order.Add(c);
                if (order.Count == 0) break;
                Shuffle(order);
                int pick = order[0];
                for (int i = 0; i < order.Count; i++)
                {
                    if (chosen[order[i]].ColorIndex == lastColor) continue;
                    pick = order[i];
                    break;
                }
                int size = groupSizes[pick][groupCursor[pick]++];
                for (int k = 0; k < size; k++) colors.Add(chosen[pick].ColorIndex);
                lastColor = chosen[pick].ColorIndex;
            }
        }

        /// Chia ghế thành nhóm ngẫu nhiên GroupMin..GroupMax; phần đuôi nhỏ hơn GroupMin thì gộp vào nhóm cuối.
        private void SplitSeats(int seats, List<int> result)
        {
            result.Clear();
            int min = Math.Max(1, settings.GroupMin);
            int max = Math.Max(min, settings.GroupMax);
            int left = seats;
            while (left > 0)
            {
                int size = Math.Min(left, random.Next(min, max + 1));
                if (left - size < min) size = left;
                result.Add(size);
                left -= size;
            }
        }

        private void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        #endregion

        #region Chọn xe an toàn

        private CarModel PickSafe() => PickRandom(hasLeftLot) ?? PickRandom(isReachable);

        private static bool HasLeftLot(CarModel car) => car.State != CarState.Parked;

        private bool IsReachable(CarModel car)
        {
            if (car.State != CarState.Parked) return false;
            IReadOnlyList<int> ahead = graph.AheadOf(car.Id);
            for (int i = 0; i < ahead.Count; i++)
                if (IsPendingBlocker(ahead[i])) return false;
            return true;
        }

        private CarModel PickRandom(Func<CarModel, bool> filter)
        {
            candidates.Clear();
            for (int i = 0; i < cars.Count; i++)
                if (!planned[i] && filter(cars[i])) candidates.Add(cars[i]);
            return candidates.Count > 0 ? candidates[random.Next(candidates.Count)] : null;
        }

        private CarModel LeastBlocked()
        {
            CarModel best = null;
            int bestCount = int.MaxValue;
            for (int i = 0; i < cars.Count; i++)
            {
                if (planned[i]) continue;
                int count = 0;
                IReadOnlyList<int> ahead = graph.AheadOf(i);
                for (int k = 0; k < ahead.Count; k++)
                    if (IsPendingBlocker(ahead[k])) count++;
                if (count < bestCount) { bestCount = count; best = cars[i]; }
            }
            return best;
        }

        #endregion
    }
}
