using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ColaCola.Presentation
{
    /// <summary>Keeps the tabletop controls readable, including cached Editor scenes with scene reload disabled.</summary>
    public static class TabletopReadability
    {
        public static void Apply(Transform sceneRoot)
        {
            if (!sceneRoot) return;
            var gaugeFill = sceneRoot.Find("06_Lighting/Horror_Gauge_Readability");
            if (gaugeFill && gaugeFill.TryGetComponent<Light>(out var light))
            {
                light.enabled = false;
                light.intensity = 0;
            }
            var volume = sceneRoot.GetComponentInChildren<Volume>();
            if (volume && volume.profile.TryGet<Bloom>(out var bloom))
            {
                bloom.intensity.Override(0);
                bloom.clamp.Override(2);
            }
        }
    }
}
