using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ColaCola.Presentation
{
    /// <summary>Original pizzeria lighting/grading, also applied to cached scenes on Play entry.</summary>
    public static class TabletopReadability
    {
        public const float GrainIntensity = .035f;
        public static void Apply(Transform sceneRoot)
        {
            if (!sceneRoot) return;
            ApplyLighting(sceneRoot);
            var volume = sceneRoot.GetComponentInChildren<Volume>();
            if (volume) ConfigureProfile(volume.profile);
            var camera = sceneRoot.GetComponentInChildren<Camera>();
            if (camera && camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
            {
                camera.allowHDR = true;
                data.renderPostProcessing = true;
                data.dithering = true;
                ColaRetroScreen.EnsureFor(camera, sceneRoot.GetComponentInChildren<ShakeSelectionInteraction>());
            }
        }

        public static void ApplyLighting(Transform sceneRoot)
        {
            if (!sceneRoot) return;
            // Restore the pre-refinement fixture settings, including cached scenes with scene reload disabled.
            Tune(sceneRoot, "Warm_Table_Key", new Color(.85f, .93f, .76f), 4.1f);
            Tune(sceneRoot, "Warm_Face_Fill", new Color(.70f, .79f, .73f), 1.25f);
            Tune(sceneRoot, "Horror_Face_Softbox", new Color(.77f, .84f, .74f), 2.3f);
            Tune(sceneRoot, "Horror_Cola_LabelFill", new Color(.94f, .84f, .66f), .90f);
            Tune(sceneRoot, "Kitchen_Warm", new Color(.36f, .61f, .44f), .50f);
            Tune(sceneRoot, "Soda_Cool_Fill", new Color(.20f, .66f, .57f), .95f);
            Tune(sceneRoot, "Horror_Boss_RedRim", new Color(1f, .12f, .055f), 2.1f);
            Tune(sceneRoot, "Horror_Emergency_WallGlow", new Color(1f, .075f, .025f), .65f);
            var kitchen = Tune(sceneRoot, "Horror_Kitchen_Fluorescent", new Color(.40f, .70f, .51f), .85f);
            if (kitchen && kitchen.TryGetComponent<HorrorPracticalFlicker>(out var flicker))
            {
                flicker.baseIntensity = .85f;
                flicker.variation = .07f;
                flicker.cycleSeconds = 17;
            }
            var key = sceneRoot.Find("06_Lighting/Warm_Table_Key");
            if (key && key.TryGetComponent<Light>(out var keyLight))
            {
                keyLight.spotAngle = 82;
                keyLight.innerSpotAngle = 55;
            }
            var gaugeFill = sceneRoot.Find("06_Lighting/Horror_Gauge_Readability");
            if (gaugeFill && gaugeFill.TryGetComponent<Light>(out var light))
            {
                light.enabled = false;
                light.intensity = 0;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.055f, .073f, .063f);
            RenderSettings.reflectionIntensity = .12f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.018f, .031f, .028f);
            RenderSettings.fogDensity = .065f;
        }

        private static Light Tune(Transform sceneRoot, string name, Color color, float intensity)
        {
            var fixture = sceneRoot.Find("06_Lighting/" + name);
            if (!fixture || !fixture.TryGetComponent<Light>(out var light)) return null;
            light.color = color;
            light.intensity = intensity;
            return light;
        }

        public static void ConfigureProfile(VolumeProfile profile)
        {
            if (!profile) return;
            Effect<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            var color = Effect<ColorAdjustments>(profile);
            color.postExposure.Override(.40f);
            color.contrast.Override(13);
            color.saturation.Override(-19);
            color.colorFilter.Override(new Color(.98f, .975f, .94f));
            var white = Effect<WhiteBalance>(profile);
            white.temperature.Override(-2);
            white.tint.Override(0);
            Effect<LiftGammaGain>(profile).lift.Override(new Vector4(1, 1, 1, .006f));
            var ranges = Effect<ShadowsMidtonesHighlights>(profile);
            ranges.shadows.Override(new Vector4(.91f, .99f, 1.01f, 0));
            ranges.midtones.Override(new Vector4(.99f, 1, .96f, 0));
            ranges.highlights.Override(new Vector4(1.04f, 1.01f, .94f, 0));
            var bloom = Effect<Bloom>(profile);
            bloom.intensity.Override(0);
            bloom.clamp.Override(2);
            var grain = Effect<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(GrainIntensity);
            grain.response.Override(.75f);
            var vignette = Effect<Vignette>(profile);
            vignette.intensity.Override(.22f);
            vignette.smoothness.Override(.72f);
            vignette.color.Override(new Color(.003f, .009f, .007f));
            vignette.rounded.Override(true);
            Effect<ChromaticAberration>(profile).intensity.Override(.004f);
        }

        private static T Effect<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component)) component = profile.Add<T>(true);
            component.active = true;
            return component;
        }
    }
}
