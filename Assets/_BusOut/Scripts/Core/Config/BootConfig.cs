using UnityEngine;

namespace BusOut.Core
{
    /// Số chỉnh được của phần khởi động game: GameBootstrap + màn Loading.
    [CreateAssetMenu(menuName = "BusOut/Boot Config", fileName = "BootConfig")]
    public class BootConfig : ScriptableObject
    {
        [Header("Ứng dụng")]
        [Tooltip("Khung hình / giây mục tiêu (Application.targetFrameRate). ⚠️ Không đặt thì điện thoại chạy 30 fps mặc định: " +
                 "xe chạy, khách đi trông giật dù máy còn dư sức. 60 = mượt, tốn pin hơn; ≤ 0 = để mặc định nền tảng.")]
        public int targetFrameRate = 60;

        [Header("Người chơi mới")]
        [Tooltip("Level đang lưu nhỏ hơn số này thì mở game vào thẳng ván, không qua Menu. 2 = chưa thắng level 1 thì chơi luôn level 1; " +
                 "1 = luôn vào Menu.")]
        [Min(1)] public int directPlayBelowLevel = 2;

        [Header("Màn Loading")]
        [Tooltip("Tên scene chơi (phải có trong Build Settings).")]
        public string gameSceneName = "GameScene";

        [Tooltip("Màn Loading hiện tối thiểu bấy nhiêu giây, kể cả khi nạp xong sớm hơn (không thì màn chớp qua).")]
        public float loadingMinDuration = 1f;

        [Tooltip("Phần thanh tiến trình dành cho nạp scene (0..1); phần còn lại cho các bước tạo sẵn trong GameScene.")]
        [Range(0f, 1f)] public float sceneLoadProgressShare = 0.5f;

        [Tooltip("Thanh tiến trình chạy tối đa bấy nhiêu phần mỗi giây (1 = cả thanh trong 1 giây): chạy mượt thay vì nhảy cục.")]
        public float loadingBarFillSpeed = 1.5f;

        [Tooltip("Nạp xong: màn Loading mờ dần trong bấy nhiêu giây rồi mới lộ Menu.")]
        public float loadingFadeDuration = 0.3f;

        public static BootConfig CreateDefault() => CreateInstance<BootConfig>();
    }
}
