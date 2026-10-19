using System;
using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>Các công tắc người chơi có thể bật/tắt trong màn Cài đặt.</summary>
    public enum SettingKind
    {
        Sound,  // tiếng đặt khối, xóa hàng, combo...
        Music,  // nhạc nền
        Haptic, // rung điện thoại
        Shake   // rung màn hình khi xóa nhiều đường
    }

    /// <summary>
    /// Lưu cài đặt người chơi vào PlayerPrefs. Mặc định tất cả đều bật.
    /// Các hệ thống khác (AudioManager, Haptics, ScreenShake) tự đọc ở đây nên không cần nối dây trong scene.
    /// </summary>
    public static class GameSettings
    {
        private const string KeyPrefix = "BP_Setting_";
        private static readonly bool[] Values = new bool[4];
        private static bool _loaded;

        /// <summary>Bắn khi có công tắc đổi giá trị.</summary>
        public static event Action Changed;

        public static bool SoundOn => Get(SettingKind.Sound);
        public static bool MusicOn => Get(SettingKind.Music);
        public static bool HapticOn => Get(SettingKind.Haptic);
        public static bool ShakeOn => Get(SettingKind.Shake);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loaded = false;
            Changed = null;
        }

        public static bool Get(SettingKind kind)
        {
            Load();
            return Values[(int)kind];
        }

        public static void Set(SettingKind kind, bool value)
        {
            Load();
            if (Values[(int)kind] == value) return;

            Values[(int)kind] = value;
            PlayerPrefs.SetInt(KeyPrefix + kind, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            foreach (SettingKind kind in Enum.GetValues(typeof(SettingKind)))
                Values[(int)kind] = PlayerPrefs.GetInt(KeyPrefix + kind, 1) != 0;
        }
    }
}
