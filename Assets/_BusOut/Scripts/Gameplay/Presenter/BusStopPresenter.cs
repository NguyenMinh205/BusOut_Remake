using System.Collections.Generic;
using BusOut.Core;
using BusOut.DesignPattern;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Ô đỗ ở bến: giữ / tới / trả ô, mở ô khoá, cho biết điểm vào và điểm dừng của từng ô, vẽ hình ô theo trạng thái.
    public sealed class BusStopPresenter
    {
        private readonly BusStop busStop;
        private readonly BusStopView view;
        private readonly GameplayConfig config;
        private readonly FxConfig fx;
        private readonly bool hasSlotRing;
        private readonly bool hasUnlockEffect;

        /// Trạng thái 7 ô (chỉ đọc), theo thứ tự trái → phải.
        public IReadOnlyList<BusStopSlot> Slots => busStop.Slots;

        /// Số ô mở và chưa có xe giữ.
        public int FreeCount => busStop.FreeCount;

        public BusStopPresenter(BusStopView view, GameplayConfig config)
        {
            this.view = view;
            this.config = config;
            busStop = new BusStop(view.SlotCount);
            fx = FxConfig.Instance;
            hasSlotRing = fx.slotRingPrefab != null;
            hasUnlockEffect = fx.slotUnlockEffectPrefab != null;
            if (!hasSlotRing)
                Debug.LogError("[BusOut] FxConfig thiếu Slot Ring Prefab: xe vào ô / mở ô không có vòng sáng. Kéo " +
                               "Prefab/VFX/SlotRing vào Resources/FxConfig.", fx);
            if (!hasUnlockEffect)
                Debug.LogError("[BusOut] FxConfig thiếu Slot Unlock Effect Prefab: mở ô không có pháo sáng. Kéo " +
                               "Prefab/VFX/LevelPassEffect vào Resources/FxConfig.", fx);
        }

        public void ResetForLevel(int openSlotCount)
        {
            busStop.Reset(openSlotCount);
            Refresh();
        }

        /// Giữ ô trống số nhỏ nhất cho xe. -1 khi hết ô.
        public int TryReserve(int carId)
        {
            int slotIndex = busStop.TryReserve(carId);
            Refresh();
            return slotIndex;
        }

        public void NotifyCarArrived(int slotIndex)
        {
            busStop.MarkOccupied(slotIndex);
            Refresh();
        }

        /// Trả ô ngay khi xe bắt đầu lùi ra: xe sau có thể giữ ô luôn, tới nơi thì xe trước đã đi khỏi.
        public void Release(int slotIndex)
        {
            busStop.Release(slotIndex);
            Refresh();
        }

        /// Ô có đang khoá không (chạm ô khoá → popup mở thêm ô, mua bằng coin / quảng cáo).
        public bool IsLocked(int slotIndex) =>
            slotIndex >= 0 && slotIndex < busStop.SlotCount && busStop.Slots[slotIndex].IsLocked;

        public bool HasLockedSlot => busStop.HasLockedSlot;

        /// Mở thêm 1 ô (ô khoá gần nhất) — hồi sinh, hoặc mua ô ở popup mở ô. -1 khi không còn ô khoá.
        /// Trả coin / xem quảng cáo do nơi gọi lo (LevelResultPresenter, PurchasePresenter).
        public int UnlockNearest()
        {
            int slotIndex = busStop.UnlockNearest();
            if (slotIndex < 0) return slotIndex;
            Refresh();
            // Mua / hồi sinh xong phải thấy ngay ô nào vừa mở: vòng tỏa + pháo sáng tại ô đó.
            PlaySlotRing(slotIndex);
            if (hasUnlockEffect) SpawnAtSlot(fx.slotUnlockEffectPrefab, slotIndex, fx.slotUnlockEffectScale);
            return slotIndex;
        }

        public Vector3 GetEntryPoint(int slotIndex) => view.GetSlot(slotIndex).EntryPoint;
        public Vector3 GetParkPoint(int slotIndex) => view.GetSlot(slotIndex).ParkPoint;

        /// Vòng sáng phẳng tỏa trên mặt ô (xe vừa vào ô, ô vừa mở). Prefab tự trả về pool (AutoDespawn).
        public void PlaySlotRing(int slotIndex)
        {
            if (hasSlotRing) SpawnAtSlot(fx.slotRingPrefab, slotIndex, 1f);
        }

        /// Màn Loading: tạo sẵn hiệu ứng ô đỗ (lần đầu xe vào ô / mở ô không phải Instantiate giữa ván).
        public void PrewarmEffects()
        {
            if (hasSlotRing) PoolingManager.Prewarm(fx.slotRingPrefab, fx.slotRingPrewarmCount);
            if (hasUnlockEffect) PoolingManager.Prewarm(fx.slotUnlockEffectPrefab, 1);
        }

        private void SpawnAtSlot(GameObject prefab, int slotIndex, float scale)
        {
            // Nâng nhẹ khỏi mặt đường: vòng nằm phẳng trùng mặt đất là chớp (z-fighting).
            Vector3 position = GetParkPoint(slotIndex) + Vector3.up * fx.slotEffectHeight;
            GameObject effect = PoolingManager.Spawn(prefab, position, prefab.transform.rotation);
            // Đặt cỡ mỗi lần lấy ra (bản trong pool giữ cỡ lần trước; đổi config lúc đang chơi cũng ăn ngay).
            if (effect != null) effect.transform.localScale = prefab.transform.localScale * scale;
        }

        private void Refresh() => view.Render(busStop, config);
    }
}
