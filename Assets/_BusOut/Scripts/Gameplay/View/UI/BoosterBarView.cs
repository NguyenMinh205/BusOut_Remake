using UnityEngine;

namespace BusOut.Gameplay
{
    /// Thanh booster dưới màn chơi. Gắn trên Canvas/BottomUI/Boosters.
    public class BoosterBarView : MonoBehaviour
    {
        [Tooltip("Các nút theo thứ tự trái → phải; nút thứ i ứng với GameplayConfig.boosters[i].")]
        [SerializeField] private BoosterButtonView[] buttons;

        public int Count => buttons.Length;
        public BoosterButtonView GetButton(int index) => buttons[index];

        public bool Init()
        {
            if (buttons == null || buttons.Length == 0)
            {
                Debug.LogError("[BusOut] BoosterBarView chưa có nút nào. Kéo 4 object Booster (trái → phải) vào Buttons.", this);
                return false;
            }
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].Init()) continue;
                if (buttons[i] == null)
                    Debug.LogError($"[BusOut] BoosterBarView: ô Buttons[{i}] trống. Kéo object Booster vào.", this);
                return false;
            }
            return true;
        }
    }
}
