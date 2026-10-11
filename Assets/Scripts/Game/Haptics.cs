using UnityEngine;

namespace LudoGame
{
    /// <summary>Short vibration ticks on Android (dice landing, captures). Does nothing elsewhere.</summary>
    public static class Haptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int sdk;
        static bool initialised;

        static void Init()
        {
            initialised = true;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    sdk = version.GetStatic<int>("SDK_INT");
                }
            }
            catch (System.Exception)
            {
                vibrator = null;
            }
        }
#endif

        public static void Tap(int milliseconds = 15, int amplitude = 80)
        {
            if (!GameSettings.Vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!initialised) Init();
            if (vibrator == null) return;
            try
            {
                if (sdk >= 26)
                {
                    using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                    {
                        vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    // Android 7.x has no short vibration API; this also tells Unity to add the VIBRATE permission.
                    Handheld.Vibrate();
                }
            }
            catch (System.Exception)
            {
            }
#endif
        }
    }
}
