using System;
using BusOut.DesignPattern;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BusOut.Core
{
    /// Phát nhạc nền và tiếng hiệu ứng theo công tắc Music / Sound (đọc từ save lúc dựng, nghe SettingsChangedEvent khi đổi).
    /// Nhạc nền chỉ chạy ở Menu (màn Loading + HomeContext): GameplayShownEvent → dừng, HomeShownEvent → chạy tiếp.
    /// Sống suốt game: GameBootstrap dựng ngay sau save; chạy thẳng GameScene trong Editor thì GameFlowController gọi tới lần
    /// đầu là tự dựng. Mọi phần gọi thẳng: AudioService.Play(SoundId.HitCar).
    /// Tiếng là 2D (không theo vị trí): camera nhìn từ trên cao, mọi thứ phát tiếng đều nằm trong khung hình.
    public class AudioService : Singleton<AudioService>
    {
        private AudioConfig config;
        private AudioSource musicSource;
        // Một nguồn cho mọi tiếng hiệu ứng: PlayOneShot chồng được nhiều tiếng cùng lúc, không cần pool nguồn.
        private AudioSource soundSource;
        // Tra theo (int)SoundId: không tìm trong List mỗi lần phát.
        private AudioClip[] clips;
        private float[] volumes;
        private bool[] missingReported;
        private bool soundOn;
        private bool musicOn;
        // Nhạc nền chỉ ở Menu: trong ván im nhạc (bạn chốt 3/11/2026), về Menu chạy tiếp.
        private bool inGameplay;
        // Nhạc đang dừng tạm do mình (tắt Music / vào ván): bật lại thì UnPause để chạy tiếp chỗ cũ.
        private bool isMusicPaused;
        // Dựng sẵn một delegate dùng chung cho mọi nút.
        private UnityAction onButtonClicked;

        /// Phát một tiếng hiệu ứng (đang tắt Sound thì im). Gọi được từ mọi phần, không cần giữ tham chiếu.
        public static void Play(SoundId id)
        {
            // Lúc tắt game Instance trả null.
            AudioService service = Instance;
            if (service != null) service.PlaySound(id);
        }

        /// Quảng cáo toàn màn đang mở: dừng MỌI tiếng của game (AudioListener.pause — cả nhạc lẫn tiếng hiệu ứng, kể cả tiếng
        /// đang phát dở), đóng thì chạy tiếp. Không đụng công tắc Music / Sound của người chơi. AdService gọi.
        public static void SetPausedByAd(bool paused) => AudioListener.pause = paused;

        protected override void OnSingletonAwake()
        {
            config = Resources.Load<AudioConfig>(AudioConfig.ResourcePath);
            if (config == null)
            {
                Debug.LogError($"[BusOut] Không thấy Resources/{AudioConfig.ResourcePath}.asset: game không có tiếng. Tạo bằng " +
                               "Create → BusOut → Audio Config, đặt trong Assets/_BusOut/Resources, kéo clip vào.", this);
                config = AudioConfig.CreateDefault();
            }
            BuildLookup();

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.clip = config.backgroundMusic;
            musicSource.volume = config.musicVolume;
            if (config.backgroundMusic == null)
                Debug.LogError("[BusOut] AudioConfig chưa có Background Music: game không có nhạc nền. Kéo clip vào " +
                               $"Resources/{AudioConfig.ResourcePath}.asset.", config);

            soundSource = gameObject.AddComponent<AudioSource>();
            soundSource.playOnAwake = false;
            onButtonClicked = PlayTap;

            // Save đọc trước (GameBootstrap dựng DataManager trước; thiếu thì Instance tự dựng).
            SecureGameData data = DataManager.Instance.GameData;
            ApplySettings(data.musicOn, data.sfxOn);
            EventBus.Subscribe<SettingsChangedEvent>(HandleSettingsChanged);
            EventBus.Subscribe<GameplayShownEvent>(HandleGameplayShown);
            EventBus.Subscribe<HomeShownEvent>(HandleHomeShown);
        }

        protected override void OnDestroy()
        {
            EventBus.Unsubscribe<SettingsChangedEvent>(HandleSettingsChanged);
            EventBus.Unsubscribe<GameplayShownEvent>(HandleGameplayShown);
            EventBus.Unsubscribe<HomeShownEvent>(HandleHomeShown);
            base.OnDestroy();
        }

        /// Gắn tiếng bấm cho mọi Button trong scene (kể cả object đang tắt), khỏi gắn tay từng nút.
        /// GameFlowController gọi một lần lúc nạp GameScene. ⚠️ Nút sinh ra SAU lúc này (Instantiate) không có tiếng: gọi
        /// AudioService.Play(SoundId.TapButton) trong code của nút đó.
        public void AddButtonSounds(Scene scene)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    button.onClick.AddListener(onButtonClicked);
                    count++;
                }
            }
            GameLog.Info($"[BusOut] AudioService: gắn tiếng bấm cho {count} nút trong {scene.name}.");
        }

        private void PlayTap() => PlaySound(SoundId.TapButton);

        private void PlaySound(SoundId id)
        {
            if (!soundOn) return;
            int index = (int)id;
            if (index < 0 || index >= clips.Length) return;
            AudioClip clip = clips[index];
            if (clip == null)
            {
                // Báo một lần mỗi tiếng: tiếng phát mỗi 0.1s (khách lên xe) mà báo mỗi lần là ngập Console.
                if (!missingReported[index])
                {
                    missingReported[index] = true;
                    Debug.LogError($"[BusOut] AudioConfig chưa có clip cho tiếng {id}: tiếng này im. Kéo clip vào " +
                                   $"Resources/{AudioConfig.ResourcePath}.asset › Sounds.", config);
                }
                return;
            }
            soundSource.PlayOneShot(clip, volumes[index] * config.soundVolume);
        }

        private void BuildLookup()
        {
            int count = Enum.GetValues(typeof(SoundId)).Length;
            clips = new AudioClip[count];
            volumes = new float[count];
            missingReported = new bool[count];
            if (config.sounds == null) return;
            var assigned = new bool[count];
            foreach (SoundEntry entry in config.sounds)
            {
                if (entry == null) continue;
                int index = (int)entry.id;
                if (index < 0 || index >= count) continue;
                if (assigned[index])
                {
                    Debug.LogError($"[BusOut] AudioConfig có hai dòng cho tiếng {entry.id}: dùng dòng đầu. Xoá dòng thừa.", config);
                    continue;
                }
                assigned[index] = true;
                clips[index] = entry.clip;
                volumes[index] = entry.volume;
            }
        }

        private void HandleSettingsChanged(SettingsChangedEvent settings) => ApplySettings(settings.MusicOn, settings.SoundOn);

        private void HandleGameplayShown(GameplayShownEvent _)
        {
            inGameplay = true;
            UpdateMusic();
        }

        private void HandleHomeShown(HomeShownEvent _)
        {
            inGameplay = false;
            UpdateMusic();
        }

        private void ApplySettings(bool isMusicOn, bool isSoundOn)
        {
            soundOn = isSoundOn;
            // Tắt Sound giữa chừng: cắt luôn tiếng đang vang (tiếng thắng / thua dài vài giây).
            if (!soundOn) soundSource.Stop();
            musicOn = isMusicOn;
            UpdateMusic();
        }

        // Kiểm mỗi khung (rẻ: vài phép so) thay vì tin một lần Play() lúc khởi động: xem UpdateMusic.
        private void Update() => UpdateMusic();

        /// Đưa nhạc về đúng trạng thái: chạy khi bật Music và không trong ván, dừng tạm khi ngược lại.
        /// ⚠️ Tự sửa sai mỗi khung, không chỉ khi có sự kiện: clip nạp nền (Load In Background, Preload Audio Data tắt) mà gọi
        /// Play() lúc chưa nạp xong (màn Loading) thì có thể không phát — trước đây code tin là "đã chạy", về sau chỉ UnPause
        /// (không bắt đầu được bài chưa từng chạy) → vào game / về Menu im lặng tới khi bật lại công tắc Music (lỗi 4/11/2026).
        private void UpdateMusic()
        {
            AudioClip clip = musicSource.clip;
            if (clip == null) return;
            bool shouldPlay = musicOn && !inGameplay;
            if (!shouldPlay)
            {
                // Tạm dừng thay vì Stop: bật lại / về Menu thì nhạc chạy tiếp, không nghe lại từ đầu.
                if (musicSource.isPlaying)
                {
                    musicSource.Pause();
                    isMusicPaused = true;
                }
                return;
            }
            if (musicSource.isPlaying) return;
            if (isMusicPaused)
            {
                isMusicPaused = false;
                musicSource.UnPause();
                if (musicSource.isPlaying) return;
            }
            // Chưa nạp xong thì chờ khung sau (gọi Play lúc đang nạp có thể bị bỏ qua); lỗi nạp thì thôi, khỏi thử mỗi khung.
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            if (clip.loadState == AudioDataLoadState.Loaded) musicSource.Play();
        }
    }
}
