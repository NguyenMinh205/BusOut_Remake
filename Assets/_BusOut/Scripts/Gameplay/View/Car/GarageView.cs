using TMPro;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Cổng bãi chờ: số xe còn chờ bên trong + màu xe ra tiếp theo. Gắn trên root prefab; LevelSpawner đặt đúng chỗ xe đứng trước
    /// cổng (gốc prefab = gốc xe đó), nên phần nhà cổng trong prefab phải nằm phía sau (−Z) đủ xa để không chồng xe dài nhất.
    /// Không có collider: xe khác không được chạy xuyên cổng (Level Builder và bộ sinh đã soát).
    public class GarageView : MonoBehaviour
    {
        [Tooltip("Chữ TextMeshPro (3D) hiện số xe còn chờ.")]
        [SerializeField] private TMP_Text countText;
        [Tooltip("Không bắt buộc. Sprite tô màu xe ra tiếp theo; ẩn khi hết xe chờ.")]
        [SerializeField] private SpriteRenderer nextColor;

        public void SetQueue(int waiting, Color nextCarColor)
        {
            if (countText != null) countText.SetText("{0}", waiting);
            if (nextColor == null) return;
            nextColor.gameObject.SetActive(waiting > 0);
            nextColor.color = nextCarColor;
        }
    }
}
