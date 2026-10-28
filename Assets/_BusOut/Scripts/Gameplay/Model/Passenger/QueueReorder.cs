using System.Collections.Generic;

namespace BusOut.Gameplay
{
    /// Một nhu cầu khách: màu nào, bao nhiêu người (vd. xe đỏ ở bến còn 3 ghế).
    public struct ColorDemand
    {
        public int Color;
        public int Count;

        public ColorDemand(int color, int count)
        {
            Color = color;
            Count = count;
        }
    }

    /// Sắp lại thứ tự màu của hàng khách cho booster (Sort, Shuffle). Chỉ đổi THỨ TỰ: số khách mỗi màu giữ nguyên,
    /// nên tổng khách mỗi màu vẫn bằng tổng ghế xe màu đó.
    public static class QueueReorder
    {
        /// Lần lượt từng nhu cầu, lấy những khách cùng màu đứng SỚM NHẤT trong <paramref name="colors"/> đưa lên đầu
        /// (đúng thứ tự nhu cầu); khách còn lại giữ nguyên thứ tự tương đối. Ghi kết quả vào <paramref name="result"/>.
        /// Trả về số khách đã được kéo lên.
        public static int PullToFront(IReadOnlyList<int> colors, IReadOnlyList<ColorDemand> demands, List<int> result,
            List<bool> takenBuffer)
        {
            result.Clear();
            takenBuffer.Clear();
            for (int i = 0; i < colors.Count; i++) takenBuffer.Add(false);
            int pulled = 0;
            for (int d = 0; d < demands.Count; d++)
            {
                int need = demands[d].Count;
                for (int i = 0; i < colors.Count && need > 0; i++)
                {
                    if (takenBuffer[i] || colors[i] != demands[d].Color) continue;
                    takenBuffer[i] = true;
                    result.Add(colors[i]);
                    need--;
                    pulled++;
                }
            }
            for (int i = 0; i < colors.Count; i++)
                if (!takenBuffer[i]) result.Add(colors[i]);
            return pulled;
        }

        /// Chia nhu cầu từng xe thành nhóm groupMin..groupMax người rồi trộn xen kẽ NGẪU NHIÊN giữa các xe, tránh 2 nhóm
        /// cùng màu liền nhau khi còn cách. Nối các nhóm (theo thứ tự trộn) vào cuối <paramref name="output"/> —
        /// đưa thẳng vào PullToFront như danh sách nhu cầu.
        public static void AppendInterleaved(IReadOnlyList<ColorDemand> cars, System.Random random, int groupMin,
            int groupMax, List<ColorDemand> output, List<ColorDemand> groupsBuffer, List<int> ownerBuffer)
        {
            groupsBuffer.Clear();
            ownerBuffer.Clear();
            int min = System.Math.Max(1, groupMin);
            int max = System.Math.Max(min, groupMax);
            for (int c = 0; c < cars.Count; c++)
            {
                int left = cars[c].Count;
                while (left > 0)
                {
                    int size = System.Math.Min(left, random.Next(min, max + 1));
                    if (left - size < min) size = left; // không để nhóm lẻ nhỏ hơn min ở cuối
                    groupsBuffer.Add(new ColorDemand(cars[c].Color, size));
                    ownerBuffer.Add(c);
                    left -= size;
                }
            }
            // Bốc ngẫu nhiên nhóm kế tiếp, nhưng giữ thứ tự nhóm trong cùng một xe (nhóm đầu của xe ra trước).
            int lastColor = -1;
            while (groupsBuffer.Count > 0)
            {
                int pick = -1, tries = 0;
                while (tries++ < 8)
                {
                    int owner = ownerBuffer[random.Next(ownerBuffer.Count)];
                    int first = ownerBuffer.IndexOf(owner);
                    if (groupsBuffer[first].Color != lastColor || tries == 8)
                    {
                        pick = first;
                        break;
                    }
                }
                output.Add(groupsBuffer[pick]);
                lastColor = groupsBuffer[pick].Color;
                groupsBuffer.RemoveAt(pick);
                ownerBuffer.RemoveAt(pick);
            }
        }

        public static bool SameOrder(IReadOnlyList<int> a, IReadOnlyList<int> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
