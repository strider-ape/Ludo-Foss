using UnityEngine;
using UnityEngine.Rendering;

namespace LudoGame
{
    /// <summary>
    /// Battery saver: draws at full rate only while something moves. When the screen is still
    /// (waiting for a tap) it renders 10-20 frames per second, so the GPU mostly idles.
    /// </summary>
    public static class FrameGovernor
    {
        const int IdleFps = 10;
        const int PulseFps = 20;

        static float fullUntil;

        /// <summary>Set while a gentle looping animation (a glowing token or dice) is on screen.</summary>
        public static bool Pulsing;

        /// <summary>Render at full rate for at least this many seconds.</summary>
        public static void Keep(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            if (until > fullUntil) fullUntil = until;
        }

        public static void ApplyTargetFrameRate() => Application.targetFrameRate = GameSettings.BatterySaver ? 30 : 60;

        public static void Tick()
        {
            int target = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
            int fps = Time.unscaledTime < fullUntil ? target : Pulsing ? PulseFps : IdleFps;
            OnDemandRendering.renderFrameInterval = Mathf.Max(1, Mathf.RoundToInt((float)target / fps));
        }
    }
}
