using System.Collections.Generic;

namespace BusOut.Gameplay
{
    /// Hàng khách: những người đang đứng trong hàng (tối đa Capacity) + màu của những người đã lên kế hoạch
    /// nhưng chưa xuất hiện. Màu được nạp dần (AddPending) theo PassengerColorPlanner.
    public sealed class PassengerQueue
    {
        // Phần tử 0 = đầu hàng (gần bến nhất).
        private readonly List<PassengerModel> line = new List<PassengerModel>();
        private readonly Queue<int> pendingColors = new Queue<int>();
        private int nextId;

        public int Capacity { get; private set; }
        public IReadOnlyList<PassengerModel> Line => line;
        public PassengerModel Head => line.Count > 0 ? line[0] : null;

        /// Số khách đang đứng + đã lên kế hoạch chưa xuất hiện (chưa gồm khách chưa lên kế hoạch).
        public int RemainingCount => line.Count + pendingColors.Count;

        public int PendingCount => pendingColors.Count;

        /// Còn chỗ trống ở cuối hàng và còn khách chưa xuất hiện.
        public bool CanAddToTail => line.Count < Capacity && pendingColors.Count > 0;

        public void Reset(int capacity)
        {
            line.Clear();
            pendingColors.Clear();
            Capacity = capacity;
            nextId = 0;
        }

        public void AddPending(IReadOnlyList<int> colors)
        {
            for (int i = 0; i < colors.Count; i++) pendingColors.Enqueue(colors[i]);
        }

        /// Đưa khách tiếp theo vào cuối hàng. Chỉ số của khách trong hàng = Line.Count - 1 sau khi thêm.
        public bool TryAddToTail(out PassengerModel passenger)
        {
            if (!CanAddToTail)
            {
                passenger = null;
                return false;
            }
            passenger = new PassengerModel(nextId++, pendingColors.Dequeue(), false);
            line.Add(passenger);
            return true;
        }

        /// Màu của cả hàng theo thứ tự: đang đứng (đầu → cuối) rồi đã lên kế hoạch chưa xuất hiện.
        public void CopyColors(List<int> into)
        {
            into.Clear();
            for (int i = 0; i < line.Count; i++) into.Add(line[i].ColorIndex);
            foreach (int color in pendingColors) into.Add(color);
        }

        /// Đặt lại màu theo thứ tự mới (cùng số lượng với CopyColors). Ghi chỉ số khách ĐANG ĐỨNG bị đổi màu vào
        /// <paramref name="changedLine"/> để View tô lại. False (không đổi gì) khi số lượng lệch.
        public bool ApplyColors(IReadOnlyList<int> colors, List<int> changedLine)
        {
            changedLine.Clear();
            if (colors.Count != line.Count + pendingColors.Count) return false;
            for (int i = 0; i < line.Count; i++)
            {
                if (line[i].ColorIndex == colors[i]) continue;
                line[i].ColorIndex = colors[i];
                changedLine.Add(i);
            }
            pendingColors.Clear();
            for (int i = line.Count; i < colors.Count; i++) pendingColors.Enqueue(colors[i]);
            return true;
        }

        /// Người đầu hàng rời hàng; cả hàng dồn lên một chỗ.
        public PassengerModel RemoveHead()
        {
            if (line.Count == 0) return null;
            PassengerModel head = line[0];
            line.RemoveAt(0);
            return head;
        }
    }
}
