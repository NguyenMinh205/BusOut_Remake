using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Menu
{
    /// Nút Play ở trang Home: hiện level sẽ chơi, bấm → vào ván.
    /// Gắn trên HomePanel/CenterUI/Buttons/PlayButton.
    public class PlayButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [Tooltip("PlayButton/LevelTxt.")]
        [SerializeField] private TMP_Text levelText;

        public event Action Clicked;

        public bool Init()
        {
            if (button == null) button = GetComponent<Button>();
            string missing = "";
            if (button == null) missing += " Button";
            if (levelText == null) missing += " LevelText";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] PlayButtonView thiếu tham chiếu:{missing}. Kéo Button và LevelTxt của nút Play vào.", this);
                return false;
            }
            button.onClick.AddListener(HandleClick);
            return true;
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
        }

        public void SetLevel(string format, int level) => levelText.SetText(format, level);

        private void HandleClick() => Clicked?.Invoke();
    }
}
