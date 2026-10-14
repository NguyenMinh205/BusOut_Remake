using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusOut.DesignPattern
{
    public interface IEvent { }

    /// Kênh riêng cho từng kiểu sự kiện: mỗi T là một class static riêng, tìm listener không cần Dictionary.
    /// Sự kiện là struct nên Publish không cấp phát rác.
    public static class EventBus<T> where T : struct, IEvent
    {
        private static Action<T> listeners;

        // Static constructor chạy một lần cho mỗi T, lần đầu T được dùng: đăng ký để EventBus xoá được khi bấm Play.
        static EventBus() => EventBus.RegisterChannel(Clear);

        public static bool HasListeners => listeners != null;

        public static void Subscribe(Action<T> listener)
        {
            if (listener == null) return;
            // Gỡ trước rồi mới thêm: Subscribe 2 lần (vd. OnEnable chạy lại) vẫn chỉ nhận sự kiện 1 lần.
            listeners -= listener;
            listeners += listener;
        }

        public static void Unsubscribe(Action<T> listener) => listeners -= listener;

        /// ⚠️ Một listener ném exception thì các listener sau nó KHÔNG nhận được sự kiện này. Listener tự bắt lỗi của mình.
        public static void Publish(T eventData)
        {
#if UNITY_EDITOR
            if (EventBus.LogEnabled) Debug.Log($"[EventBus] {typeof(T).Name}");
#endif
            listeners?.Invoke(eventData);
        }

        public static void Clear() => listeners = null;
    }

    /// API gọi trong game: <c>EventBus.Publish(new LevelStartedEvent { LevelIndex = 3 });</c>
    /// Subscribe ở OnEnable, Unsubscribe ở OnDisable (hoặc OnDestroy). Quên Unsubscribe là object đã huỷ vẫn bị gọi.
    public static class EventBus
    {
        private static readonly List<Action> channelClears = new List<Action>();

#if UNITY_EDITOR
        /// Bật trong Editor để in tên mọi sự kiện được Publish (dò luồng game). Mặc định tắt.
        public static bool LogEnabled;
#endif

        public static void Subscribe<T>(Action<T> listener) where T : struct, IEvent => EventBus<T>.Subscribe(listener);

        public static void Unsubscribe<T>(Action<T> listener) where T : struct, IEvent => EventBus<T>.Unsubscribe(listener);

        public static void Publish<T>(T eventData) where T : struct, IEvent => EventBus<T>.Publish(eventData);

        public static void Clear<T>() where T : struct, IEvent => EventBus<T>.Clear();

        /// Xoá listener của mọi kênh đã từng được dùng.
        public static void ClearAll()
        {
            for (int i = 0; i < channelClears.Count; i++) channelClears[i]();
        }

        internal static void RegisterChannel(Action clear) => channelClears.Add(clear);

        // ⚠️ Khi tắt Domain Reload, static giữ nguyên giữa các lần Play: listener của lần trước (object đã huỷ) vẫn bị gọi.
        // Danh sách channelClears KHÔNG được xoá: static constructor của từng kênh không chạy lại để đăng ký lần nữa.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => ClearAll();
    }
}
