using System.Collections.Generic;

namespace BusOut.Gameplay
{
    /// Vòng lên xe (như RunProcessBusStopOperationAsync của game gốc): người đầu hàng tìm xe cùng màu đã đỗ ở bến,
    /// còn ghế → rời hàng đi tới xe. Xe đủ khách → chờ một chút rồi rời bến.
    /// GameplayPresenter tạo, gọi ResetForLevel khi vào level và Tick mỗi frame.
    public sealed class BoardingPresenter
    {
        private struct PendingLeave
        {
            public int CarId;
            public float Remaining;
        }

        private readonly GameplayConfig config;
        private readonly LevelSpawner spawner;
        private readonly BusStopPresenter busStop;
        private readonly PassengerPresenter passengers;
        private readonly CarFlowPresenter carFlow;

        // Khách đang đi tới xe nào (để tới nơi biết cộng ghế cho xe nào).
        private readonly Dictionary<PassengerView, int> passengerTargets = new Dictionary<PassengerView, int>();
        private readonly List<PendingLeave> pendingLeaves = new List<PendingLeave>();
        private float cooldown;

        public BoardingPresenter(GameplayConfig config, LevelSpawner spawner, BusStopPresenter busStop,
            PassengerPresenter passengers, CarFlowPresenter carFlow)
        {
            this.config = config;
            this.spawner = spawner;
            this.busStop = busStop;
            this.passengers = passengers;
            this.carFlow = carFlow;
            passengers.PassengerReachedCar += HandlePassengerReachedCar;
        }

        public void ResetForLevel()
        {
            passengerTargets.Clear();
            pendingLeaves.Clear();
            cooldown = 0f;
        }

        public void Tick(float deltaTime)
        {
            TickPendingLeaves(deltaTime);

            cooldown -= deltaTime;
            if (cooldown > 0f) return;
            if (TryBoardHead()) cooldown = config.boardingInterval;
        }

        /// Một khách mỗi lần (gốc: 0.1s một người). True nếu đã cho một khách lên.
        private bool TryBoardHead()
        {
            PassengerModel head = passengers.Queue.Head;
            PassengerView headView = passengers.HeadView;
            // Đợi người đầu đứng vào chỗ: lúc mới vào level họ còn đang đi từ SpawnPoint tới, rời hàng giữa chừng
            // thì đi chéo cắt ngang cả hàng.
            if (head == null || headView == null || headView.IsWalking) return false;

            CarModel car = FindCarFor(head.ColorIndex);
            CarView carView = car != null ? spawner.GetView(car.Id) : null;
            if (carView == null) return false;

            car.ReserveSeat();
            PassengerView view = passengers.RemoveHead(out _);
            passengerTargets[view] = car.Id;
            passengers.SendToCar(view, carView.transform.position);
            return true;
        }

        /// Ô đầu tiên (trái → phải) có xe đã đỗ, cùng màu, còn ghế chưa ai nhận.
        private CarModel FindCarFor(int colorIndex)
        {
            IReadOnlyList<BusStopSlot> slots = busStop.Slots;
            IReadOnlyList<CarModel> cars = spawner.Cars;
            for (int i = 0; i < slots.Count; i++)
            {
                BusStopSlot slot = slots[i];
                if (slot.Occupancy != SlotOccupancy.Occupied) continue;
                CarModel car = cars[slot.CarId];
                if (car.State == CarState.InSlot && car.ColorIndex == colorIndex && car.HasFreeSeat) return car;
            }
            return null;
        }

        private void HandlePassengerReachedCar(PassengerView view)
        {
            if (!passengerTargets.TryGetValue(view, out int carId)) return;
            passengerTargets.Remove(view);

            CarModel car = spawner.Cars[carId];
            car.BoardSeat();
            // Đủ khách: chờ một chút cho người cuối "ngồi xuống" rồi mới chạy (gốc: 0.5s).
            if (car.IsFull) pendingLeaves.Add(new PendingLeave { CarId = carId, Remaining = config.carLeaveDelay });
        }

        private void TickPendingLeaves(float deltaTime)
        {
            for (int i = pendingLeaves.Count - 1; i >= 0; i--)
            {
                PendingLeave leave = pendingLeaves[i];
                leave.Remaining -= deltaTime;
                if (leave.Remaining > 0f)
                {
                    pendingLeaves[i] = leave;
                    continue;
                }
                pendingLeaves.RemoveAt(i);
                carFlow.LeaveBusStop(spawner.Cars[leave.CarId]);
            }
        }
    }
}
