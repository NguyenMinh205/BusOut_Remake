using System;
using System.Collections;
using BusOut.DesignPattern;
using UnityEngine;

namespace BusOut.Core
{
    /// Chuyển giữa Menu (HomeContext) và màn chơi (GameContext) trong cùng một scene: bật context này, tắt context kia.
    /// Menu và Gameplay không biết nhau (asmdef chỉ tham chiếu Core) nên chỉ phát sự kiện xin chuyển; ở đây nghe và bật / tắt.
    /// Camera, UI của từng context nằm trong context đó nên bật / tắt theo luôn.
    /// Bật xong context thì phát GameplayShownEvent / HomeShownEvent: GameplayPresenter nạp level đang lưu, MenuPresenter
    /// dựng lại bản đồ (thắng thì xe chạy sang bệ mới). Lần bật đầu tiên của mỗi bên thì Start của nó lo (lúc đó nó chưa nghe).
    /// Gắn trên object GameFlow ở gốc scene (ngoài cả hai context).
    public class GameFlowController : MonoBehaviour
    {
        [Tooltip("HomeContext: Menu.")]
        [SerializeField] private GameObject homeContext;
        [Tooltip("GameContext: màn chơi.")]
        [SerializeField] private GameObject gameContext;
        [Tooltip("Các bước tạo sẵn chạy trong màn Loading (script có IPreloadStep), theo thứ tự: MenuPresenter, GameplayPresenter.")]
        [SerializeField] private MonoBehaviour[] preloadSteps;

        private bool isReady;

        private void Awake()
        {
            if (homeContext == null || gameContext == null)
            {
                Debug.LogError("[BusOut] GameFlowController thiếu Home Context hoặc Game Context: bấm Play / về Home không làm gì. " +
                               "Kéo HomeContext và GameContext vào.", this);
                return;
            }
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

        private void OnEnable()
        {
            EventBus.Subscribe<PlayLevelRequestedEvent>(HandlePlayLevelRequested);
            EventBus.Subscribe<GoHomeRequestedEvent>(HandleGoHomeRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayLevelRequestedEvent>(HandlePlayLevelRequested);
            EventBus.Unsubscribe<GoHomeRequestedEvent>(HandleGoHomeRequested);
        }

        private void HandlePlayLevelRequested(PlayLevelRequestedEvent _)
        {
            if (!isReady) return;
            // ⚠️ Tắt trước rồi mới bật: hai context cùng bật một khung là hai camera cùng vẽ, hai AudioListener cùng nghe.
            homeContext.SetActive(false);
            gameContext.SetActive(true);
            EventBus.Publish(new GameplayShownEvent());
        }

        private void HandleGoHomeRequested(GoHomeRequestedEvent _)
        {
            if (!isReady) return;
            ShowHome();
        }

        private void ShowHome()
        {
            gameContext.SetActive(false);
            homeContext.SetActive(true);
            EventBus.Publish(new HomeShownEvent());
        }
    }
}
