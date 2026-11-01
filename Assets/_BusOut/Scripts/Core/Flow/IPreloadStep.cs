using System;
using System.Collections;

namespace BusOut.Core
{
    /// Một bước tạo sẵn chạy trong lúc màn Loading còn che (GameFlowController.Preload gọi lần lượt): dựng trước những gì
    /// nặng (pool, khởi tạo presenter) để lúc chơi không giật. Chạy nhiều khung được — yield giữa các phần nặng để thanh
    /// tiến trình còn chạy. Không có màn Loading (chạy thẳng GameScene trong Editor) thì không ai gọi: phải tự lo lúc cần.
    public interface IPreloadStep
    {
        /// <paramref name="reportProgress"/>: tiến độ 0..1 của riêng bước này.
        IEnumerator Preload(Action<float> reportProgress);
    }
}
