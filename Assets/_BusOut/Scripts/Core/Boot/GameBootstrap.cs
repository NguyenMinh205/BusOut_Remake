using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BusOut.Core
{
    /// Điểm vào của game và chỗ giữ những thứ sống suốt phiên chơi (sau này: âm thanh, quảng cáo…).
    /// Gắn trên object GameBootstrap ở gốc LoadingScene (scene đầu tiên trong Build Settings); tự giữ qua lần đổi scene.
    /// Mở game: đặt fps, khởi tạo DOTween, nạp save → nạp GameScene (thay LoadingScene) → GameFlowController.Preload
    /// (các bước tạo sẵn) → màn Loading mờ dần rồi bị xoá, lộ Menu.
    /// Chạy thẳng GameScene trong Editor (không qua LoadingScene) thì không có object này: GameFlowController tự lo phần
    /// của nó (xem GameBootstrap.IsActive).
    public class GameBootstrap : MonoBehaviour
    {
        // Unity báo AsyncOperation.progress tối đa 0.9 cho tới khi scene được bật.
        private const float SceneLoadDoneProgress = 0.9f;

        [SerializeField] private BootConfig config;
        [Tooltip("LoadingView trên Canvas gốc của LoadingScene.")]
        [SerializeField] private LoadingView loadingView;

        /// Game mở qua LoadingScene (có GameBootstrap). False khi chạy thẳng GameScene trong Editor.
        public static bool IsActive { get; private set; }

        private bool hasLoadingView;
        private bool isLoading;
        private float targetProgress;
        private float shownProgress;

        // Tắt Domain Reload (Enter Play Mode Options): biến static giữ giá trị của lần Play trước → reset.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => IsActive = false;

        private void Awake()
        {
            // Nạp lại LoadingScene (không nên xảy ra) → không dựng lần hai, giữ bản đang sống.
            if (IsActive)
            {
                Destroy(gameObject);
                return;
            }
            IsActive = true;
            DontDestroyOnLoad(gameObject);
            if (config == null)
            {
                Debug.LogError("[BusOut] GameBootstrap thiếu Boot Config: đang chạy số mặc định trong code. " +
                               "Kéo Assets/_BusOut/Resources/BootConfig.asset vào.", this);
                config = BootConfig.CreateDefault();
            }
            if (config.targetFrameRate > 0) Application.targetFrameRate = config.targetFrameRate;
            // Khởi tạo sẵn: tween đầu tiên của game không phải gánh việc DOTween tự dựng.
            DOTween.Init();
            // Nạp save ngay (DataManager tự dựng object + đọc file lần đầu gọi), không để tới lúc Menu cần.
            _ = DataManager.Instance.GameData;
        }

        private void Start()
        {
            if (loadingView == null)
                Debug.LogError("[BusOut] GameBootstrap thiếu Loading View: vẫn vào game nhưng không có màn Loading che. " +
                               "Kéo Canvas (có LoadingView) của LoadingScene vào.", this);
            else hasLoadingView = loadingView.Init();
            // Màn Loading sống qua lần đổi scene để che GameScene đang dựng; xong thì xoá.
            if (hasLoadingView) DontDestroyOnLoad(loadingView.transform.root.gameObject);
            StartCoroutine(Boot());
        }

        private void Update()
        {
            if (!isLoading || !hasLoadingView) return;
            shownProgress = Mathf.MoveTowards(shownProgress, targetProgress, config.loadingBarFillSpeed * Time.unscaledDeltaTime);
            loadingView.SetProgress(shownProgress);
        }

        private IEnumerator Boot()
        {
            isLoading = true;
            float startTime = Time.unscaledTime;
            float sceneShare = Mathf.Clamp01(config.sceneLoadProgressShare);

            // Single: LoadingScene (camera, đèn, EventSystem của nó) tự bị gỡ; GameScene thành scene chính nên trời / ánh
            // sáng (RenderSettings) của nó dùng được ngay.
            AsyncOperation load = SceneManager.LoadSceneAsync(config.gameSceneName, LoadSceneMode.Single);
            if (load == null)
            {
                Debug.LogError($"[BusOut] Không nạp được scene \"{config.gameSceneName}\": chưa có trong Build Settings hoặc sai tên " +
                               "(BootConfig.gameSceneName).", this);
                yield break;
            }
            while (!load.isDone)
            {
                ReportProgress(Mathf.Clamp01(load.progress / SceneLoadDoneProgress) * sceneShare);
                yield return null;
            }

            // Awake / OnEnable của GameScene đã chạy; đợi một khung cho Start của các object chạy xong.
            yield return null;
            GameFlowController flow = FindFirstObjectByType<GameFlowController>();
            if (flow == null)
                Debug.LogError($"[BusOut] {config.gameSceneName} không có GameFlowController: bỏ qua các bước tạo sẵn.", this);
            else yield return flow.Preload(stepProgress => ReportProgress(sceneShare + stepProgress * (1f - sceneShare)));

            ReportProgress(1f);
            // Thanh chạy đủ và màn hiện đủ lâu rồi mới mờ: nạp nhanh quá thì màn chớp qua, người chơi không kịp thấy.
            while (hasLoadingView && (shownProgress < 1f || Time.unscaledTime - startTime < config.loadingMinDuration))
                yield return null;
            isLoading = false;

            if (!hasLoadingView) yield break;
            yield return loadingView.FadeOut(config.loadingFadeDuration).WaitForCompletion();
            Destroy(loadingView.transform.root.gameObject);
        }

        private void ReportProgress(float progress) => targetProgress = Mathf.Max(targetProgress, Mathf.Clamp01(progress));
    }
}
