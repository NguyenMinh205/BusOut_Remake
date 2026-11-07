using System;
using System.Collections.Generic;
using BusOut.Core;
using BusOut.DesignPattern;
using DG.Tweening;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Hàng khách: sinh khách ở SpawnPoint, cho đi vào chỗ trong hàng, dồn hàng khi người đầu rời đi.
    /// GameplayPresenter tạo, gọi ResetForLevel khi vào level và Tick mỗi frame.
    public sealed class PassengerPresenter
    {
        private readonly GameplayConfig config;
        private readonly PassengerLineView lineView;
        private readonly PassengerView prefab;
        private readonly PassengerQueue queue = new PassengerQueue();
        private readonly QueuePath path;

        // Song song với queue.Line: lineViews[i] là hình của queue.Line[i].
        private readonly List<PassengerView> lineViews = new List<PassengerView>();
        // Khách đã rời hàng, đang đi tới xe: phải dọn khi chơi lại level.
        private readonly List<PassengerView> leavingViews = new List<PassengerView>();
        private readonly List<Vector3> walkBuffer = new List<Vector3>(8);
        private readonly Action<PassengerView> onLeftQueueArrived;

        private readonly List<int> planBuffer = new List<int>(16);
        private readonly List<int> changedBuffer = new List<int>(32);
        // Cột sáng quét dọc hàng khi Sort / Shuffle (FxConfig.queueSweepPrefab).
        private readonly FxConfig fx;
        private readonly bool hasSweep;
        private readonly TweenCallback<float> moveSweep;
        private Transform sweepTransform;
        private Tween sweepTween;
        private PassengerColorPlanner planner;
        private float spacing;
        private float joinCooldown;

        public PassengerQueue Queue => queue;

        /// Khách còn phải chở trong level: đang đứng + đã lên kế hoạch + chưa lên kế hoạch.
        public int RemainingCount => queue.RemainingCount + (planner != null ? planner.UnplannedSeats : 0);

        /// Hình của người đầu hàng (null khi hàng rỗng).
        public PassengerView HeadView => lineViews.Count > 0 ? lineViews[0] : null;

        /// Khách rời hàng đã tới xe. Từ lúc này khách thuộc về xe (BoardingPresenter gắn vào ghế); PassengerPresenter
        /// không còn giữ hay dọn khách đó nữa.
        public event Action<PassengerView> PassengerReachedCar;

        public PassengerPresenter(GameplayConfig config, PassengerLineView lineView, PassengerView prefab)
        {
            this.config = config;
            this.lineView = lineView;
            this.prefab = prefab;
            path = new QueuePath(lineView.QueuePositions);
            onLeftQueueArrived = HandleLeftQueueArrived;
            fx = FxConfig.Instance;
            hasSweep = fx.queueSweepPrefab != null;
            moveSweep = MoveSweep;
            if (!hasSweep)
                Debug.LogError("[BusOut] FxConfig thiếu Queue Sweep Prefab: Sort / Shuffle không có cột sáng quét hàng (khách vẫn " +
                               "đổi màu lần lượt). Kéo Prefab/VFX/QueueSweep vào Resources/FxConfig.", fx);
        }

        /// Tạo sẵn <paramref name="count"/> khách trong pool (+ cột sáng Sort / Shuffle). Gọi một lần lúc load.
        public void Prewarm(int count)
        {
            PoolingManager.Prewarm(prefab.gameObject, count);
            if (hasSweep) PoolingManager.Prewarm(fx.queueSweepPrefab, 1);
        }

        public void ResetForLevel(PassengerColorPlanner colorPlanner)
        {
            DespawnAll();
            planner = colorPlanner;
            int capacity = Mathf.Max(1, config.passengerQueueCount);
            queue.Reset(capacity);
            spacing = path.SpacingFor(capacity);
            joinCooldown = 0f;
        }

        /// Mỗi frame: còn chỗ ở cuối hàng thì cho thêm khách, cách nhau đủ để khách đi vào không chồng lên nhau.
        public void Tick(float deltaTime)
        {
            RefillPlan();
            joinCooldown -= deltaTime;
            if (joinCooldown > 0f || !queue.CanAddToTail) return;
            SpawnAtTail();
            // Khách sau xuất phát trễ đúng bằng thời gian đi hết một khoảng cách: tới nơi vẫn giữ đúng khoảng cách.
            joinCooldown = spacing / Mathf.Max(0.01f, config.passengerJoinSpeed);
        }

        /// Giữ luôn đủ passengerPlanAheadCount màu đã lên kế hoạch phía sau hàng. Lên kế hoạch dần (không cả level một
        /// lần) để xe người chơi vừa lấy ra được ưu tiên có khách sớm.
        private void RefillPlan()
        {
            if (planner == null) return;
            while (queue.PendingCount < config.passengerPlanAheadCount && planner.HasMore)
            {
                planBuffer.Clear();
                if (!planner.TryPlanNext(planBuffer)) break;
                if (planner.LastPickWasForced)
                    Debug.LogWarning("[BusOut] Không còn xe nào chắc chắn lấy ra được, đã chọn tạm một xe — level có thể có " +
                                     "xe chặn vòng nhau (vd. 2 xe đâm đầu vào nhau) và không giải được.");
                if (planner.LastPickWasHardPoint)
                    GameLog.Info($"[BusOut] Hard point: {planBuffer.Count} khách màu {planBuffer[0]} của một xe đang bị chặn " +
                              $"(đứng sau {queue.PendingCount} khách đã lên kế hoạch).");
                queue.AddPending(planBuffer);
            }
        }

        /// Màu cả hàng theo thứ tự (đang đứng rồi đã lên kế hoạch) — cho booster sắp lại hàng.
        public void CopyQueueColors(List<int> into) => queue.CopyColors(into);

        /// Booster đặt lại thứ tự màu cả hàng (cùng số lượng, cùng tập màu). Khách đang đứng bị đổi màu thì tô lại + nảy.
        public bool ApplyQueueColors(IReadOnlyList<int> colors)
        {
            if (!queue.ApplyColors(colors, changedBuffer)) return false;
            // Cột sáng chạy đều từ đầu tới cuối hàng: khách ở chỗ i đổi màu đúng lúc cột tới chỗ i (Model đã đổi ngay).
            int lastSpot = lineViews.Count - 1;
            float sweepDuration = Mathf.Max(0f, fx.queueSweepDuration);
            for (int i = 0; i < changedBuffer.Count; i++)
            {
                int index = changedBuffer[i];
                float delay = lastSpot > 0 ? sweepDuration * index / lastSpot : 0f;
                lineViews[index].Recolor(config.GetCarColor(queue.Line[index].ColorIndex), config.boosterRecolorPunch,
                    config.boosterRecolorDuration, delay);
            }
            PlaySweep(lastSpot, sweepDuration);
            return true;
        }

        /// Người đầu hàng rời hàng: trả về hình của họ để đưa đi tiếp; cả hàng dồn lên một chỗ.
        public PassengerView RemoveHead(out PassengerModel passenger)
        {
            passenger = queue.RemoveHead();
            if (passenger == null) return null;
            PassengerView head = lineViews[0];
            head.FlushPendingRecolor();
            lineViews.RemoveAt(0);
            for (int i = 0; i < lineViews.Count; i++) WalkToSpot(lineViews[i], i, config.passengerForwardSpeed);
            return head;
        }

        private void PlaySweep(int lastSpot, float duration)
        {
            if (!hasSweep || lastSpot < 0) return;
            if (sweepTween != null && sweepTween.IsActive()) sweepTween.Kill();
            GameObject sweep = PoolingManager.Spawn(fx.queueSweepPrefab, SweepPosition(0f), fx.queueSweepPrefab.transform.rotation);
            if (sweep == null) return;
            sweepTransform = sweep.transform;
            // Prefab tự trả về pool (AutoDespawn) — Lifetime của nó phải ≥ queueSweepDuration, không thì cột biến giữa đường.
            sweepTween = DOVirtual.Float(0f, lastSpot * spacing, duration, moveSweep)
                .SetEase(Ease.Linear)
                .SetLink(sweep);
        }

        private void MoveSweep(float distance)
        {
            if (sweepTransform != null) sweepTransform.position = SweepPosition(distance);
        }

        // Tâm cột cao hơn mặt đường nửa chiều cao cột (đáy sáng chạm đất).
        private Vector3 SweepPosition(float distance) => path.PositionAt(distance) + Vector3.up * fx.queueSweepHeight;

        /// Đưa khách đã rời hàng đi tới xe: qua MoveToBusPoint rồi tới <paramref name="carPosition"/>.
        public void SendToCar(PassengerView view, Vector3 carPosition)
        {
            walkBuffer.Clear();
            walkBuffer.Add(lineView.MoveToBusPoint);
            walkBuffer.Add(new Vector3(carPosition.x, view.transform.position.y, carPosition.z));
            leavingViews.Add(view);
            view.WalkTo(walkBuffer, config.passengerToBusSpeed, config.passengerTurnSharpness, view.transform.rotation,
                onLeftQueueArrived);
        }

        public void DespawnAll()
        {
            foreach (PassengerView view in lineViews) PoolingManager.Despawn(view);
            foreach (PassengerView view in leavingViews) PoolingManager.Despawn(view);
            lineViews.Clear();
            leavingViews.Clear();
        }

        private void SpawnAtTail()
        {
            if (!queue.TryAddToTail(out PassengerModel passenger)) return;
            int spot = queue.Line.Count - 1;
            PassengerView view = PoolingManager.Spawn(prefab, lineView.SpawnPoint,
                Quaternion.LookRotation(path.FacingAt(path.Length)), lineView.PassengersRoot);
            if (view == null) return;
            view.Init(passenger.Id, config.GetCarColor(passenger.ColorIndex), passenger.IsVip);
            lineViews.Add(view);

            walkBuffer.Clear();
            walkBuffer.Add(path.PositionAt(path.Length)); // SpawnPoint → cuối hàng (trùng nhau thì đoạn này dài 0)
            path.AppendWalk(path.Length, spot * spacing, walkBuffer);
            view.WalkTo(walkBuffer, config.passengerJoinSpeed, config.passengerTurnSharpness,
                Quaternion.LookRotation(path.FacingAt(spot * spacing)));
        }

        /// Đi tới chỗ thứ <paramref name="spot"/> dọc theo hàng, tính từ vị trí hiện tại trên đường:
        /// khách đang đi vào dở cũng rẽ đúng góc, không cắt chéo.
        private void WalkToSpot(PassengerView view, int spot, float speed)
        {
            float target = spot * spacing;
            walkBuffer.Clear();
            path.AppendWalk(path.ClosestDistance(view.transform.position), target, walkBuffer);
            view.WalkTo(walkBuffer, speed, config.passengerTurnSharpness, Quaternion.LookRotation(path.FacingAt(target)));
        }

        private void HandleLeftQueueArrived(PassengerView view)
        {
            leavingViews.Remove(view);
            PassengerReachedCar?.Invoke(view);
        }
    }
}
