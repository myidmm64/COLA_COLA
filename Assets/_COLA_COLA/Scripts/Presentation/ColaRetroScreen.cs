using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Native-resolution lens finish and brief signal tears, independent of gameplay randomness.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaRetroScreen : MonoBehaviour
    {
        [Range(0, .1f)] public float highlightSoftness = .025f;
        [Range(0, .2f)] public float edgeSoftness = .08f;
        [Range(0, .025f)] public float lensDistortion = .012f;
        [Range(0, .05f)] public float horizontalGlare = .018f;
        [Range(0, .1f)] public float tensionVignette = .055f;
        private ShakeSelectionInteraction owner;
        private readonly System.Random timing = new(87031);
        private float pulseAt = -10, pulseDuration = .10f, amplitude, nextNoise;
        private int bursts;
        public int RenderedFrames { get; private set; }
        public int GlitchCount { get; private set; }
        public float Tension { get; private set; }
        public float SignalTime => ColaGameClock.Time;
        public float GlitchStrength
        {
            get
            {
                float t = (ColaGameClock.Time - pulseAt) / pulseDuration;
                float envelope = t >= 0 && t <= 1 ? Mathf.Sin(t * Mathf.PI) : 0;
                return amplitude * envelope * envelope;
            }
        }

        public static void EnsureFor(Camera camera, ShakeSelectionInteraction interaction)
        {
            if (!camera || !Application.IsPlaying(camera.gameObject)) return;
            var signal = camera.GetComponent<ColaRetroScreen>();
            if (!signal) signal = camera.gameObject.AddComponent<ColaRetroScreen>();
            signal.owner = interaction;
            signal.enabled = true;
        }
        private void OnEnable()
        {
            pulseAt = -10; nextNoise = ColaGameClock.Time + 20;
            bursts = owner && owner.burstPresentation ? owner.burstPresentation.BurstCount : 0;
            Tension = 0;
        }
        private void Update()
        {
            if (!owner) return;
            float target = owner.AwaitingShakeResult ? 1 : 0;
            Tension = Mathf.Lerp(Tension, target, 1 - Mathf.Exp(-Mathf.Min(ColaGameClock.Delta, .05f) * 7));
            var burst = owner.burstPresentation;
            if (burst && burst.BurstCount != bursts)
            {
                bursts = burst.BurstCount;
                Pulse(.30f, .12f);
            }
            if (ColaGameClock.Time >= nextNoise)
            {
                if (owner.IsBusy || burst && burst.Stage != ColaBurstPresentation.BurstStage.Idle)
                { nextNoise = ColaGameClock.Time + 3; return; }
                nextNoise = ColaGameClock.Time + 35 + (float)timing.NextDouble() * 20;
                Pulse(.10f, .09f);
            }
        }
        public void Pulse(float strength, float seconds)
        {
            if (!isActiveAndEnabled) return;
            amplitude = Mathf.Clamp(strength, 0, .30f); pulseDuration = Mathf.Clamp(seconds, .06f, .15f);
            pulseAt = ColaGameClock.Time; GlitchCount++;
        }
        public void MarkRendered() => RenderedFrames++;
        private void OnDisable() { Tension = 0; pulseAt = -10; }
    }
}
