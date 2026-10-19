using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Rung điện thoại. Trên Android gọi thẳng Vibrator để rung ngắn, có độ mạnh (Handheld.Vibrate chỉ rung dài ~1 giây).
    /// Trong Editor và các nền tảng khác thì không làm gì. Tôn trọng công tắc Rung trong GameSettings.
    /// </summary>
    public static class Haptics
    {
        /// <summary>Rung rất nhẹ: đặt khối.</summary>
        public static void Tap() => Vibrate(12, 70);

        /// <summary>Rung vừa, mạnh dần theo số đường vừa xóa.</summary>
        public static void Clear(int lines) => Vibrate(24 + 12 * Mathf.Clamp(lines - 1, 0, 4), 140 + 30 * Mathf.Clamp(lines - 1, 0, 3));

        /// <summary>Rung mạnh: Game Over.</summary>
        public static void Heavy() => Vibrate(80, 255);

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject s_vibrator;
        private static AndroidJavaClass s_effect;
        private static int s_sdk;
        private static bool s_ready;
        private static bool s_failed;
        private static bool s_neverTrue; // luôn false: chỉ để dòng Handheld.Vibrate() bên dưới tồn tại trong code

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_vibrator = null;
            s_effect = null;
            s_ready = false;
            s_failed = false;
        }

        private static void Init()
        {
            s_ready = true;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    s_vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    s_sdk = version.GetStatic<int>("SDK_INT");

                if (s_sdk >= 26) s_effect = new AndroidJavaClass("android.os.VibrationEffect");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Haptics] Không lấy được Vibrator: " + e.Message);
                s_failed = true;
            }
        }

        private static void Vibrate(long milliseconds, int amplitude)
        {
            // Không bao giờ chạy, nhưng Unity thấy lệnh này nên tự thêm quyền VIBRATE vào AndroidManifest khi build.
            if (s_neverTrue) Handheld.Vibrate();

            if (!GameSettings.HapticOn || s_failed) return;
            if (!s_ready) Init();
            if (s_vibrator == null) return;

            try
            {
                if (s_effect != null)
                {
                    using (AndroidJavaObject effect = s_effect.CallStatic<AndroidJavaObject>(
                               "createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                    {
                        s_vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    s_vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Haptics] Rung lỗi: " + e.Message);
                s_failed = true;
            }
        }
#else
        private static void Vibrate(long milliseconds, int amplitude)
        {
            // Editor / nền tảng khác: không rung.
        }
#endif
    }
}
