using UnityEngine;

namespace BusOut.DesignPattern
{
    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        private static T instance;

        [Header("Singleton")]
        [Tooltip("Giữ lại khi đổi scene. Chỉ có tác dụng khi object nằm ở gốc scene (không có cha).")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        public static bool HasInstance => instance != null;

        public static T Instance
        {
            get
            {
                if (instance != null) return instance;
                if (SingletonState.IsQuitting) return null;

                instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);
                if (instance == null)
                {
                    var holder = new GameObject($"[Singleton] {typeof(T).Name}");
                    instance = holder.AddComponent<T>();
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"[Singleton] Thừa một {typeof(T).Name} trên '{name}', huỷ bản này. Chỉ để một bản trong scene.", this);
                Destroy(gameObject);
                return;
            }

            instance = (T)this;
            if (dontDestroyOnLoad)
            {
                if (transform.parent == null) DontDestroyOnLoad(gameObject);
                else Debug.LogWarning($"[Singleton] {typeof(T).Name} có cha nên KHÔNG giữ được qua đổi scene. Đưa ra gốc scene hoặc tắt Dont Destroy On Load.", this);
            }
            OnSingletonAwake();
        }

        protected virtual void OnSingletonAwake() { }

        protected virtual void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }

    /// Cờ tắt game dùng chung cho mọi Singleton<T>; (trường static trong class generic không gắn được RuntimeInitializeOnLoadMethod).
    internal static class SingletonState
    {
        public static bool IsQuitting { get; private set; }

        // ⚠️ Chạy cả khi tắt Domain Reload (Enter Play Mode Options): không reset thì sau lần Play đầu
        // IsQuitting kẹt true và mọi Instance trả null.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            IsQuitting = false;
            Application.quitting -= MarkQuitting;
            Application.quitting += MarkQuitting;
        }

        private static void MarkQuitting() => IsQuitting = true;
    }
}
