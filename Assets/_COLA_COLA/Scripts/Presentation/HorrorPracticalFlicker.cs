using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Subtle, deterministic power fluctuations for a background practical light.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class HorrorPracticalFlicker : MonoBehaviour
    {
        public Light targetLight;
        [Min(0f)] public float baseIntensity = 0.65f;
        [Range(0f, 0.3f)] public float variation = 0.07f;
        [Min(1f)] public float cycleSeconds = 17f;
        public float seed = 2.73f;

        private const float DipSeconds = 0.3f;
        private const float MaximumDimming = 0.3f;

        private void OnEnable()
        {
            if (targetLight == null)
                targetLight = GetComponent<Light>();

            RestoreIntensity();
        }

        private void Update()
        {
            if (targetLight == null)
                return;

            double time = Time.timeAsDouble;
            float noise = Mathf.PerlinNoise(seed, (float)(time * 0.35d));
            float fluctuation = (noise * 2f - 1f) * Mathf.Clamp(variation, 0f, 0.3f);

            // One gently eased dip per cycle; never switch the light off.
            double cycle = System.Math.Max(1d, cycleSeconds);
            double phase = (time + seed) % cycle;
            if (phase < 0d)
                phase += cycle;

            float dip = 0f;
            if (phase < DipSeconds)
            {
                float eased = Mathf.Sin((float)phase / DipSeconds * Mathf.PI);
                dip = eased * eased * MaximumDimming;
            }

            float multiplier = Mathf.Max(1f - MaximumDimming, 1f + fluctuation - dip);
            targetLight.intensity = Mathf.Max(0f, baseIntensity) * multiplier;
        }

        private void OnDisable()
        {
            RestoreIntensity();
        }

        private void OnValidate()
        {
            baseIntensity = Mathf.Max(0f, baseIntensity);
            variation = Mathf.Clamp(variation, 0f, 0.3f);
            cycleSeconds = Mathf.Max(1f, cycleSeconds);
        }

        private void RestoreIntensity()
        {
            if (targetLight != null)
                targetLight.intensity = Mathf.Max(0f, baseIntensity);
        }
    }
}
