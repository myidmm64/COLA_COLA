using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColaCola.Editor
{
    /// <summary>Balances the secondary gauge against the central cola without changing the camera or controls.</summary>
    public static class TabletopFocusSceneTools
    {
        public const string Output = "output/gauge-focus";
        private const float GaugeScale = .82f;
        private static readonly Vector3 GaugeFoot = new Vector3(0, -.210f, 0);

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
                report.AppendLine("Dial and inner bezel use darker, more matte colours; gauge fill 0.40 -> 0.16.");
                report.AppendLine("Existing cola label light: intensity 0.55 -> 0.90, cone 40 -> 46 degrees, warmer tint. No new lights or glow materials.");
                File.WriteAllText(Output + "/Focus-validation.txt", report.ToString());
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
