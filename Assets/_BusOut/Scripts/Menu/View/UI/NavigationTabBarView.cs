using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BusOut.Menu
{
    /// Thanh tab dưới Menu: chạm tab → báo presenter; vẽ theo vị trí trang (MenuPageView.Progress): khối sáng
    /// SelectedBG trượt theo, tab càng gần trang đang xem càng tiến lên (tới selectedTabY). Vuốt dở → khối sáng nằm
    /// giữa hai tab, hai tab cùng nhô một nửa — giống gốc.
    /// Gắn trên NavigationTab/TabArea. Mỗi tab cần component Button.
    public class NavigationTabBarView : MonoBehaviour
    {
        [Tooltip("Khối sáng trượt tới tab đang chọn.")]
        [SerializeField] private RectTransform selectedBackground;
        [Tooltip("Các tab theo đúng thứ tự trang (Shop, Home, Achievement).")]
        [SerializeField] private Button[] tabs;

        public event Action<int> TabClicked;

        public int TabCount => tabs.Length;

        private RectTransform[] tabRects;
        private float[] restingY;
        private UnityAction[] clickHandlers;

        public bool Init()
        {
            string missing = "";
            if (selectedBackground == null) missing += " SelectedBackground";
            if (tabs == null || tabs.Length == 0) missing += " Tabs";
            else if (Array.IndexOf(tabs, null) >= 0) missing += " Tabs(có ô trống)";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] NavigationTabBarView thiếu tham chiếu:{missing}. Kéo SelectedBG và Button của từng tab vào.", this);
                return false;
            }
            selectedBackground.gameObject.SetActive(true);
            tabRects = new RectTransform[tabs.Length];
            restingY = new float[tabs.Length];
            clickHandlers = new UnityAction[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                tabRects[i] = (RectTransform)tabs[i].transform;
                // y dựng trong scene = y lúc tab KHÔNG được chọn.
                restingY[i] = tabRects[i].anchoredPosition.y;
                int index = i;
                clickHandlers[i] = () => TabClicked?.Invoke(index);
                tabs[i].onClick.AddListener(clickHandlers[i]);
            }
            return true;
        }

        private void OnDestroy()
        {
            if (clickHandlers == null) return;
            for (int i = 0; i < tabs.Length; i++)
                if (tabs[i] != null) tabs[i].onClick.RemoveListener(clickHandlers[i]);
        }

        /// <paramref name="progress"/>: vị trí trang (số thực). <paramref name="selectedY"/>: y của tab khi được chọn hẳn.
        public void Render(float progress, float selectedY)
        {
            int last = tabRects.Length - 1;
            int left = Mathf.Clamp(Mathf.FloorToInt(progress), 0, last);
            int right = Mathf.Min(left + 1, last);
            float t = Mathf.Clamp01(progress - left);

            // Theo toạ độ thế giới chỉ trục x: SelectedBG và tab nằm dưới hai cha khác nhau, neo khác nhau.
            Vector3 position = selectedBackground.position;
            position.x = Mathf.Lerp(tabRects[left].position.x, tabRects[right].position.x, t);
            selectedBackground.position = position;

            for (int i = 0; i <= last; i++)
            {
                float selectedWeight = Mathf.Clamp01(1f - Mathf.Abs(progress - i));
                Vector2 anchored = tabRects[i].anchoredPosition;
                anchored.y = Mathf.Lerp(restingY[i], selectedY, selectedWeight);
                tabRects[i].anchoredPosition = anchored;
            }
        }
    }
}
