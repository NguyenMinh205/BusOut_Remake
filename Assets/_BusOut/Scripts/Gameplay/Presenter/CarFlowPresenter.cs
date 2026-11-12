using System;
using System.Collections.Generic;
using BusOut.Core;
using BusOut.DesignPattern;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BusOut.Gameplay
{
    /// Điều khiển xe: chạm xe trong bãi → bị chặn thì đâm và lùi, đường trống thì giữ ô và chạy lên bến;
    /// xe đủ khách → BoardingPresenter gọi LeaveBusStop để xe rời bến.
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

        /// Chạm xe đường trống nhưng hết ô (để hiện chữ "Hết chỗ!").
        public event Action NoSlotRejected;

        // Booster Undo: xe gửi lên bến gần nhất + chỗ đỗ của nó trong bãi lúc chạm (-1 = không có gì để Undo).
        private int lastSentCarId = -1;
        private Vector3 lastSentPosition;
        private Quaternion lastSentRotation;
        private int rewindingCarId = -1;
        private readonly Action<CarView> onRewound;
        private readonly FxConfig fx;
        private readonly CraneEffects craneEffects;
        private readonly bool hasRammedEffect;

        /// Có xe đang tua ngược về bãi (Undo). ⚠️ Lúc này collider xe đó tắt: chạm xe khác là xe kia tưởng đường trống,
        /// chạy xuyên qua xe đang lùi về → GameplayPresenter khoá chạm xe và booster tới khi xong.
        public bool IsRewinding => rewindingCarId >= 0;

        public CarFlowPresenter(GameplayConfig config, ParkingLotView parkingLot, LevelSpawner spawner, BusStopPresenter busStop)
        {
            this.config = config;
            this.parkingLot = parkingLot;
            this.spawner = spawner;
            this.busStop = busStop;
            onArrivedSlot = HandleArrivedSlot;
            onLeft = HandleLeft;
            onReachedTopLane = HandleReachedTopLane;
            onRewound = HandleRewound;
            fx = FxConfig.Instance;
            hasRammedEffect = fx.rammedEffectPrefab != null;
            craneEffects = new CraneEffects
            {
                ShadowPrefab = fx.craneShadowPrefab,
                ShadowGroundOffset = fx.craneShadowGroundOffset,
                ShadowMinScale = fx.craneShadowMinScale,
                ShadowMinAlpha = fx.craneShadowMinAlpha,
                DustPrefab = fx.craneDustPrefab,
                DustScale = fx.craneDustScale,
            };
            if (fx.craneShadowPrefab == null)
                Debug.LogError("[BusOut] FxConfig thiếu Crane Shadow Prefab: xe bay bằng cần cẩu không có bóng đổ. Kéo " +
                               "Prefab/VFX/CraneShadow vào Resources/FxConfig.", fx);
            if (!hasRammedEffect)
                Debug.LogError("[BusOut] FxConfig thiếu Rammed Effect Prefab: xe đâm xe chặn không có hiệu ứng va chạm. " +
                               "Kéo prefab hiệu ứng va chạm vào Resources/FxConfig › Rammed Effect Prefab.", fx);
        }

        /// Vào level mới: quên xe đã gửi (Undo không được trả xe của ván trước).
        public void ResetForLevel()
        {
            lastSentCarId = -1;
            rewindingCarId = -1;
        }

        /// Booster Crane: nhấc xe trong bãi (kể cả đang bị chặn) bay thẳng vào ô trống đầu tiên. False = không làm gì:
        /// xe không còn trong bãi / đang bận, hoặc hết ô (báo "Hết chỗ!").
        /// Vẫn luôn giải được: tương đương lấy sớm một xe ra; bộ lên kế hoạch ưu tiên xe đã rời bãi.
        public bool TryCraneLift(CarView view)
        {
            if (!spawner.TryGetCar(view, out CarModel car) || car.State != CarState.Parked || view.IsBusy) return false;
            int slotIndex = busStop.TryReserve(car.Id);
            if (slotIndex < 0)
            {
                NoSlotRejected?.Invoke();
                return false;
            }
            // Nhấc xe còn ẩn màu: lộ ngay, không nảy (tween cỡ của cú nhấc chạy ngay sau).
            spawner.RevealColor(car, false);
            car.StartMoveToSlot(slotIndex);
            view.SetCollidersEnabled(false, false);
            view.SetMarkVisible(false);
            // ⚠️ Xe bay không chạy theo đường → Undo không có đường để tua ngược: không cho Undo xe này.
            lastSentCarId = -1;
            Vector3 entry = busStop.GetEntryPoint(slotIndex);
            Vector3 park = busStop.GetParkPoint(slotIndex);
            Vector3 parkDirection = park - entry;
            parkDirection.y = 0f;
            // Nhấc lên là ra khỏi bãi: to dần về cỡ ngoài bãi trong lúc bay.
            view.SetOutsideLot(true);
            view.SetMoveScale(false, fx.carMoveScale, fx.carLeaveLotScaleDuration);
            view.PlayCraneLift(park, Quaternion.LookRotation(parkDirection), config.craneLiftHeight,
                config.craneRiseDuration, config.craneFlyDuration, config.craneDropDuration, craneEffects, onArrivedSlot);
            AudioService.Play(SoundId.Helicopter);
            return true;
        }

        /// Booster Undo: đưa xe gửi lên bến GẦN NHẤT về đúng chỗ cũ trong bãi. Chỉ được khi chưa có khách nào nhận ghế xe
        /// đó (đang chạy tới bến hoặc đã đỗ mà chưa ai lên). False = không làm gì (không mất lượt).
        /// Vẫn luôn giải được: lúc chạm, đường ra của xe đã trống; xe khác chỉ rời bãi chứ không chen vào đường đó.
        public bool TryUndoLast()
        {
            if (lastSentCarId < 0 || IsRewinding) return false;
            CarModel car = spawner.Cars[lastSentCarId];
            CarView view = spawner.GetView(lastSentCarId);
            if (view == null || car.ReservedSeats > 0) return false;
            // ⚠️ Bãi chờ: xe kế sẽ lăn ra đúng chỗ đỗ cũ, tua về là hai xe chồng một chỗ. Không cho Undo, không mất lượt.
            if (spawner.HasGarageSuccessor(car)) return false;
            if (car.State != CarState.MovingToSlot && car.State != CarState.InSlot) return false;

            busStop.Release(car.SlotIndex);
            car.ReturnToParking();
            view.SetRoofOpen(false);
            rewindingCarId = car.Id;
            lastSentCarId = -1;
            view.Rewind(lastSentPosition, lastSentRotation, config.undoRewindSpeed, onRewound);
            // Về bãi: thu về cỡ trong bãi ngay (đang chạy dở thì còn 1.1, đã ra khỏi bãi thì còn cỡ ngoài bãi); khói theo
            // suốt đường lùi.
            view.SetOutsideLot(false);
            view.SetMoveScale(false, fx.carMoveScale, fx.carMoveScaleDuration);
            view.SetSmoke(true);
            return true;
        }

        private void HandleRewound(CarView view)
        {
            rewindingCarId = -1;
            view.SetSmoke(false);
            // Về đúng chỗ cũ: nảy một cái cho thấy "đã về" (không thì xe lùi về rồi đứng khựng).
            view.SettleWithBounce(fx.carMoveScaleDuration, fx.undoArrivePunch, fx.undoArrivePunchDuration);
            // Về chỗ: chặn xe khác và chạm được lại như trước khi gửi đi.
            view.SetCollidersEnabled(true, true);
            view.SetMarkVisible(true);
        }

        public void HandleCarTapped(CarView view)
        {
            // Chỉ xe còn trong bãi mới chạm được; xe ở bến tự rời đi khi đủ khách.
            if (spawner.TryGetCar(view, out CarModel car) && car.State == CarState.Parked) TryMoveOut(car, view);
        }

        private void TryMoveOut(CarModel car, CarView view)
        {
            if (view.IsBusy) return;
            if (spawner.IsLockedNow(car))
            {
                view.PlayLockedReject(config);
                HapticService.Play(HapticType.Impact);
                return;
            }

            Transform carTransform = view.transform;
            LaneRing ring = parkingLot.Ring;
            float distanceToLane = ParkingRoute.DistanceToRing(carTransform.position, carTransform.forward, ring);
            if (view.FindBlocker(distanceToLane, config.blockerRayInset, out CarView blocker, out float hitDistance))
            {
                float travel = Mathf.Max(0f, hitDistance - view.HalfLength - config.rammedGap);
                Vector3 hitDirection = carTransform.forward;
                view.PlayRam(travel, config, () =>
                {
                    // Tiếng / rung / hiệu ứng lúc chạm vào xe chặn, không phải lúc bấm.
                    AudioService.Play(SoundId.HitCar);
                    HapticService.Play(HapticType.Impact);
                    SpawnRammedEffect(view, hitDirection);
                    // Ai nghe thì rung (CameraShakeView trên camera gameplay); cường độ 0 = không rung.
                    if (fx.ramShakeStrength > 0f && fx.ramShakeDuration > 0f)
                        EventBus.Publish(new CameraShakeEvent { Duration = fx.ramShakeDuration, Intensity = fx.ramShakeStrength });
                    blocker.PlayRammed(hitDirection, config);
                });
                return;
            }

            int slotIndex = busStop.TryReserve(car.Id);
            if (slotIndex < 0)
            {
                view.PlayRejectShake(config);
                NoSlotRejected?.Invoke();
                return;
            }

            lastSentCarId = car.Id;
            lastSentPosition = carTransform.position;
            lastSentRotation = carTransform.rotation;
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
            view.SetMoveScale(true, fx.carMoveScale, fx.carMoveScaleDuration);
            view.SetSmoke(true);
            AudioService.Play(SoundId.CarMoveToBusStop);
        }

        /// Hiệu ứng va chạm ở đầu xe đâm (lúc chạm), lấy từ pool; prefab tự trả về pool (AutoDespawn).
        private void SpawnRammedEffect(CarView rammer, Vector3 hitDirection)
        {
            if (!hasRammedEffect) return;
            hitDirection.y = 0f;
            Vector3 point = rammer.FrontPoint + hitDirection.normalized * fx.rammedEffectForwardOffset;
            GameObject prefab = fx.rammedEffectPrefab;
            PoolingManager.Spawn(prefab, point, prefab.transform.rotation);
        }

        // Ra tới làn trên là xe đã thoát khỏi bãi: mũi tên chỉ hướng hết ý nghĩa nên ẩn đi; to lên cỡ ngoài bãi (vẫn giữ
        // phóng 1.1 vì đang chạy). ⚠️ Không to lúc vừa chạm: xe đang giữa đám xe xếp sát, phình ra là đè lên xe bên cạnh.
        private void HandleReachedTopLane(CarView view)
        {
            view.SetMarkVisible(false);
            view.SetOutsideLot(true);
            view.SetMoveScale(true, fx.carMoveScale, fx.carLeaveLotScaleDuration);
        }

        private void HandleArrivedSlot(CarView view)
        {
            if (!spawner.TryGetCar(view, out CarModel car)) return;
            car.ArriveSlot();
            busStop.NotifyCarArrived(car.SlotIndex);
            busStop.PlaySlotRing(car.SlotIndex);
            // Mở nóc lộ ghế để khách lên.
            view.SetRoofOpen(true);
            // Thu về cỡ đỗ rồi nảy nhẹ: điểm nhấn lúc xe vào ô (cả xe chạy tới lẫn cần cẩu hạ xuống).
            view.SettleWithBounce(fx.carMoveScaleDuration, fx.slotArrivePunch, fx.slotArrivePunchDuration);
            view.SetSmoke(false);
        }

        /// Xe đủ khách rời bến: lùi ra theo trục ô rồi chạy sang trái/phải, tới nơi thì biến mất.
        public void LeaveBusStop(CarModel car)
        {
            CarView view = spawner.GetView(car.Id);
            if (view == null || car.State != CarState.InSlot) return;

            int slotIndex = car.SlotIndex;
            busStop.Release(slotIndex);
            car.StartLeaving();
            view.SetCollidersEnabled(false, false);
            // Đủ khách: đóng nóc rồi chạy như xe bình thường (khách ngồi ẩn theo ghế).
            view.SetRoofOpen(false);
            // Random trái/phải cho đa dạng (game gốc chỉ rời sang phải).
            Vector3 leavingPoint = Random.value < 0.5f ? parkingLot.LeftLeavingPoint : parkingLot.RightLeavingPoint;
            int reverseCount = ParkingRoute.BuildLeave(view.transform.position, busStop.GetEntryPoint(slotIndex),
                leavingPoint, routeBuffer);
            view.Drive(routeBuffer, config.carMoveSpeed, config.carTurnSharpness, null, onLeft, reverseCount);
            view.SetSmoke(true);
            AudioService.Play(SoundId.CarLeaveBusStop);
        }

        private void HandleLeft(CarView view) => spawner.Despawn(view);
    }
}
