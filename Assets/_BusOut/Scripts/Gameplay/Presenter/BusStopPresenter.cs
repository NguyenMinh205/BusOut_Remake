using System.Collections.Generic;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Ô đỗ ở bến: giữ / tới / trả ô, mở ô khoá, cho biết điểm vào và điểm dừng của từng ô, vẽ hình ô theo trạng thái.
    public sealed class BusStopPresenter
    {
        private readonly BusStop busStop;
        private readonly BusStopView view;
        private readonly GameplayConfig config;

        /// Trạng thái 7 ô (chỉ đọc), theo thứ tự trái → phải.
        public IReadOnlyList<BusStopSlot> Slots => busStop.Slots;

        /// Số ô mở và chưa có xe giữ.
        public int FreeCount => busStop.FreeCount;

        public BusStopPresenter(BusStopView view, GameplayConfig config)
        {
            this.view = view;
            this.config = config;
            busStop = new BusStop(view.SlotCount);
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

        /// Người chơi chạm vào một ô.
        /// ⚠️ TẠM: ô khoá mở NGAY. Khi có quảng cáo (Core): xin rewarded ad, chỉ mở khi nhận AdCompletedEvent có thưởng.
        public void HandleSlotTapped(int slotIndex)
        {
            if (busStop.Unlock(slotIndex)) Refresh();
        }

        public bool HasLockedSlot => busStop.HasLockedSlot;

        /// Hồi sinh: mở thêm 1 ô (ô khoá gần nhất). -1 khi không còn ô khoá.
        /// ⚠️ TẠM: mở ngay, chưa qua quảng cáo / coin (như HandleSlotTapped).
        public int UnlockNearest()
        {
            int slotIndex = busStop.UnlockNearest();
            if (slotIndex >= 0) Refresh();
            return slotIndex;
        }

        public Vector3 GetEntryPoint(int slotIndex) => view.GetSlot(slotIndex).EntryPoint;
        public Vector3 GetParkPoint(int slotIndex) => view.GetSlot(slotIndex).ParkPoint;

        private void Refresh() => view.Render(busStop, config);
    }
}
