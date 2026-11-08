using System;
using System.Collections;
using BusOut.DesignPattern;
using DG.Tweening;
using UnityEngine;

namespace BusOut.Core
{
    /// Chuyển giữa Menu (HomeContext) và màn chơi (GameContext) trong cùng một scene: bật context này, tắt context kia.
    /// Menu và Gameplay không biết nhau (asmdef chỉ tham chiếu Core) nên chỉ phát sự kiện xin chuyển; ở đây nghe và bật / tắt.
    /// Camera, UI của từng context nằm trong context đó nên bật / tắt theo luôn.
    /// Mỗi lần chuyển: tranh Loading hiện dần che kín (ScreenTransitionView) → dưới màn che bật / tắt context + phát
    /// GameplayShownEvent / HomeShownEvent (GameplayPresenter nạp level, MenuPresenter dựng lại bản đồ) → giữ đủ lâu →
    /// ScreenRevealedEvent (bắt đầu hoạt ảnh: xe "bung", xe chạy sang bệ mới) → màn che mờ đi.
    /// Lần bật đầu tiên của mỗi bên thì Start của nó lo (lúc đó nó chưa nghe); lần vào game đầu tiên màn Loading của
    /// GameBootstrap che, không qua màn chuyển cảnh.
    /// Gắn trên object GameFlow ở gốc scene (ngoài cả hai context).
    public class GameFlowController : MonoBehaviour
    {
        [Tooltip("HomeContext: Menu.")]
        [SerializeField] private GameObject homeContext;
        [Tooltip("GameContext: màn chơi.")]
        [SerializeField] private GameObject gameContext;
        [Tooltip("Các bước tạo sẵn chạy trong màn Loading (script có IPreloadStep), theo thứ tự: MenuPresenter, GameplayPresenter.")]
        [SerializeField] private MonoBehaviour[] preloadSteps;
        [Tooltip("Màn che chuyển cảnh (SharedUI/TransitionPanel). Trống = đổi cảnh tức thì.")]
        [SerializeField] private ScreenTransitionView transition;
        [Tooltip("Assets/_BusOut/Resources/UIConfig.asset (thời gian chuyển cảnh).")]
        [SerializeField] private UIConfig uiConfig;

        private bool isReady;
        private bool hasTransition;
        // Đang chuyển: bỏ qua yêu cầu chuyển khác (bấm đúp Play / nhận thưởng) — chạy hai lần là bật / tắt context lẫn lộn.
        private bool isTransitioning;

        private void Awake()
        {
            // Mọi object của scene đã có từ trước Awake: gắn tiếng bấm cho mọi nút một lần ở đây. Chạy thẳng GameScene
            // (không qua GameBootstrap) thì lệnh này cũng là lúc AudioService tự dựng.
            AudioService.Instance.AddButtonSounds(gameObject.scene);
            if (homeContext == null || gameContext == null)
            {
                Debug.LogError("[BusOut] GameFlowController thiếu Home Context hoặc Game Context: bấm Play / về Home không làm gì. " +
                               "Kéo HomeContext và GameContext vào.", this);
                return;
            }
            if (uiConfig == null)
            {
                Debug.LogError("[BusOut] GameFlowController thiếu UI Config: chuyển cảnh chạy số mặc định trong code. " +
                               "Kéo Assets/_BusOut/Resources/UIConfig.asset vào.", this);
                uiConfig = UIConfig.CreateDefault();
            }
            // Cùng lúc với tiếng bấm: mọi nút của scene đã có, gắn nhún khi bấm một lần.
            int pressCount = ButtonPressFeedback.AttachAll(gameObject.scene, FxConfig.Instance.buttonPressScale,
                FxConfig.Instance.buttonPressDuration);
            GameLog.Info($"[BusOut] Gắn nhún khi bấm cho {pressCount} nút.");
            if (transition == null)
                Debug.LogError("[BusOut] GameFlowController thiếu Transition: chuyển Menu ↔ màn chơi tức thì, không có màn " +
                               "Loading. Kéo SharedUI/TransitionPanel (ScreenTransitionView) vào.", this);
            else hasTransition = transition.Init();
            isReady = true;
            // Vào game luôn ở Menu, dù scene đang lưu context nào bật (hay bật GameContext để thử trong Editor).
            ShowHome();
        }

        /// Các bước tạo sẵn chạy trong lúc màn Loading còn che (GameBootstrap gọi sau khi nạp xong GameScene), lần lượt, mỗi
        /// bước một phần bằng nhau của thanh. <paramref name="reportProgress"/>: tiến độ 0..1 của cả phần này.
        public IEnumerator Preload(Action<float> reportProgress)
        {
            int count = preloadSteps?.Length ?? 0;
            for (int i = 0; i < count; i++)
            {
                if (!(preloadSteps[i] is IPreloadStep step))
                {
                    Debug.LogError($"[BusOut] GameFlowController: Preload Steps[{i}] trống hoặc không phải bước tạo sẵn " +
                                   "(IPreloadStep): bỏ qua. Kéo MenuPresenter / GameplayPresenter vào.", this);
                    continue;
                }
                int index = i;
                yield return step.Preload(stepProgress => reportProgress?.Invoke((index + Mathf.Clamp01(stepProgress)) / count));
            }
            reportProgress?.Invoke(1f);
        }

        /// Mở game vào thẳng ván (người chơi mới): đổi context dưới màn Loading đầu game, không qua màn che chuyển cảnh.
        /// GameBootstrap gọi ngay trước khi màn Loading mờ, nên phát luôn ScreenRevealedEvent để xe "bung" lúc người chơi thấy.
        public void EnterGameplayUnderLoading()
        {
            if (!isReady) return;
            ShowGameplay();
            EventBus.Publish(new ScreenRevealedEvent());
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PlayLevelRequestedEvent>(HandlePlayLevelRequested);
            EventBus.Subscribe<GoHomeRequestedEvent>(HandleGoHomeRequested);
            EventBus.Subscribe<LoadingTipsChangedEvent>(HandleLoadingTipsChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayLevelRequestedEvent>(HandlePlayLevelRequested);
            EventBus.Unsubscribe<GoHomeRequestedEvent>(HandleGoHomeRequested);
            EventBus.Unsubscribe<LoadingTipsChangedEvent>(HandleLoadingTipsChanged);
        }

        // Màn che là của GameFlowController (tham chiếu trực tiếp: cần chờ hiện / mờ xong); trang Loading thì ai có dữ liệu
        // gửi qua sự kiện, không phải kéo màn che vào từng nơi.
        private void HandleLoadingTipsChanged(LoadingTipsChangedEvent e)
        {
            if (hasTransition) transition.SetTips(e.Tips);
        }

        private void HandlePlayLevelRequested(PlayLevelRequestedEvent _)
        {
            if (!isReady || isTransitioning) return;
            StartCoroutine(SwitchContext(true));
        }

        private void HandleGoHomeRequested(GoHomeRequestedEvent _)
        {
            if (!isReady || isTransitioning) return;
            StartCoroutine(SwitchContext(false));
        }

        private IEnumerator SwitchContext(bool toGameplay)
        {
            isTransitioning = true;
            if (hasTransition) yield return transition.FadeIn(uiConfig.transitionFadeDuration).WaitForCompletion();
            float coveredTime = Time.unscaledTime;
            if (toGameplay) ShowGameplay();
            else ShowHome();
            // Giữ tối thiểu tính từ lúc che kín (không tính lúc hiện dần): tranh đứng yên đủ lâu để đọc.
            while (hasTransition && Time.unscaledTime - coveredTime < uiConfig.transitionMinHoldDuration) yield return null;
            // ⚠️ Phát lúc màn che BẮT ĐẦU mờ (còn che kín), không đợi mờ xong: lúc mờ dần đã lộ cảnh sau, xe đang to đủ rồi
            // mới thu về 0 để "bung" là thấy giật.
            EventBus.Publish(new ScreenRevealedEvent());
            if (hasTransition) yield return transition.FadeOut(uiConfig.transitionFadeDuration).WaitForCompletion();
            isTransitioning = false;
        }

        private void ShowGameplay()
        {
            // ⚠️ Tắt trước rồi mới bật: hai context cùng bật một khung là hai camera cùng vẽ, hai AudioListener cùng nghe.
            homeContext.SetActive(false);
            gameContext.SetActive(true);
            EventBus.Publish(new GameplayShownEvent());
        }

        private void ShowHome()
        {
            gameContext.SetActive(false);
            homeContext.SetActive(true);
            EventBus.Publish(new HomeShownEvent());
        }
    }
}
