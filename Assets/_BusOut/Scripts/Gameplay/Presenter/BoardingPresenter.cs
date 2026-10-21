using System.Collections.Generic;
using BusOut.DesignPattern;

namespace BusOut.Gameplay
{
    /// Vòng lên xe: người đầu hàng tìm xe cùng màu đã đỗ ở bến, còn ghế → rời hàng đi tới xe. Xe đủ khách → chờ một chút rồi
    /// rời bến. GameplayPresenter tạo, gọi ResetForLevel khi vào level và Tick mỗi frame.
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

        /// Kẹt hẳn = thua: mọi ô mở đều có xe đứng, người đầu hàng đứng yên mà không xe nào ở bến cùng màu
        /// còn ghế, và không còn gì đang chạy có thể gỡ thế kẹt (xe chạy lên / rời bến, khách đi tới xe, xe chờ rời bến).
        /// ⚠️ Thiếu một điều kiện "đang chạy" là báo thua oan: vd. bỏ pendingLeaves thì xe vừa đủ khách (0.5s trước khi
        /// rời bến, ô sắp trống) cũng bị tính là kẹt.
        public bool IsStuck()
        {
            if (busStop.FreeCount > 0) return false;
            if (spawner.HasMovingCar || passengerTargets.Count > 0 || pendingLeaves.Count > 0) return false;
            PassengerModel head = passengers.Queue.Head;
            PassengerView headView = passengers.HeadView;
            // Hàng rỗng hoặc người đầu còn đang đi vào chỗ: chưa kết luận được.
            if (head == null || headView == null || headView.IsWalking) return false;
            return FindCarFor(head.ColorIndex) == null;
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
            CarView carView = null;
            if (passengerTargets.TryGetValue(view, out int carId))
            {
                passengerTargets.Remove(view);
                carView = spawner.GetView(carId);
            }
            if (carView == null)
            {
                // Không còn xe để ngồi (không xảy ra trong luồng thường): trả khách về pool, không để khách đứng giữa đường.
                PoolingManager.Despawn(view);
                return;
            }

            CarModel car = spawner.Cars[carId];
            car.BoardSeat();
            // Ghế theo thứ tự khách tới: khách thứ n ngồi ghế n. Xe xoá thì CarView trả khách về pool.
            carView.SeatPassenger(view.transform, car.BoardedSeats - 1, config.seatedPassengerScale);
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
