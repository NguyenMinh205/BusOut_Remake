using System.Collections.Generic;

namespace BusOut.Gameplay
{
    /// Xe nào nằm trên đường ra của xe nào. Dựng một lần lúc vào level:
    /// xe trong bãi chỉ đứng yên hoặc rời đi hẳn, không bao giờ quay lại, nên đồ thị chỉ cần lọc theo trạng thái xe.
    public sealed class BlockingGraph
    {
        private static readonly int[] None = new int[0];
        private readonly int[][] ahead;

        public BlockingGraph(int carCount)
        {
            ahead = new int[carCount][];
            for (int i = 0; i < carCount; i++) ahead[i] = None;
        }

        public void SetAhead(int carId, List<int> carIds) => ahead[carId] = carIds.ToArray();

        /// Mọi xe nằm trên đường ra của <paramref name="carId"/> (gần hay xa đều tính).
        public IReadOnlyList<int> AheadOf(int carId) => ahead[carId];
    }
}
