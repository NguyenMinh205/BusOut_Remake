using System.Collections.Generic;

namespace BusOut.Gameplay
{
    /// Hình của một ô, suy ra từ trạng thái (không lưu).
    public enum SlotVisual
    {
        Normal,
        Warning, // ô trống cuối cùng còn lại: sắp hết chỗ
        Ads,     // ô khoá, xem quảng cáo để mở
        Construction, // ô công trường: mở khi đủ khách lên xe
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
            if (slot.IsUnderConstruction) return SlotVisual.Construction;
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

        /// Còn ô khoá (hồi sinh còn mở thêm được).
        public bool HasLockedSlot
        {
            get
            {
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i].IsLocked) return true;
                return false;
            }
        }

        /// Ô công trường: <paramref name="slotCount"/> ô mở cuối bến bị chắn; ô bên trái mở trước, mỗi ô cần thêm
        /// <paramref name="passengersPerSlot"/> khách. Luôn chừa ít nhất 1 ô mở, không thì ván không đi được nước nào.
        public void SetConstruction(int slotCount, int passengersPerSlot)
        {
            if (slotCount <= 0 || passengersPerSlot <= 0) return;
            var open = new List<int>();
            for (int i = 0; i < slots.Length; i++)
                if (!slots[i].IsLocked) open.Add(i);
            int count = System.Math.Min(slotCount, open.Count - 1);
            for (int k = 0; k < count; k++)
                slots[open[open.Count - count + k]].SetConstruction(passengersPerSlot * (k + 1));
            if (count > 0) Version++;
        }

        public int ConstructionSlotCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i].IsUnderConstruction) count++;
                return count;
            }
        }

        /// Một khách vừa lên xe: mọi ô công trường đếm lùi. Trả ô vừa mở (-1 khi không ô nào mở lần này).
        public int OnPassengerBoarded()
        {
            int opened = -1;
            bool changed = false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsUnderConstruction) continue;
                changed = true;
                if (slots[i].CountDownConstruction()) opened = i;
            }
            if (changed) Version++;
            return opened;
        }

        /// Mở ô khoá số nhỏ nhất (hồi sinh: "UnlockNearestEmptySlot" của gốc). -1 khi không còn ô khoá.
        public int UnlockNearest()
        {
            for (int i = 0; i < slots.Length; i++)
                if (Unlock(i)) return i;
            return -1;
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
