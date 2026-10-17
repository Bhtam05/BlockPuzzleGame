using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Thiết lập chạy trên điện thoại, tự chạy một lần trước khi vào scene (không cần gắn vào object nào).
    /// - 60 FPS: mobile mặc định chỉ 30 FPS nên kéo thả sẽ thấy giật.
    /// - Không cho máy tự tắt màn hình khi đang chơi.
    /// </summary>
    public static class AppSettings
    {
        private const int TargetFps = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            QualitySettings.vSyncCount = 0; // để targetFrameRate có hiệu lực
            Application.targetFrameRate = TargetFps;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
