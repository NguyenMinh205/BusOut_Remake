using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusOut.Core
{
    /// Âm thanh của game: nhạc nền, clip + âm lượng từng tiếng hiệu ứng.
    /// AudioService đọc qua Resources (tự dựng, không có ô kéo) → asset phải nằm đúng Resources/AudioConfig.asset.
    [CreateAssetMenu(menuName = "BusOut/Audio Config", fileName = "AudioConfig")]
    public class AudioConfig : ScriptableObject
    {
        public const string ResourcePath = "AudioConfig";

        [Header("Nhạc nền")]
        [Tooltip("Nhạc nền phát lặp suốt game (Menu lẫn màn chơi).")]
        public AudioClip backgroundMusic;

        [Tooltip("Âm lượng nhạc nền (0..1).")]
        [Range(0f, 1f)] public float musicVolume = 0.5f;

        [Header("Tiếng hiệu ứng")]
        [Tooltip("Âm lượng chung mọi tiếng hiệu ứng (0..1), nhân với âm lượng riêng từng tiếng.")]
        [Range(0f, 1f)] public float soundVolume = 1f;

        [Tooltip("Clip từng tiếng, mỗi tiếng một dòng. Thiếu clip thì tiếng đó im và báo lỗi một lần lúc phát.")]
        public List<SoundEntry> sounds = new List<SoundEntry>
        {
            new SoundEntry { id = SoundId.TapButton },
            new SoundEntry { id = SoundId.HitCar },
            new SoundEntry { id = SoundId.CarMoveToBusStop },
            new SoundEntry { id = SoundId.CarLeaveBusStop },
            new SoundEntry { id = SoundId.PassengerGetInCar },
            new SoundEntry { id = SoundId.WinGame },
            new SoundEntry { id = SoundId.LoseGame },
            new SoundEntry { id = SoundId.Coin },
            new SoundEntry { id = SoundId.BonusCoin },
            new SoundEntry { id = SoundId.Helicopter },
            new SoundEntry { id = SoundId.StreakComboGood },
            new SoundEntry { id = SoundId.StreakComboNice },
            new SoundEntry { id = SoundId.StreakComboGoodJob },
            new SoundEntry { id = SoundId.StreakComboFantastic },
        };

        public static AudioConfig CreateDefault() => CreateInstance<AudioConfig>();
    }

    [Serializable]
    public class SoundEntry
    {
        public SoundId id;
        public AudioClip clip;

        [Tooltip("Âm lượng riêng tiếng này (0..1): cân các clip to / nhỏ khác nhau.")]
        [Range(0f, 1f)] public float volume = 1f;
    }
}
