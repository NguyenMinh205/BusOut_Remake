using System;
using System.Collections.Generic;

namespace BusOut.Gameplay
{
    /// Lên kế hoạch màu hành khách theo xe để level luôn giải được (bản đơn giản của thuật toán "puzzle" gốc):
    /// mỗi lần chọn một xe chắc chắn lấy ra được, sinh đủ số ghế của nó theo màu của nó.
    /// Tổng khách mỗi màu = tổng ghế xe màu đó.
    public sealed class PassengerColorPlanner
    {
        private readonly IReadOnlyList<CarModel> cars;
        private readonly BlockingGraph graph;
        private readonly Random random;
        private readonly bool[] planned;
        private readonly List<CarModel> candidates = new List<CarModel>();
        private int unplannedCount;

        public bool HasMore => unplannedCount > 0;

        /// Lần chọn gần nhất phải chọn bừa (không còn xe chắc chắn lấy ra được): Presenter nên cảnh báo.
        public bool LastPickWasForced { get; private set; }

        /// Số khách chưa lên kế hoạch (cho biển đếm "QUEUE": cộng thêm số khách đang chờ trong hàng).
        public int UnplannedSeats { get; private set; }

        public PassengerColorPlanner(IReadOnlyList<CarModel> cars, BlockingGraph graph, Random random)
        {
            this.cars = cars;
            this.graph = graph;
            this.random = random;
            planned = new bool[cars.Count];
            unplannedCount = cars.Count;
            foreach (CarModel car in cars) UnplannedSeats += car.SeatCount;
        }

        /// Chọn xe tiếp theo, thêm màu khách của nó vào <paramref name="colors"/>. False khi đã hết xe.
        /// Ưu tiên 1: xe đã rời bãi (đang chạy lên / đang đỗ ở bến / đã đi hẳn) mà chưa có kế hoạch — để lâu là chiếm ô.
        ///   "Đã đi hẳn mà chưa có kế hoạch" xảy ra khi người chơi lấy xe C cùng màu với xe P đã có kế hoạch: khách của P
        ///   lên C và C đi luôn. Khách tính theo TỔNG GHẾ TỪNG MÀU nên phần ghế của C giờ thành khách cho P — vẫn đúng.
        /// Ưu tiên 2: xe trong bãi mà mọi xe chặn phía trước đều đã có kế hoạch hoặc đã rời bãi. Xe rời bãi không
        /// bao giờ quay lại nên xe đã "thoát được" thì mãi thoát được → thứ tự này luôn giải được.
        public bool TryPlanNext(List<int> colors)
        {
            if (!HasMore) return false;

            CarModel pick = PickRandom(HasLeftLot);
            if (pick == null) pick = PickRandom(IsReachable);
            // ⚠️ Không còn xe nào chắc chắn lấy ra được: level có xe chặn vòng nhau (vd. 2 xe đâm đầu vào nhau).
            // Lấy xe ít bị chặn nhất để không đứng hình; level này có thể không giải được.
            LastPickWasForced = pick == null;
            if (pick == null) pick = LeastBlocked();
            if (pick == null)
            {
                // Không còn xe nào để chọn dù đếm còn (danh sách xe đã bị thay ngoài ý muốn): dừng hẳn thay vì lỗi null.
                unplannedCount = 0;
                UnplannedSeats = 0;
                return false;
            }

            planned[pick.Id] = true;
            unplannedCount--;
            UnplannedSeats -= pick.SeatCount;
            for (int i = 0; i < pick.SeatCount; i++) colors.Add(pick.ColorIndex);
            return true;
        }

        private static bool HasLeftLot(CarModel car) => car.State != CarState.Parked;

        private bool IsReachable(CarModel car)
        {
            if (car.State != CarState.Parked) return false;
            foreach (int blockerId in graph.AheadOf(car.Id))
                if (!planned[blockerId] && cars[blockerId].State == CarState.Parked) return false;
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
                foreach (int blockerId in graph.AheadOf(i))
                    if (!planned[blockerId] && cars[blockerId].State == CarState.Parked) count++;
                if (count < bestCount) { bestCount = count; best = cars[i]; }
            }
            return best;
        }
    }
}
