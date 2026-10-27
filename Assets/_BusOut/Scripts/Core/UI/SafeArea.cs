using System;
using UnityEngine;

namespace BusOut.Core
{
    /// Co RectTransform này vào vùng an toàn của màn hình (tránh tai thỏ, đục lỗ camera, bo góc, thanh home).
    /// Không phụ thuộc gì ngoài UnityEngine: chép sang project khác dùng được ngay (đổi namespace nếu cần).
    ///
    /// Cách dùng:
    /// 1. Gắn lên một object kéo giãn toàn Canvas (anchor 0..1); UI cần tránh tai thỏ là con của nó.
    ///    Object phải là con trực tiếp của Canvas gốc (hoặc của object phủ kín màn): anchor tính theo cả màn hình.
    /// 2. Panel có nền phủ kín màn (nền tối popup, ảnh nền): để nền ở NGOÀI, gắn SafeArea lên một con kéo giãn chứa phần
    ///    nội dung — nền vẫn phủ cả dải tai thỏ / thanh home, nội dung thì tránh.
    /// 3. Chỉ cần tránh một số cạnh (vd. thanh trên chỉ tránh tai thỏ, không bị thanh home đẩy): bỏ chọn cạnh trong Edges.
    ///
    /// Thử: Device Simulator (Window ▸ General ▸ Device Simulator) trả Screen.safeArea thật theo từng máy.
    /// Android: muốn vẽ cả vùng tai thỏ thì bật Player Settings ▸ Resolution ▸ Render outside safe area; tắt thì hệ thống
    /// tự chừa vùng đó (safe area = cả màn, script này không đổi gì).
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeArea : MonoBehaviour
    {
        [Flags]
        public enum Edges
        {
            None = 0,
            Left = 1,
            Right = 2,
            Top = 4,
            Bottom = 8,
            All = Left | Right | Top | Bottom,
        }

        [Tooltip("Các cạnh tránh vùng không an toàn. Bỏ chọn một cạnh thì cạnh đó vẫn sát mép màn hình.")]
        [SerializeField] private Edges edges = Edges.All;

        private RectTransform rectTransform;
        private Rect appliedSafeArea;
        private Vector2Int appliedScreenSize;
        private ScreenOrientation appliedOrientation;
        private Edges appliedEdges;
        private bool hasApplied;

        public Edges ConformEdges
        {
            get => edges;
            set
            {
                edges = value;
                Refresh(true);
            }
        }

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>(true);
            if (canvas == null)
                Debug.LogError($"[SafeArea] '{name}' không nằm trong Canvas nào: không có màn hình để tính vùng an toàn. " +
                               "Đặt object vào trong một Canvas.", this);
            else if (canvas.rootCanvas.renderMode == RenderMode.WorldSpace)
                Debug.LogError($"[SafeArea] '{name}' nằm trong Canvas World Space: vùng an toàn chỉ có nghĩa với UI phủ màn " +
                               "hình. Đổi Canvas sang Screen Space hoặc gỡ SafeArea.", this);
        }

        /// ⚠️ Áp lại khi bật: object đang tắt không chạy Update — đổi máy / xoay màn lúc popup đang tắt thì lần bật sau
        /// sẽ vẽ sai một frame nếu chỉ chờ Update.
        private void OnEnable() => Refresh(true);

        /// So mỗi frame (rẻ, không cấp phát), chỉ đặt lại anchor khi safe area / kích thước / hướng xoay thật sự đổi.
        private void Update() => Refresh(false);

        /// Gọi tay khi cần áp ngay (vd. vừa đổi Canvas). <paramref name="force"/> = áp dù không có gì đổi.
        public void Refresh(bool force)
        {
            if (rectTransform == null) rectTransform = (RectTransform)transform;
            Rect safeArea = Screen.safeArea;
            int width = Screen.width, height = Screen.height;
            ScreenOrientation orientation = Screen.orientation;
            if (!force && hasApplied && safeArea == appliedSafeArea && width == appliedScreenSize.x &&
                height == appliedScreenSize.y && orientation == appliedOrientation && edges == appliedEdges)
                return;

            // ⚠️ Lúc khởi động, vài máy (Samsung Note 10+, A71, S20…) báo kích thước 0 hoặc safe area NaN / âm ở lần gọi
            // đầu. Bỏ qua lần đó (giữ anchor cũ) và KHÔNG ghi nhớ, để frame sau thử lại.
            if (width <= 0 || height <= 0 || !IsFinite(safeArea)) return;

            float xMin = (edges & Edges.Left) != 0 ? safeArea.xMin / width : 0f;
            float xMax = (edges & Edges.Right) != 0 ? safeArea.xMax / width : 1f;
            float yMin = (edges & Edges.Bottom) != 0 ? safeArea.yMin / height : 0f;
            float yMax = (edges & Edges.Top) != 0 ? safeArea.yMax / height : 1f;
            xMin = Mathf.Clamp01(xMin);
            xMax = Mathf.Clamp01(xMax);
            yMin = Mathf.Clamp01(yMin);
            yMax = Mathf.Clamp01(yMax);
            if (xMax <= xMin || yMax <= yMin) return;

            rectTransform.anchorMin = new Vector2(xMin, yMin);
            rectTransform.anchorMax = new Vector2(xMax, yMax);
            // ⚠️ Anchor đã nằm đúng mép vùng an toàn: lề phải về 0, không thì lệch thêm đúng bằng lề đang đặt trong scene.
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            appliedSafeArea = safeArea;
            appliedScreenSize = new Vector2Int(width, height);
            appliedOrientation = orientation;
            appliedEdges = edges;
            hasApplied = true;
        }

        private static bool IsFinite(Rect rect) =>
            !float.IsNaN(rect.x) && !float.IsNaN(rect.y) && !float.IsNaN(rect.width) && !float.IsNaN(rect.height) &&
            !float.IsInfinity(rect.width) && !float.IsInfinity(rect.height) && rect.width > 0f && rect.height > 0f;
    }
}
