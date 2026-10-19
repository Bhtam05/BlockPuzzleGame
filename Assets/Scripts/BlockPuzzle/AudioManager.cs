using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>Tên các hiệu ứng âm thanh. File tương ứng: Assets/Resources/Audio/sfx_*.wav.</summary>
    public enum Sfx
    {
        Place,
        Pick,
        Click,
        Clear,
        Combo,
        GameOver
    }

    /// <summary>
    /// Phát hiệu ứng âm thanh và nhạc nền. Tự tạo ra trước khi vào scene (không cần gắn vào object nào),
    /// đọc clip từ thư mục Resources/Audio. Dùng: AudioManager.Play(Sfx.Place).
    /// Tôn trọng công tắc Âm thanh / Nhạc nền trong GameSettings.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private const int PoolSize = 6;
        private const string MusicClipName = "bgm_main";

        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;

        private static AudioManager s_instance;

        private readonly AudioClip[] _clips = new AudioClip[6];
        private AudioSource[] _pool;
        private AudioSource _music;
        private int _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (s_instance != null) return;

            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioManager>();
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_instance = this;

            _clips[(int)Sfx.Place] = Resources.Load<AudioClip>("Audio/sfx_place");
            _clips[(int)Sfx.Pick] = Resources.Load<AudioClip>("Audio/sfx_pick");
            _clips[(int)Sfx.Click] = Resources.Load<AudioClip>("Audio/sfx_click");
            _clips[(int)Sfx.Clear] = Resources.Load<AudioClip>("Audio/sfx_clear");
            _clips[(int)Sfx.Combo] = Resources.Load<AudioClip>("Audio/sfx_combo");
            _clips[(int)Sfx.GameOver] = Resources.Load<AudioClip>("Audio/sfx_gameover");

            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                _pool[i] = gameObject.AddComponent<AudioSource>();
                _pool[i].playOnAwake = false;
            }

            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.volume = musicVolume;
            _music.clip = Resources.Load<AudioClip>("Audio/" + MusicClipName);

            GameSettings.Changed += RefreshMusic;
            RefreshMusic();
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= RefreshMusic;
            if (s_instance == this) s_instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_instance = null;

        /// <summary>Phát hiệu ứng. pitch &gt; 1 cao hơn (dùng để combo càng cao càng "đã").</summary>
        public static void Play(Sfx id, float pitch = 1f, float volume = 1f)
        {
            if (s_instance == null || !GameSettings.SoundOn) return;
            s_instance.PlayInternal(id, pitch, volume);
        }

        private void PlayInternal(Sfx id, float pitch, float volume)
        {
            AudioClip clip = _clips[(int)id];
            if (clip == null) return;

            AudioSource src = _pool[_next];
            _next = (_next + 1) % _pool.Length; // xoay vòng, tiếng cũ bị cắt nếu quá nhiều tiếng chồng nhau

            src.pitch = pitch;
            src.volume = sfxVolume * volume;
            src.clip = clip;
            src.Play();
        }

        private void RefreshMusic()
        {
            if (_music == null || _music.clip == null) return;

            if (GameSettings.MusicOn)
            {
                if (!_music.isPlaying) _music.Play();
            }
            else
            {
                _music.Stop();
            }
        }
    }
}
