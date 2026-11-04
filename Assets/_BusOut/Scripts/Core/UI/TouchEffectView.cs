using Coffee.UIExtensions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BusOut.Core
{
    /// Hiệu ứng chạm toàn game (gốc: TouchEffect — vòng tròn tỏa + quầng + sao nhỏ tại ngón tay).
    /// Gốc vẽ trong không gian 3D trước camera; ở đây UI là Canvas Overlay (luôn nằm trên) nên vẽ kiểu gốc là chạm vào nút
    /// sẽ không thấy → vẽ bằng UI Particle trên một Canvas riêng nằm TRÊN CÙNG.
    /// Gắn trên TouchFxCanvas (gốc scene): Canvas Screen Space Overlay, Sort Order cao nhất, KHÔNG có Graphic Raycaster
    /// (không được chặn chạm). Tạo sẵn một vòng pool "vật chủ" (UI Particle chứa một bản prefab), mỗi lần chạm lấy cái kế
    /// tiếp, đặt tại ngón tay, phát lại — không Instantiate / cấp phát lúc chạy.
    public class TouchEffectView : MonoBehaviour
    {
        [Tooltip("Prefab hiệu ứng chạm (Prefab/VFX/UI/TouchEffect): các Particle System dùng material shader UI " +
                 "(UI/Additive) — shader particle URP không vẽ đúng trong Canvas.")]
        [SerializeField] private GameObject effectPrefab;

        private RectTransform canvasRect;
        private Camera canvasCamera;          // null với Canvas Overlay
        private RectTransform[] hostRects;
        private UIParticle[] hostParticles;
        private int nextHost;
        private bool isReady;

        private void Awake()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null || effectPrefab == null)
            {
                Debug.LogError("[BusOut] TouchEffectView thiếu Canvas (cùng object) hoặc Effect Prefab: chạm không có hiệu ứng. " +
                               "Gắn lên TouchFxCanvas, kéo Prefab/VFX/UI/TouchEffect vào.", this);
                return;
            }
            // Tắt (bỏ tick) cũng được như xoá: raycaster tắt thì canvas không nhận chạm.
            var raycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null && raycaster.enabled)
                Debug.LogError("[BusOut] TouchFxCanvas có Graphic Raycaster đang bật: canvas hiệu ứng có thể chặn chạm xuống nút " +
                               "bên dưới. Tắt hoặc xoá component Graphic Raycaster khỏi TouchFxCanvas.", this);
            canvasRect = (RectTransform)transform;
            canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            // Độ phóng, số vật chủ: FxConfig (tự nạp từ Resources).
            BuildHosts(Mathf.Max(1, FxConfig.Instance.touchEffectPoolSize));
            isReady = true;
        }

        private void BuildHosts(int count)
        {
            hostRects = new RectTransform[count];
            hostParticles = new UIParticle[count];
            for (int i = 0; i < count; i++)
            {
                var host = new GameObject($"TouchFx{i}", typeof(RectTransform), typeof(CanvasRenderer));
                var rect = (RectTransform)host.transform;
                rect.SetParent(transform, false);
                var particle = host.AddComponent<UIParticle>();
                // ⚠️ UI Particle là Graphic: để mặc định thì nó nhận raycast, nằm trên cùng là nuốt mọi cú chạm vào nút.
                particle.raycastTarget = false;
                particle.scale3D = Vector3.one * FxConfig.Instance.touchEffectScale;
                Instantiate(effectPrefab, rect, false);
                particle.RefreshParticles();
                // Prefab để Play On Awake: vừa tạo là phát một lần ở giữa màn — dừng + xoá ngay.
                particle.Stop();
                particle.Clear();
                hostRects[i] = rect;
                hostParticles[i] = particle;
            }
        }

        private void Update()
        {
            if (!isReady) return;
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointer.position.ReadValue(), canvasCamera,
                    out Vector2 local)) return;
            int index = nextHost;
            nextHost = (nextHost + 1) % hostRects.Length;
            hostRects[index].anchoredPosition = local;
            hostParticles[index].Play();
        }
    }
}
