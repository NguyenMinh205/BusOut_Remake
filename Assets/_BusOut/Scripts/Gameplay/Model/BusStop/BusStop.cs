using System.Collections.Generic;

namespace BusOut.Gameplay
{
    /// Hình của một ô, suy ra từ trạng thái (không lưu).
    public enum SlotVisual
    {
        Normal,
        Warning, // ô trống cuối cùng còn lại: sắp hết chỗ
        Ads,     // ô khoá, xem quảng cáo để mở
    }

    /// Các ô đỗ ở bến. Xe giữ ô ngay lúc được chạm (trước khi chạy tới) để hai xe chạm liền nhau không tranh một ô.
    public sealed class BusStop
    {
        private readonly BusStopSlot[] slots;

        public IReadOnlyList<BusStopSlot> Slots => slots;
        public int SlotCount => slots.Length;

        /// Tăng mỗi lần có ô đổi trạng thái. View so số này để chỉ vẽ lại khi thật sự đổi.
        public int Version { get; private set; }

        public BusStop(int slotCount)
        {
            slots = new BusStopSlot[slotCount];
            for (int i = 0; i < slotCount; i++) slots[i] = new BusStopSlot(i);
        }

        /// Đầu level: mọi ô trống, <paramref name="openCount"/> ô đầu mở, còn lại khoá.
        public void Reset(int openCount)
        {
            for (int i = 0; i < slots.Length; i++) slots[i].Reset(i >= openCount);
            Version++;
        }

        /// Số ô mở và chưa có xe giữ.
        public int FreeCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i].IsFree) count++;
                return count;
            }
        }

        /// Khoá → Ads; ô trống DUY NHẤT còn lại → Warning (như game gốc: còn đúng 1 chỗ thì cảnh báo); còn lại → Normal.
        public SlotVisual GetVisual(int slotIndex)
        {
            BusStopSlot slot = slots[slotIndex];
            if (slot.IsLocked) return SlotVisual.Ads;
            if (slot.IsFree && FreeCount == 1) return SlotVisual.Warning;
            return SlotVisual.Normal;
        }

        /// Giữ ô trống có số nhỏ nhất. Trả -1 khi hết ô.
        public int TryReserve(int carId)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsFree) continue;
                slots[i].Reserve(carId);
                Version++;
                return i;
            }
            return -1;
        }

        /// Mở một ô khoá. False nếu ô không khoá (chạm thừa / chạm 2 lần).
        public bool Unlock(int slotIndex)
        {
            if (!IsValid(slotIndex) || !slots[slotIndex].IsLocked) return false;
            slots[slotIndex].Unlock();
            Version++;
            return true;
        }

        public void MarkOccupied(int slotIndex)
        {
            if (!IsValid(slotIndex)) return;
            slots[slotIndex].Occupy();
            Version++;
        }

        public void Release(int slotIndex)
        {
            if (!IsValid(slotIndex)) return;
            slots[slotIndex].Release();
            Version++;
        }

        private bool IsValid(int slotIndex) => slotIndex >= 0 && slotIndex < slots.Length;
    }
}
