using System;
using System.Collections.Generic;
using BusOut.Core;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Tác dụng của booster trong ván. Chỉ trừ lượt khi dùng THÀNH CÔNG; không dùng được (sai thời điểm, không có gì
    /// để làm, hết lượt) thì trả false để nút rung và người chơi không mất lượt.
    /// - Sort: giải phóng bến + dọn sẵn 1 nước tiếp theo. Từng xe ở bến (trái → phải) một khối khách đủ ghế → cả bến đầy
    ///   và rời đi; ngay sau là một khối đủ ghế cho 1 xe trong bãi chạm được ngay. Có thứ tự, đoán trước được.
    /// - Shuffle: xáo lại hàng cho nhiều nước đi. Pha 1: khách các xe ở bến chia nhóm 2–4, trộn ngẫu nhiên. Pha 2 (sau khi
    ///   bến trống): khách của MỌI xe chạm được ngay, trộn từng cặp xe. Mỗi lần bấm ra một kiểu khác.
    ///   ⚠️ Không trộn khách xe trong bãi lẫn vào pha 1: bến còn đầy mà khách xe ngoài bãi lên đầu hàng là kẹt.
    /// - Crane: bấm → "giương cần" (chưa trừ lượt), chạm một xe bất kỳ trong bãi → xe bay vào ô trống, lúc đó mới trừ
    ///   lượt. Bấm lại nút / dùng booster khác / ván kết thúc → huỷ, không mất lượt.
    /// - Undo: xe gửi lên bến gần nhất (chưa ai nhận ghế) tua ngược về chỗ cũ trong bãi (CarFlowPresenter.TryUndoLast).
    /// Sort / Shuffle chỉ đổi THỨ TỰ màu trong hàng (QueueReorder), số khách mỗi màu giữ nguyên; khách của xe bị chặn giữ thứ tự cũ.
    /// GameplayPresenter tạo; HudPresenter gọi TryUse khi bấm nút booster.
    public sealed class BoosterPresenter
    {
        private const int ShuffleAttempts = 3;

        private readonly GameplayConfig config;
        private readonly SecureGameData gameData;
        private readonly LevelSpawner spawner;
        private readonly BusStopPresenter busStop;
        private readonly PassengerPresenter passengers;
        private readonly CarFlowPresenter carFlow;
        private readonly Func<bool> canUse;
        private readonly System.Random random = new System.Random();

        private readonly List<ColorDemand> demands = new List<ColorDemand>(32);
        private readonly List<ColorDemand> carDemands = new List<ColorDemand>(16);
        private readonly List<ColorDemand> pairBuffer = new List<ColorDemand>(2);
        private readonly List<ColorDemand> groupsBuffer = new List<ColorDemand>(32);
        private readonly List<int> ownerBuffer = new List<int>(32);
        private readonly List<int> colors = new List<int>(64);
        private readonly List<int> reordered = new List<int>(64);
        private readonly List<bool> taken = new List<bool>(64);
        private readonly List<CarModel> freeCars = new List<CarModel>(16);
        private readonly Dictionary<int, int> firstIndexOfColor = new Dictionary<int, int>(8);

        /// Crane đang chờ chọn xe: chạm xe tiếp theo là nhấc xe đó (GameplayPresenter chuyển cú chạm sang TryCranePick).
        public bool IsCraneArmed { get; private set; }

        /// Booster đổi trạng thái "giương" (để HUD phóng to / thu nút).
        public event Action<BoosterType, bool> ArmedChanged;

        /// <paramref name="canUse"/>: lúc này có được dùng booster không (đang chơi, không trong hoạt ảnh vào level).
        public BoosterPresenter(GameplayConfig config, SecureGameData gameData, LevelSpawner spawner,
            BusStopPresenter busStop, PassengerPresenter passengers, CarFlowPresenter carFlow, Func<bool> canUse)
        {
            this.config = config;
            this.gameData = gameData;
            this.spawner = spawner;
            this.busStop = busStop;
            this.passengers = passengers;
            this.carFlow = carFlow;
            this.canUse = canUse;
        }

        /// True khi đã dùng (đã trừ 1 lượt và lưu). False: không dùng được — không mất lượt.
        public bool TryUse(BoosterType type)
        {
            if (!canUse()) return false;
            // Bấm lại Crane khi đang giương: huỷ (không mất lượt) — không tính là "không dùng được" nên nút không rung.
            if (type == BoosterType.Crane && IsCraneArmed)
            {
                Disarm();
                return true;
            }
            // Dùng booster khác trong lúc Crane đang giương: bỏ giương trước.
            Disarm();
            if (gameData.GetBoosterCount(type) <= 0)
            {
                // HudPresenter mở popup mua khi hết lượt nên không gọi vào đây; tới được đây là nơi gọi quên kiểm lượt.
                GameLog.Info($"[BusOut] Booster {type}: hết lượt (nơi gọi phải mở popup mua thay vì dùng).");
                return false;
            }
            bool used;
            switch (type)
            {
                case BoosterType.Sort:
                    used = Sort();
                    break;
                case BoosterType.Shuffle:
                    used = Shuffle();
                    break;
                case BoosterType.Undo:
                    used = carFlow.TryUndoLast();
                    if (used) GameLog.Info("[BusOut] Booster Undo: xe gửi gần nhất quay về chỗ cũ.");
                    break;
                case BoosterType.Crane:
                    // Chưa trừ lượt: chỉ giương. Hết ô thì giương cũng vô ích → không cho.
                    if (busStop.FreeCount <= 0) return false;
                    IsCraneArmed = true;
                    ArmedChanged?.Invoke(BoosterType.Crane, true);
                    return true;
                default:
                    Debug.LogError($"[BusOut] Booster {type} chưa có tác dụng.");
                    used = false;
                    break;
            }
            if (!used) return false;
            gameData.TryUseBooster(type);
            gameData.Save();
            return true;
        }

        /// Đang giương Crane mà người chơi chạm một xe: nhấc xe đó. Thành công thì trừ lượt; xe không nhấc được (đang ở bến,
        /// đang bận) thì vẫn giương chờ xe khác; hết ô thì huỷ giương (đã báo "Hết chỗ!"), không mất lượt.
        public void TryCranePick(CarView view)
        {
            if (!IsCraneArmed) return;
            if (!canUse())
            {
                Disarm();
                return;
            }
            if (!spawner.TryGetCar(view, out CarModel car) || car.State != CarState.Parked || view.IsBusy) return;
            bool lifted = carFlow.TryCraneLift(view);
            Disarm();
            if (!lifted) return;
            gameData.TryUseBooster(BoosterType.Crane);
            gameData.Save();
            GameLog.Info("[BusOut] Booster Crane: nhấc xe lên bến.");
        }

        /// Huỷ giương Crane (không mất lượt). Gọi khi vào level / ván kết thúc.
        public void Disarm()
        {
            if (!IsCraneArmed) return;
            IsCraneArmed = false;
            ArmedChanged?.Invoke(BoosterType.Crane, false);
        }

        private bool Sort()
        {
            passengers.CopyQueueColors(colors);
            demands.Clear();
            CollectBusStopDemands(demands);
            CollectFreeCars();
            // Nước tiếp theo: xe chạm được có màu đứng sớm nhất trong hàng (đổi ít nhất so với thứ tự cũ).
            if (freeCars.Count > 0)
            {
                freeCars.Sort(CompareByFirstColor);
                demands.Add(new ColorDemand(freeCars[0].ColorIndex, freeCars[0].SeatCount));
            }
            return Apply(BoosterType.Sort);
        }

        private bool Shuffle()
        {
            passengers.CopyQueueColors(colors);
            CollectFreeCars();
            for (int attempt = 0; attempt < ShuffleAttempts; attempt++)
            {
                demands.Clear();
                // Pha 1: xe đã ở bến — trộn nhóm giữa chúng thoải mái (đều đang đỗ sẵn).
                carDemands.Clear();
                CollectBusStopDemands(carDemands);
                QueueReorder.AppendInterleaved(carDemands, random, config.passengerGroupMin, config.passengerGroupMax,
                    demands, groupsBuffer, ownerBuffer);
                // Pha 2: xe chạm được, thứ tự ngẫu nhiên, trộn từng cặp (2 xe cùng lúc là vừa sức 4 ô).
                ShuffleList(freeCars);
                for (int i = 0; i < freeCars.Count; i += 2)
                {
                    pairBuffer.Clear();
                    pairBuffer.Add(new ColorDemand(freeCars[i].ColorIndex, freeCars[i].SeatCount));
                    if (i + 1 < freeCars.Count)
                        pairBuffer.Add(new ColorDemand(freeCars[i + 1].ColorIndex, freeCars[i + 1].SeatCount));
                    QueueReorder.AppendInterleaved(pairBuffer, random, config.passengerGroupMin, config.passengerGroupMax,
                        demands, groupsBuffer, ownerBuffer);
                }
                // Xáo ra y như cũ (hiếm, khi chỉ có 1 xe): thử lại cách xáo khác.
                if (Apply(BoosterType.Shuffle)) return true;
            }
            return false;
        }

        /// Xe đang ở bến hoặc đang chạy tới bến, còn ghế chưa ai nhận: cần thêm bấy nhiêu khách màu đó (trái → phải).
        private void CollectBusStopDemands(List<ColorDemand> into)
        {
            IReadOnlyList<BusStopSlot> slots = busStop.Slots;
            IReadOnlyList<CarModel> cars = spawner.Cars;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Occupancy == SlotOccupancy.Empty) continue;
                CarModel car = cars[slots[i].CarId];
                if (car.State != CarState.InSlot && car.State != CarState.MovingToSlot) continue;
                int freeSeats = car.SeatCount - car.ReservedSeats;
                if (freeSeats > 0) into.Add(new ColorDemand(car.ColorIndex, freeSeats));
            }
        }

        /// Xe trong bãi chạm được ngay (mọi xe chặn phía trước đã rời bãi) và trong hàng có khách màu đó.
        private void CollectFreeCars()
        {
            freeCars.Clear();
            BlockingGraph graph = spawner.Graph;
            if (graph == null) return;
            firstIndexOfColor.Clear();
            for (int i = 0; i < colors.Count; i++)
                if (!firstIndexOfColor.ContainsKey(colors[i])) firstIndexOfColor[colors[i]] = i;
            IReadOnlyList<CarModel> cars = spawner.Cars;
            for (int i = 0; i < cars.Count; i++)
            {
                CarModel car = cars[i];
                if (car.State == CarState.Parked && firstIndexOfColor.ContainsKey(car.ColorIndex) && IsFree(car, graph, cars))
                    freeCars.Add(car);
            }
        }

        private int CompareByFirstColor(CarModel a, CarModel b)
        {
            int order = firstIndexOfColor[a.ColorIndex].CompareTo(firstIndexOfColor[b.ColorIndex]);
            return order != 0 ? order : a.Id.CompareTo(b.Id);
        }

        private static bool IsFree(CarModel car, BlockingGraph graph, IReadOnlyList<CarModel> cars)
        {
            IReadOnlyList<int> ahead = graph.AheadOf(car.Id);
            for (int i = 0; i < ahead.Count; i++)
                if (cars[ahead[i]].State == CarState.Parked) return false;
            return true;
        }

        private void ShuffleList(List<CarModel> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// Không có gì để kéo lên, hoặc thứ tự đã y như vậy → không dùng (không mất lượt).
        private bool Apply(BoosterType type)
        {
            if (demands.Count == 0) return false;
            int pulled = QueueReorder.PullToFront(colors, demands, reordered, taken);
            if (pulled == 0 || QueueReorder.SameOrder(colors, reordered)) return false;
            if (!passengers.ApplyQueueColors(reordered)) return false;
            GameLog.Info($"[BusOut] Booster {type}: sắp lại hàng, {pulled} khách lên đầu.");
            return true;
        }
    }
}
