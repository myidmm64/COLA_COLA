using UnityEngine;

namespace ColaCola.Presentation
{
    // All tabletop choreography shares this clock so opening settings cannot finish a roll off-screen.
    public static class ColaGameClock
    {
        private static double pausedAt, pausedSeconds;
        public static bool Paused { get; private set; }
        public static double TimeAsDouble => UnityEngine.Time.unscaledTimeAsDouble - pausedSeconds - (Paused ? UnityEngine.Time.unscaledTimeAsDouble - pausedAt : 0);
        public static float Time => (float)TimeAsDouble;
        public static float Delta => Paused ? 0 : UnityEngine.Time.unscaledDeltaTime;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Paused = false; pausedAt = pausedSeconds = 0; }
        public static void SetPaused(bool value)
        {
            if (Paused == value) return;
            if (value) pausedAt = UnityEngine.Time.unscaledTimeAsDouble;
            else pausedSeconds += UnityEngine.Time.unscaledTimeAsDouble - pausedAt;
            Paused = value;
        }
    }

    public sealed class ColaPauseDelay : CustomYieldInstruction
    {
        private readonly float until;
        public ColaPauseDelay(float seconds) { until = ColaGameClock.Time + Mathf.Max(0, seconds); }
        public override bool keepWaiting => ColaGameClock.Paused || ColaGameClock.Time < until;
    }
}
