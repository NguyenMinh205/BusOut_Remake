#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace BusOut.Core
{
    /// Dòng chữ "ms · fps · khung nặng nhất" ở góc trên trái, để xem nhanh hiệu năng khi chơi thử.
    /// Chỉ có trong Editor và bản Development Build (bản phát hành không biên dịch file này).
    /// Editor: bật / tắt bằng Tools → BusOut Tools → Hiện FPS (nhớ qua các lần Play). Development Build: luôn hiện —
    /// lúc cài lên máy thật là lúc cần số nhất.
    /// Tự dựng khi vào Play (không cần gắn vào scene), sống suốt phiên, kể cả khi chạy thẳng GameScene.
    public class FpsOverlay : MonoBehaviour
    {
        /// Khoá PlayerPrefs của công tắc trong Editor (menu BusOut Tools ghi, overlay đọc).
        public const string EditorPrefKey = "BusOut.ShowFpsOverlay";

        // Số của công cụ đo, không phải số chơi game nên không đưa vào config.
        // Gộp nửa giây một lần: số đổi mỗi khung thì không đọc kịp, và chỉ dựng chuỗi 2 lần / giây (không cấp phát mỗi khung).
        private const float SampleInterval = 0.5f;
        private const float FontSizeOfScreenHeight = 0.022f;
        private const float MarginOfScreenHeight = 0.01f;

        private static FpsOverlay instance;

        private float elapsed;
        private int frames;
        private float worstFrame;
        private string text = "";
        private GUIStyle style;
        private GUIStyle shadowStyle;
        private int styledScreenHeight;
        private readonly GUIContent content = new GUIContent();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnPlay()
        {
            if (instance != null) return;
            var holder = new GameObject("[Debug] FpsOverlay");
            DontDestroyOnLoad(holder);
            instance = holder.AddComponent<FpsOverlay>();
            instance.enabled = IsWanted();
        }

        /// Menu Editor gọi khi đổi công tắc lúc đang Play: hiện / ẩn ngay, không phải Play lại.
        public static void SetVisible(bool visible)
        {
            if (instance != null) instance.enabled = visible;
        }

        private static bool IsWanted()
        {
#if UNITY_EDITOR
            return PlayerPrefs.GetInt(EditorPrefKey, 0) == 1;
#else
            return true;
#endif
        }

        private void Awake() => useGUILayout = false; // Không dùng GUILayout → Unity bỏ lượt Layout của OnGUI.

        private void OnEnable()
        {
            elapsed = 0f;
            frames = 0;
            worstFrame = 0f;
            text = "";
        }

        private void Update()
        {
            // unscaled: đo thời gian thật của khung, kể cả lúc game chỉnh timeScale.
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            frames++;
            if (dt > worstFrame) worstFrame = dt;
            if (elapsed < SampleInterval) return;

            float averageMs = elapsed / frames * 1000f;
            text = $"{averageMs:0.0} ms · {frames / elapsed:0} fps · max {worstFrame * 1000f:0} ms";
            elapsed = 0f;
            frames = 0;
            worstFrame = 0f;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || text.Length == 0) return;
            if (style == null || styledScreenHeight != Screen.height) BuildStyles();

            // Screen.safeArea tính y từ đáy, GUI tính từ đỉnh → đổi về toạ độ GUI để chữ không nằm dưới tai thỏ.
            Rect safe = Screen.safeArea;
            float margin = Screen.height * MarginOfScreenHeight;
            content.text = text;
            Vector2 size = style.CalcSize(content);
            var rect = new Rect(safe.xMin + margin, Screen.height - safe.yMax + margin, size.x, size.y);

            // Bóng đen lệch 1–2 px: chữ đọc được cả trên nền sáng (mặt đường) lẫn nền tối.
            float shadow = Mathf.Max(1f, style.fontSize * 0.08f);
            GUI.Label(new Rect(rect.x + shadow, rect.y + shadow, rect.width, rect.height), content, shadowStyle);
            GUI.Label(rect, content, style);
        }

        private void BuildStyles()
        {
            styledScreenHeight = Screen.height;
            int fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * FontSizeOfScreenHeight));
            style = new GUIStyle { fontSize = fontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
            style.normal.textColor = Color.yellow;
            shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = Color.black;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
#endif
