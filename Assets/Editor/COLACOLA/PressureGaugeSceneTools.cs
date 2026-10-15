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
    public static class PressureGaugeSceneTools
    {
        private const string ReportPath = "output/scene-layout/PressureGauge-validation.txt";
        private static readonly Vector3 ReferenceSize = new Vector3(.34f, .42f, .12f);

        // Patch the saved scene in place; never regenerate the room or other props.
        public static void BuildAndReplace()
        {
            try
            {
                Directory.CreateDirectory(MainSceneTools.Output);
                var backup = MainSceneTools.Output + "/MainScene-before-pressure-gauge.unity";
                if (!File.Exists(backup)) File.Copy(MainSceneTools.ScenePath, backup);
                var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene");
                var placeholder = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == "PLACEHOLDER_PressureGauge");
                var existing = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == "PressureGauge");
                if (!placeholder && !existing) throw new InvalidOperationException("Pressure gauge placement anchor not found.");
                var anchor = placeholder ? placeholder : existing;
                var position = anchor.position;
                var rotation = anchor.rotation;
                var sizeMultiplier = placeholder
                    ? new Vector3(anchor.lossyScale.x / ReferenceSize.x, anchor.lossyScale.y / ReferenceSize.y, anchor.lossyScale.z / ReferenceSize.z)
                    : anchor.lossyScale;
                var unaffected = root.GetComponentsInChildren<Transform>(true)
                    .Where(t => t != anchor && !t.IsChildOf(anchor))
                    .ToDictionary(t => t, t => new Pose(t.position, t.rotation));

                var asset = PressureGaugeModelFactory.BuildPrefab();
                var deviceGroup = root.transform.Find("08_Devices");
                if (!deviceGroup)
                {
                    deviceGroup = new GameObject("08_Devices").transform;
                    deviceGroup.SetParent(root.transform, false);
                }
                var gauge = InstantiateGauge(deviceGroup, position, rotation, sizeMultiplier);
                UnityEngine.Object.DestroyImmediate(anchor.gameObject);
                foreach (var pair in unaffected)
                {
                    if (!pair.Key || Vector3.Distance(pair.Key.position, pair.Value.position) > .00001f ||
                        Quaternion.Angle(pair.Key.rotation, pair.Value.rotation) > .01f)
                        throw new InvalidOperationException("Unrelated transform changed during gauge replacement.");
                }
                if (!EditorSceneManager.SaveScene(scene, MainSceneTools.ScenePath)) throw new IOException("Unable to save MainScene.");
                AssetDatabase.SaveAssets();
                WriteGaugeReport(gauge, unaffected.Count);
                MainSceneTools.WriteReport(root, Camera.main);
                Debug.Log("COLA_PRESSURE_GAUGE_REPLACED");
                var captureAt = EditorApplication.timeSinceStartup + 3;
                EditorApplication.CallbackFunction finish = null;
                finish = () =>
                {
                    if (EditorApplication.timeSinceStartup < captureAt) return;
                    EditorApplication.update -= finish;
                    try
                    {
                        CaptureCloseup(gauge);
                        // Reopen the serialized scene to verify the actual saved prefab instance.
                        MainSceneTools.Verify();
                    }
                    catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(2); }
                };
                EditorApplication.update += finish;
            }
            catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
        }

        public static GameObject InstantiateGauge(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PressureGaugeModelFactory.PrefabPath);
            if (!asset) asset = PressureGaugeModelFactory.BuildPrefab();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.name = "PressureGauge";
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            return instance;
        }

        private static void WriteGaugeReport(GameObject gauge, int unchangedCount)
        {
            var meshes = gauge.GetComponentsInChildren<MeshFilter>(true);
            if (meshes.Length < 6 || meshes.Any(m => !m.sharedMesh || m.sharedMesh.name == "Cube"))
                throw new InvalidOperationException("Expected actual authored pressure gauge meshes.");
            if (!gauge.GetComponentsInChildren<Transform>(true).Any(t => t.name == "NeedlePivot"))
                throw new InvalidOperationException("Missing independent needle pivot.");
            var renderers = gauge.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var report = new StringBuilder();
            report.AppendLine("PASS: PLACEHOLDER_PressureGauge replaced by PressureGauge prefab.");
            report.AppendLine("PASS: " + unchangedCount + " unrelated transforms retained their world position and rotation.");
            report.AppendLine("PASS: separate NeedlePivot for future local-Z rotation.");
            report.AppendLine("Prefab: " + PressureGaugeModelFactory.PrefabPath);
            report.AppendLine("Custom mesh parts: " + meshes.Length);
            report.AppendLine("Triangles: " + meshes.Sum(m => m.sharedMesh.triangles.Length / 3));
            report.AppendLine("World bounds center: " + bounds.center.ToString("F4"));
            report.AppendLine("World bounds size: " + bounds.size.ToString("F4"));
            report.AppendLine("Static visual readout: 15. Unknown threshold: ?. No gameplay binding added.");
            File.WriteAllText(ReportPath, report.ToString());
        }

        private static void CaptureCloseup(GameObject gauge)
        {
            var transforms = gauge.GetComponentsInChildren<Transform>(true);
            var layers = transforms.Select(t => t.gameObject.layer).ToArray();
            var cameraObject = new GameObject("__PressureGaugePreviewCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            try
            {
                foreach (var transform in transforms) transform.gameObject.layer = 31;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.055f, .068f, .065f);
                camera.orthographic = true;
                camera.orthographicSize = .245f;
                camera.nearClipPlane = .02f;
                camera.farClipPlane = 5;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.transform.position = gauge.transform.TransformPoint(new Vector3(.20f, .09f, -.90f));
                camera.transform.LookAt(gauge.transform.position);
                MainSceneTools.Capture(camera, MainSceneTools.Output + "/PressureGauge-closeup.png", 1000, 1000);
            }
            finally
            {
                for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
