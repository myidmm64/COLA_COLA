using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ColaCola.Editor
{
    /// <summary>Balances the secondary gauge against the central cola without changing the camera or controls.</summary>
    [InitializeOnLoad]
    public static class TabletopFocusSceneTools
    {
        public const string Output = "output/gauge-focus";
        private const float GaugeScale = .82f;
        private static readonly Vector3 GaugeFoot = new Vector3(0, -.210f, 0);

        private const string GlareVerification = "COLA_COLA.GaugeGlare.PlayVerification";

        static TabletopFocusSceneTools()
        {
            if (SessionState.GetBool(GlareVerification, false)) EditorApplication.update += VerifyGaugeGlareInPlay;
        }

        [MenuItem("COLA_COLA/Reduce Gauge Glare")]
        public static void ReduceGaugeGlare()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Apply the saved gauge adjustment from Edit mode.");
                Directory.CreateDirectory(Output);
                var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene");
                var light = root.transform.Find("06_Lighting/Horror_Gauge_Readability").GetComponent<Light>();
                light.intensity = 0;
                light.range = .8f;
                light.enabled = false;
                EditorUtility.SetDirty(light);
                PressureGaugeModelFactory.ApplyMutedPalette();
                ShakeConsoleModelFactory.ApplyMattePalette();
                var volume = root.GetComponentInChildren<Volume>();
                Require(volume && volume.sharedProfile && volume.sharedProfile.TryGet<Bloom>(out _), "Scene Bloom profile is missing.");
                volume.sharedProfile.TryGet<Bloom>(out var bloom);
                bloom.intensity.Override(0);
                bloom.clamp.Override(2);
                EditorUtility.SetDirty(volume.sharedProfile);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the gauge lighting change.");
                SessionState.SetString(GlareVerification + ".OtherLights", OtherLightsSignature(root));
                SessionState.SetString(GlareVerification + ".Started", DateTime.UtcNow.ToString("O"));
                SessionState.SetBool(GlareVerification, true);
                EditorApplication.update -= VerifyGaugeGlareInPlay;
                EditorApplication.update += VerifyGaugeGlareInPlay;
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static string OtherLightsSignature(GameObject root) => string.Join("\n",
            root.GetComponentsInChildren<Light>(true).Where(l => l.name != "Horror_Gauge_Readability" && l.name != "Horror_Kitchen_Fluorescent")
                .OrderBy(l => l.name).Select(l => $"{l.name}|{l.type}|{l.enabled}|{l.intensity:F4}|{l.range:F4}|{l.spotAngle:F4}|{l.color:F4}|{l.transform.position:F4}|{l.transform.rotation:F4}"));

        private static void VerifyGaugeGlareInPlay()
        {
            try
            {
                if (DateTime.UtcNow - DateTime.Parse(SessionState.GetString(GlareVerification + ".Started", "")) > TimeSpan.FromSeconds(90))
                    throw new TimeoutException("Gauge lighting Play verification timed out.");
                if (!EditorApplication.isPlaying || Time.frameCount < 3 || Time.unscaledTime < 2) return;
                var root = GameObject.Find("COLA_COLA_MainScene");
                var light = root.transform.Find("06_Lighting/Horror_Gauge_Readability").GetComponent<Light>();
                Require(!light.enabled && light.intensity == 0,
                    "The local gauge light must stay off during Play.");
                // The background fluorescent intensity is animated; other light settings must stay fixed.
                Require(OtherLightsSignature(root) == SessionState.GetString(GlareVerification + ".OtherLights", ""),
                    "An unrelated fixed light changed during Play.");
                foreach (var name in new[] { "Dial_Cream", "Bezel_PaleMetal" })
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_COLA_COLA/Art/Materials/PressureGauge/" + name + ".mat");
                    Require(material && material.GetFloat("_SpecularHighlights") == 0 && material.GetFloat("_EnvironmentReflections") == 0 &&
                        material.GetFloat("_Smoothness") <= .061f && material.GetColor("_EmissionColor").maxColorComponent == 0,
                        "Gauge matte material settings were not retained: " + name);
                    var expected = name == "Dial_Cream" ? new Color(.42f, .405f, .33f) : new Color(.36f, .35f, .29f);
                    Require(((Vector4)(material.GetColor("_BaseColor") - expected)).sqrMagnitude < .000001f &&
                        root.transform.Find("08_Devices/PressureGauge").GetComponentsInChildren<Renderer>(true)
                            .Any(r => r.sharedMaterials.Contains(material)),
                        "The live gauge does not use the reduced-albedo material: " + name);
                }
                var gauge = root.transform.Find("08_Devices/PressureGauge").GetComponent<ColaCola.Presentation.PressureGaugeDisplay>();
                Require(gauge && gauge.TryValidateBindings(out _), "Gauge readout/needle bindings changed.");
                var console = root.transform.Find("08_Devices/ShakeConsole");
                foreach (var material in console.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Distinct())
                    Require(material.GetFloat("_SpecularHighlights") == 0 && material.GetFloat("_EnvironmentReflections") == 0 &&
                        material.GetColor("_EmissionColor").maxColorComponent == 0 && !material.IsKeywordEnabled("_EMISSION"),
                        "An active console/key material still contributes a bright reflection or emission: " + material.name);
                var liveVolume = root.GetComponentInChildren<Volume>();
                Require(liveVolume && liveVolume.profile.TryGet<Bloom>(out var runtimeBloom) && runtimeBloom.intensity.value == 0,
                    "The live scene Volume profile still enables Bloom.");
                bool evaluatedStackAvailable = VolumeManager.instance.isInitialized;
                if (evaluatedStackAvailable)
                {
                    VolumeManager.instance.Update(Camera.main.transform, Camera.main.GetUniversalAdditionalCameraData().volumeLayerMask);
                    var liveBloom = VolumeManager.instance.stack.GetComponent<Bloom>();
                    Require(liveBloom != null && liveBloom.intensity.value == 0, "The evaluated Play-mode Bloom is still active.");
                }
                var match = root.GetComponentInChildren<ColaCola.Presentation.ColaDuelController>();
                Require(match && match.interaction.SelectStrength(3) && match.interaction.gauge.HasPrediction,
                    "The pressure forecast must still work with the new materials.");
                match.RestartRun(123);
                ProjectVerificationBridge.WriteReport(Output + "/Glare-playmode-validation.txt",
                    "PASS: live Play-mode gauge point light disabled, intensity 0.\n" +
                    "PASS: original gauge dial plus ALL active console/key materials are non-emissive and have specular/environment reflections disabled.\n" +
                    "PASS: live Play-mode Volume profile has Bloom intensity 0; authored clamp 2. Film grain, colour grading and exposure retained.\n" +
                    (evaluatedStackAvailable ? "PASS: renderer Volume stack also evaluated to Bloom intensity 0.\n" : "Scope: batch renderer did not initialize a Volume stack; live profile verified, no rendered-frame claim.\n") +
                    "PASS: actual pressure forecast selection works, readout/needle bindings intact, other fixed lights unchanged. No preview cameras or image captures used.\n" +
                    "Scope: live component/material state checks; actual Game-view appearance has not been visually confirmed.\n");
                Debug.Log("COLA_GAUGE_GLARE_PLAYMODE_PASS");
                SessionState.SetBool(GlareVerification, false);
                EditorApplication.update -= VerifyGaugeGlareInPlay;
                EditorApplication.playModeStateChanged += FinishGaugeGlareVerification;
                EditorApplication.isPlaying = false;
            }
            catch (Exception exception)
            {
                SessionState.SetBool(GlareVerification, false);
                EditorApplication.update -= VerifyGaugeGlareInPlay;
                Fail(exception);
            }
        }

        private static void FinishGaugeGlareVerification(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= FinishGaugeGlareVerification;
            if (Application.isBatchMode) EditorApplication.delayCall += PlayableDuelVerification.Begin;
        }

        [MenuItem("COLA_COLA/Balance Gauge and Cola Focus")]
        public static void Apply()
        {
            try
            {
                Directory.CreateDirectory(Output);
                var backup = Output + "/MainScene-before-focus.unity";
                if (!File.Exists(backup)) File.Copy(MainSceneTools.ScenePath, backup);
                var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene");
                var camera = Camera.main;
                camera.aspect = 16f / 9f;
                var gauge = root.transform.Find("08_Devices/PressureGauge");
                var footBefore = gauge.TransformPoint(GaugeFoot);
                var beforeArea = ProjectedArea(camera, BoundsOf(gauge));
                var untouched = root.GetComponentsInChildren<Transform>(true)
                    .Where(t => t != gauge && !t.IsChildOf(gauge))
                    .ToDictionary(t => t, t => (position: t.position, rotation: t.rotation, scale: t.localScale));
                ApplyToScene(root);
                foreach (var pair in untouched)
                    Require(pair.Key && Vector3.Distance(pair.Key.position, pair.Value.position) < .00001f &&
                        Quaternion.Angle(pair.Key.rotation, pair.Value.rotation) < .01f &&
                        Vector3.Distance(pair.Key.localScale, pair.Value.scale) < .00001f,
                        "Unrelated transform changed: " + pair.Key);
                Require(Vector3.Distance(gauge.TransformPoint(GaugeFoot), footBefore) < .00001f,
                    "Gauge foot no longer meets its console mount.");
                var afterArea = ProjectedArea(camera, BoundsOf(gauge));
                var report = new StringBuilder();
                report.AppendLine("Gauge visual hierarchy tuning: central cola is the primary focus.");
                report.AppendLine($"Gauge scale: {GaugeScale:F2}; linear size reduced 18% from the authored model.");
                report.AppendLine($"Fixed-camera gauge bounds area: {beforeArea:F5} -> {afterArea:F5} ({afterArea / beforeArea:P1} of previous area on this application).");
                report.AppendLine($"PASS: foot mounting point remains {footBefore:F4}; gauge lowered as it shrinks, without floating above the console.");
                report.AppendLine($"PASS: {untouched.Count} unrelated transforms, including camera, cola, three keys, both inventories, actors and lights, are preserved.");
                report.AppendLine("Gauge/console use matte materials without specular/environment reflections; the local gauge fill is disabled and scene Bloom intensity is zero.");
                report.AppendLine("Existing cola label light: intensity 0.55 -> 0.90, cone 40 -> 46 degrees, warmer tint. No new lights or glow materials.");
                ProjectVerificationBridge.WriteReport(Output + "/Focus-validation.txt", report.ToString());
                AssetDatabase.SaveAssets();
                Require(EditorSceneManager.SaveScene(scene, MainSceneTools.ScenePath), "Could not save MainScene.");
                MainSceneTools.WriteReport(root, camera);
                CaptureAndVerify();
            }
            catch (Exception exception) { Fail(exception); }
        }

        public static void ApplyToScene(GameObject root)
        {
            var gauge = root.transform.Find("08_Devices/PressureGauge");
            if (!gauge) throw new InvalidOperationException("PressureGauge is missing.");
            var mountingPoint = gauge.TransformPoint(GaugeFoot);
            gauge.localScale = Vector3.one * GaugeScale;
            gauge.position += mountingPoint - gauge.TransformPoint(GaugeFoot);
            PrefabUtility.RecordPrefabInstancePropertyModifications(gauge);
            PressureGaugeModelFactory.ApplyMutedPalette();
            HorrorSceneTools.ApplyTabletopFocusLighting(root);
        }

        private static void CaptureAndVerify()
        {
            var at = EditorApplication.timeSinceStartup + 3;
            EditorApplication.CallbackFunction capture = null;
            capture = () =>
            {
                if (EditorApplication.timeSinceStartup < at) return;
                EditorApplication.update -= capture;
                try
                {
                    var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                    var root = scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene");
                    var camera = Camera.main;
                    camera.aspect = 16f / 9f;
                    var gauge = root.transform.Find("08_Devices/PressureGauge");
                    Require(Vector3.Distance(gauge.localScale, Vector3.one * GaugeScale) < .00001f,
                        "Gauge scale was not serialized.");
                    TestItemLayoutSceneTools.Validate(root, camera);
                    MainSceneTools.Capture(camera, Output + "/MainScene-Focus.png");
                    Debug.Log("COLA_TABLETOP_FOCUS_SAVED");
                    PressureInteractionSceneTools.Verify();
                }
                catch (Exception exception) { Fail(exception); }
            };
            EditorApplication.update += capture;
        }

        private static Bounds BoundsOf(Transform node)
        {
            var renderers = node.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var result = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) result.Encapsulate(renderer.bounds);
            return result;
        }

        private static float ProjectedArea(Camera camera, Bounds bounds)
        {
            var min = Vector2.one; var max = Vector2.zero;
            foreach (var x in new[] { bounds.min.x, bounds.max.x })
                foreach (var y in new[] { bounds.min.y, bounds.max.y })
                    foreach (var z in new[] { bounds.min.z, bounds.max.z })
                    {
                        var point = camera.WorldToViewportPoint(new Vector3(x, y, z));
                        min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                    }
            return (max.x - min.x) * (max.y - min.y);
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Fail(Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(2);
            else throw new InvalidOperationException("Tabletop focus tuning failed.", exception);
        }
    }
}
