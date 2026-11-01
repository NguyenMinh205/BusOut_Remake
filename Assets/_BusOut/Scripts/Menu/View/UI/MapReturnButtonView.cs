using System;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Menu
{
    /// Nút "về level hiện tại" (ghim) trên trang Home: chỉ hiện khi lăn bản đồ làm bệ level cần chơi khuất khỏi màn hình.
    /// Gắn trên object nút (có Button).
    public class MapReturnButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;

        public event Action Clicked;

        public bool Init()
        {
            if (button == null) button = GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("[BusOut] MapReturnButtonView thiếu Button. Thêm component Button vào nút rồi kéo vào.", this);
                return false;
            }
            button.onClick.AddListener(HandleClick);
            SetVisible(false);
            return true;
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
        }

        public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);

        private void HandleClick() => Clicked?.Invoke();
    }
}
