using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BusOut.Gameplay
{
    /// Điều khiển xe sau khi chạm: bị chặn thì đâm và lùi, đường trống thì giữ ô và chạy lên bến, ở bến thì rời đi.
    public sealed class CarFlowPresenter
    {
        private readonly GameplayConfig config;
        private readonly ParkingLotView parkingLot;
        private readonly LevelSpawner spawner;
        private readonly BusStopPresenter busStop;

        private readonly List<Vector3> routeBuffer = new List<Vector3>(16);
        // Dựng sẵn delegate một lần: truyền method group mỗi lần chạm là cấp phát delegate mới.
        private readonly Action<CarView> onArrivedSlot;
        private readonly Action<CarView> onLeft;
        private readonly Action<CarView> onReachedTopLane;

        public CarFlowPresenter(GameplayConfig config, ParkingLotView parkingLot, LevelSpawner spawner, BusStopPresenter busStop)
        {
            this.config = config;
            this.parkingLot = parkingLot;
            this.spawner = spawner;
            this.busStop = busStop;
            onArrivedSlot = HandleArrivedSlot;
            onLeft = HandleLeft;
            onReachedTopLane = HandleReachedTopLane;
        }

        public void HandleCarTapped(CarView view)
        {
            if (!spawner.TryGetCar(view, out CarModel car)) return;
            switch (car.State)
            {
                case CarState.Parked:
                    TryMoveOut(car, view);
                    break;
                case CarState.InSlot:
                    // ⚠️ TẠM: chưa có hành khách nên xe không bao giờ đầy. Chạm xe ở bến cho nó rời đi để thử tiếp được.
                    // Bỏ nhánh này khi làm xong khách lên xe (xe đầy tự rời bến).
                    StartLeaving(car, view);
                    break;
            }
        }

        private void TryMoveOut(CarModel car, CarView view)
        {
            if (view.IsBusy) return;

            Transform carTransform = view.transform;
            LaneRing ring = parkingLot.Ring;
            float distanceToLane = ParkingRoute.DistanceToRing(carTransform.position, carTransform.forward, ring);
            if (view.FindBlocker(distanceToLane, config.blockerRayInset, out CarView blocker, out float hitDistance))
            {
                float travel = Mathf.Max(0f, hitDistance - view.HalfLength - config.rammedGap);
                Vector3 hitDirection = carTransform.forward;
                view.PlayRam(travel, config, () => blocker.PlayRammed(hitDirection, config));
                return;
            }

            int slotIndex = busStop.TryReserve(car.Id);
            if (slotIndex < 0)
            {
                view.PlayRejectShake(config);
                return;
            }

            car.StartMoveToSlot(slotIndex);
            // Tắt collider ngay: xe đã rời chỗ không còn chặn ai, và không chạm được khi đang chạy.
            view.SetCollidersEnabled(false, false);

            Vector3 entry = busStop.GetEntryPoint(slotIndex);
            Vector3 park = busStop.GetParkPoint(slotIndex);
            int topLaneIndex = ParkingRoute.BuildToSlot(carTransform.position, carTransform.forward, ring, entry, park, routeBuffer);
            Vector3 parkDirection = park - entry;
            parkDirection.y = 0f;
            view.Drive(routeBuffer, config.carMoveSpeed, config.carTurnSharpness,
                Quaternion.LookRotation(parkDirection), onArrivedSlot, 0, topLaneIndex, onReachedTopLane);
        }

        // Ra tới làn trên là xe đã thoát khỏi bãi: mũi tên chỉ hướng hết ý nghĩa nên ẩn đi.
        private static void HandleReachedTopLane(CarView view) => view.SetMarkVisible(false);

        private void HandleArrivedSlot(CarView view)
        {
            if (!spawner.TryGetCar(view, out CarModel car)) return;
            car.ArriveSlot();
            busStop.NotifyCarArrived(car.SlotIndex);
            // Bật lại vùng chạm cho nhánh TẠM "chạm xe ở bến để rời đi"; thân xe vẫn tắt vì bến nằm ngoài bãi.
            view.SetCollidersEnabled(false, true);
        }

        private void StartLeaving(CarModel car, CarView view)
        {
            int slotIndex = car.SlotIndex;
            busStop.Release(slotIndex);
            car.StartLeaving();
            view.SetCollidersEnabled(false, false);
            // Random trái/phải cho đa dạng (game gốc chỉ rời sang phải).
            Vector3 leavingPoint = Random.value < 0.5f ? parkingLot.LeftLeavingPoint : parkingLot.RightLeavingPoint;
            int reverseCount = ParkingRoute.BuildLeave(view.transform.position, busStop.GetEntryPoint(slotIndex),
                leavingPoint, routeBuffer);
            view.Drive(routeBuffer, config.carMoveSpeed, config.carTurnSharpness, null, onLeft, reverseCount);
        }

        private void HandleLeft(CarView view) => spawner.Despawn(view);
    }
}
