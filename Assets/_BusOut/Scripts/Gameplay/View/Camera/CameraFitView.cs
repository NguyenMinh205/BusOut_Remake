using UnityEngine;

namespace BusOut.Gameplay
{
    /// Chỉnh FOV camera gameplay theo tỉ lệ màn hình để khung đã căn ở tỉ lệ tham chiếu luôn thấy trọn.
    /// Màn hẹp hơn tham chiếu (điện thoại dài) → tăng FOV giữ nguyên bề ngang thấy được (như CameraManager gốc).
    /// Màn rộng hơn (iPad) → giữ FOV gốc: giữ bề ngang ở đây là thu chiều dọc, mất bến và hàng khách.
    /// Gắn trên GameContext/Main Camera; FOV gốc = FOV đang đặt trong scene.
    [RequireComponent(typeof(Camera))]
    public class CameraFitView : MonoBehaviour
    {
        private Camera targetCamera;
        private float referenceVerticalFov;
        private float referenceHalfWidthTan; // tan(nửa FOV ngang) ở tỉ lệ tham chiếu
        private Vector2Int appliedScreenSize;
        private bool isReady;

        /// <paramref name="referenceAspect"/> = rộng / cao của màn bạn đã căn camera (vd. 1170/2532).
        public bool Init(float referenceAspect)
        {
            targetCamera = GetComponent<Camera>();
            if (referenceAspect <= 0f)
            {
                Debug.LogError($"[BusOut] CameraFitView: cameraReferenceAspect = {referenceAspect} không hợp lệ. " +
                               "Đặt = rộng / cao của màn đã căn camera (vd. 0.462 cho 1170×2532) trong GameplayConfig.", this);
                return false;
            }
            // ⚠️ Đọc FOV gốc MỘT lần lúc Init: đọc lại sau khi đã chỉnh là FOV phình dần mỗi lần màn đổi.
            referenceVerticalFov = targetCamera.fieldOfView;
            referenceHalfWidthTan = Mathf.Tan(referenceVerticalFov * 0.5f * Mathf.Deg2Rad) * referenceAspect;
            isReady = true;
            Apply();
            return true;
        }

        // Đổi máy trong Device Simulator / chia đôi màn: so kích thước mỗi frame (rẻ), chỉ tính lại khi đổi.
        private void LateUpdate()
        {
            if (isReady && (Screen.width != appliedScreenSize.x || Screen.height != appliedScreenSize.y)) Apply();
        }

        private void Apply()
        {
            appliedScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (appliedScreenSize.x <= 0 || appliedScreenSize.y <= 0) return;
            float aspect = (float)appliedScreenSize.x / appliedScreenSize.y;
            float fovKeepingWidth = 2f * Mathf.Atan(referenceHalfWidthTan / aspect) * Mathf.Rad2Deg;
            targetCamera.fieldOfView = Mathf.Max(referenceVerticalFov, fovKeepingWidth);
        }
    }
}
