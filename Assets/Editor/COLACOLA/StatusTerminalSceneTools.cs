using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColaCola.Editor
{
    /// <summary>Places the terminal without rebuilding the established room, camera or characters.</summary>
    [InitializeOnLoad]
    public static class StatusTerminalSceneTools
    {
        public const string Output = "output/status-terminal";
        private const string Running = "COLACOLA.StatusTerminalVerification";
        private const string Started = Running + ".Started";
        private const string RuntimeStarted = Running + ".RuntimeStarted";
        private static readonly Vector3 ConsolePosition = new Vector3(-.34f, .90f, -.10f);
        private static readonly Vector3 TerminalPosition = new Vector3(-1.05f, .833f, .45f);
        private static bool finishing;
        private static int phase;
        private static StringBuilder playReport;

        static StatusTerminalSceneTools()
        {
            if (SessionState.GetBool(Running, false)) InstallCallbacks();
        }

        [MenuItem("COLA_COLA/Build and Place Status Terminal")]
        public static void BuildAndPlace()
        {
            try
            {
                Directory.CreateDirectory(Output);
                var backup = Output + "/MainScene-before-status-terminal.unity";
                if (!File.Exists(backup)) File.Copy(MainSceneTools.ScenePath, backup);
                var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene");
                var untouched = root.GetComponentsInChildren<Transform>(true)
                    .Where(t => !InDevice(t, "PressureGauge") && !InDevice(t, "ShakeConsole") && !InDevice(t, "StatusTerminal"))
                    .ToDictionary(t => t, t => new Pose(t.position, t.rotation));
                StatusTerminalModelFactory.BuildPrefab();
                ApplyToScene(root, Camera.main);
                foreach (var entry in untouched)
                    Require(entry.Key && Vector3.Distance(entry.Key.position, entry.Value.position) < .00001f &&
                            Quaternion.Angle(entry.Key.rotation, entry.Value.rotation) < .01f,
                        "An unrelated transform changed: " + entry.Key);
                Require(EditorSceneManager.SaveScene(scene, MainSceneTools.ScenePath), "Could not save MainScene.");
                AssetDatabase.SaveAssets();
                File.WriteAllText(Output + "/Placement-preservation.txt",
                    $"PASS: {untouched.Count} existing transforms retained their world position and rotation.\n" +
                    "Only gauge/console placement and the new terminal were changed.\n");
                if (Application.isBatchMode) BeginVerification();
                else CaptureSavedScene();
            }
            catch (Exception exception) { Fail(exception); }
        }

        // Also used by the full scene builder, after the seated camera has been created.
        public static void ApplyToScene(GameObject root, Camera camera)
        {
            Require(root && camera, "A scene root and player camera are required.");
            var console = Find(root, "ShakeConsole");
            var gauge = Find(root, "PressureGauge");
            var translation = ConsolePosition - console.position;
            console.position += translation;
            gauge.position += translation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(console);
            PrefabUtility.RecordPrefabInstancePropertyModifications(gauge);
            PlayerFacingDeviceLayout.Apply(root, camera);
            var group = root.transform.Find("08_Devices");
            Require(group, "08_Devices is missing.");
            var existing = group.Find("StatusTerminal");
            if (existing) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StatusTerminalModelFactory.PrefabPath);
            if (!prefab) prefab = StatusTerminalModelFactory.BuildPrefab();
            var terminal = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            terminal.name = "StatusTerminal";
            terminal.transform.SetPositionAndRotation(TerminalPosition,
                PlayerFacingDeviceLayout.FacingRotation(TerminalPosition, camera.transform.position));
            terminal.transform.localScale = Vector3.one;
            var display = terminal.GetComponent<StatusTerminalDisplay>();
            Require(display, "Terminal display component is missing.");
            display.SetBossState(3, 3, 12, false);
            display.SetPlayerState(3, 3, 15, false);
            display.SetActiveParticipant(true);
            display.RefreshDisplay();
            PrefabUtility.RecordPrefabInstancePropertyModifications(terminal.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(display);
        }

        [MenuItem("COLA_COLA/Verify Status Terminal")]
        public static void Verify()
        {
            try
            {
                Directory.CreateDirectory(Output);
                EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                BeginVerification();
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void BeginVerification()
        {
            ValidateScene();
            SessionState.SetBool(Running, true);
            SessionState.SetString(Started, DateTime.UtcNow.ToString("O"));
            SessionState.SetFloat(RuntimeStarted, -1);
            finishing = false;
            phase = 0;
            InstallCallbacks();
            EditorApplication.isPlaying = true;
        }

        private static void InstallCallbacks()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= PlayStateChanged;
            EditorApplication.playModeStateChanged += PlayStateChanged;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            try
            {
                if (DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Started, "")) > TimeSpan.FromSeconds(120))
                    throw new TimeoutException("Status terminal verification timed out.");
                if (!EditorApplication.isPlaying || finishing || Time.frameCount < 3) return;
                var display = GameObject.Find("StatusTerminal").GetComponent<StatusTerminalDisplay>();
                var began = SessionState.GetFloat(RuntimeStarted, -1);
                if (began < 0)
                {
                    Require(!display.IsAnimating, "The display must not fake gameplay on startup.");
                    Require(display.BossHealth == 3 && display.PlayerHealth == 3 &&
                            display.BossTokens == 12 && display.PlayerTokens == 15, "Saved sample values did not survive reopening.");
                    playReport = new StringBuilder();
                    playReport.AppendLine("PASS: saved sample values load without automatically changing game state.");
                    display.SetBossState(2, 3, 42, false);
                    Require(display.BossHealth == 2 && display.BossTokens == 42, "Boss setter failed.");
                    CheckNumber(display.boss.tokenDigits, display.digitMeshes, "0042");
                    int illuminated = display.boss.healthSegments.Count(r => r.sharedMaterial == display.healthOnMaterial);
                    Require(illuminated > 0 && illuminated < display.boss.healthSegments.Length, "Health bar did not reflect partial health.");
                    display.SetBossState(-2, 0, -1, false);
                    Require(display.boss.healthSegments.All(r => r.sharedMaterial == display.healthOffMaterial), "Zero maximum health must render safely empty.");
                    CheckNumber(display.boss.tokenDigits, display.digitMeshes, "0000");
                    display.SetPlayerState(500, 999, 12345, false);
                    Require(display.PlayerTokens == 12345, "Presentation must not truncate actual input data.");
                    CheckNumber(display.player.tokenDigits, display.digitMeshes, "9999");
                    display.SetBossState(3, 3, 12, false);
                    display.SetPlayerState(3, 3, 15, false);
                    display.SetPlayerState(2, 3, 20, true);
                    Require(display.IsAnimating, "Damage/token change did not start its feedback.");
                    playReport.AppendLine("PASS: independent participant setters, partial/empty bars, negative values and numeric overflow display.");
                    playReport.AppendLine("PASS: 4-digit token display saturates visually at 9999 while retaining the actual supplied integer.");
                    SessionState.SetFloat(RuntimeStarted, Time.unscaledTime);
                    return;
                }
                float elapsed = Time.unscaledTime - began;
                if (phase == 0 && elapsed >= 1.4f)
                {
                    Require(!display.IsAnimating && display.PlayerHealth == 2 && display.PlayerTokens == 20,
                        "Feedback did not settle to its requested target.");
                    CheckNumber(display.player.tokenDigits, display.digitMeshes, "0020");
                    playReport.AppendLine("PASS: damage blink and token tick finish at the requested value.");
                    MainSceneTools.Capture(Camera.main, Output + "/MainScene-ReadoutChange.png");
                    display.SetBossState(3, 3, 12, false);
                    display.SetPlayerState(3, 3, 15, false);
                    display.SetActiveParticipant(false);
                    display.enabled = false;
                    display.enabled = true;
                    Require(!display.IsAnimating, "Disabled display retained stale feedback.");
                    display.SetActiveParticipant(true);
                    phase = 1;
                }
                if (elapsed < 3f) return;
                foreach (var name in new[] { CharacterIdleFactory.BossName, CharacterIdleFactory.ArmsName })
                {
                    var animator = GameObject.Find(name).GetComponent<Animator>();
                    Require(animator && animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") &&
                            animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 0,
                        "Existing character Idle did not continue: " + name);
                }
                playReport.AppendLine("PASS: display disable/enable has no stale animation; both existing character Idles continue.");
                playReport.AppendLine($"Real Play-mode verification duration: {elapsed:F2}s, Unity {Application.unityVersion}.");
                playReport.AppendLine("Display samples are not a gameplay simulation. No boss AI, currency-award rules or shop logic added.");
                File.WriteAllText(Output + "/Display-playmode-validation.txt", playReport.ToString());
                finishing = true;
                EditorApplication.isPlaying = false;
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void PlayStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayStateChanged;
            EditorApplication.delayCall += () =>
            {
                try
                {
                    EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                    CaptureSavedScene();
                    Debug.Log("COLA_STATUS_TERMINAL_VERIFICATION_PASS");
                    if (Application.isBatchMode) EditorApplication.Exit(0);
                }
                catch (Exception exception) { Fail(exception); }
            };
        }

        private static void CaptureSavedScene()
        {
            var terminal = ValidateScene();
            MainSceneTools.Capture(Camera.main, Output + "/MainScene-StatusTerminal.png");
            MainSceneTools.Capture(Camera.main, MainSceneTools.Output + "/MainScene-preview.png");
            MainSceneTools.WriteReport(GameObject.Find("COLA_COLA_MainScene"), Camera.main);
            CaptureCloseup(terminal);
        }

        private static GameObject ValidateScene()
        {
            var root = GameObject.Find("COLA_COLA_MainScene");
            Require(root, "Main scene root is missing.");
            var camera = Camera.main;
            Require(camera, "Main Camera is missing.");
            camera.aspect = 16f / 9f;
            var terminal = Find(root, "StatusTerminal").gameObject;
            var display = terminal.GetComponent<StatusTerminalDisplay>();
            Require(display && display.TryValidateBindings(out _), "Display bindings are incomplete.");
            display.RefreshDisplay();
            var all = root.GetComponentsInChildren<Transform>(true);
            Require(all.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "Missing scripts.");
            Require(root.GetComponentsInChildren<Camera>(true).Length == 1 &&
                    root.GetComponentsInChildren<AudioListener>(true).Length == 1, "Camera/listener count changed.");
            Require(root.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterials.All(m => m && m.shader)), "Missing materials/shaders.");
            var meshes = terminal.GetComponentsInChildren<MeshFilter>(true);
            Require(meshes.Length > 20 && meshes.All(m => m.sharedMesh && m.sharedMesh.name != "Cube"), "Terminal must use authored meshes.");
            var gauge = Find(root, "PressureGauge");
            var cola = Find(root, "HeroCola_ExistingModel");
            var tPoint = camera.WorldToViewportPoint(terminal.transform.TransformPoint(0, .16f, -.085f));
            var gPoint = camera.WorldToViewportPoint(gauge.position);
            var cPoint = camera.WorldToViewportPoint(cola.position + Vector3.up * .2f);
            Require(tPoint.x < gPoint.x && gPoint.x < cPoint.x && cPoint.x > .48f && cPoint.x < .52f,
                "Expected player-view order: terminal -> gauge -> centered cola.");
            var cameraToDevice = camera.transform.position - terminal.transform.position;
            cameraToDevice.y = 0;
            Require(Vector3.Dot(terminal.transform.TransformDirection(Vector3.back), cameraToDevice.normalized) > .999f,
                "Terminal screen does not face the player.");
            var report = new StringBuilder();
            report.AppendLine("PASS: saved scene has authored StatusTerminal prefab and complete dynamic display bindings.");
            report.AppendLine("PASS: player-view order is status terminal -> pressure gauge -> centered cola.");
            report.AppendLine($"Viewport centers: terminal={tPoint:F4}, gauge={gPoint:F4}, cola={cPoint:F4}");
            report.AppendLine($"Terminal position={terminal.transform.position:F4}, yaw={terminal.transform.eulerAngles.y:F3}");
            report.AppendLine($"Console position={Find(root, "ShakeConsole").position:F4}, gauge={gauge.position:F4}");
            report.AppendLine($"Terminal mesh instances={meshes.Length}, triangles={meshes.Sum(m => m.sharedMesh.triangles.Length / 3)}");
            report.AppendLine("PASS: all material/shader references, one camera/listener, no missing scripts.");
            report.AppendLine("Sample display: boss 3/3, 12 tokens; player 3/3, 15 tokens. Values are visual samples only.");
            File.WriteAllText(Output + "/StatusTerminal-validation.txt", report.ToString());
            return terminal;
        }

        private static void CaptureCloseup(GameObject terminal)
        {
            var nodes = terminal.GetComponentsInChildren<Transform>(true);
            var layers = nodes.Select(t => t.gameObject.layer).ToArray();
            var cameraObject = new GameObject("__StatusTerminalPreview", typeof(Camera));
            try
            {
                foreach (var node in nodes) node.gameObject.layer = 31;
                var camera = cameraObject.GetComponent<Camera>();
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.035f, .047f, .044f);
                camera.orthographic = true;
                camera.orthographicSize = .22f;
                camera.nearClipPlane = .02f;
                camera.farClipPlane = 5;
                camera.transform.position = terminal.transform.TransformPoint(.16f, .26f, -.9f);
                camera.transform.LookAt(terminal.transform.TransformPoint(0, .15f, 0));
                MainSceneTools.Capture(camera, Output + "/StatusTerminal-closeup.png", 1400, 900);
            }
            finally
            {
                for (int i = 0; i < nodes.Length; i++) nodes[i].gameObject.layer = layers[i];
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static void CheckNumber(MeshFilter[] digits, Mesh[] glyphs, string expected)
        {
            Require(digits != null && digits.Length == expected.Length, "Numeric digit count changed.");
            for (int i = 0; i < digits.Length; i++)
                Require(digits[i].sharedMesh == glyphs[expected[i] - '0'], "Numeric glyph does not match requested value.");
        }

        private static Transform Find(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        }

        private static bool InDevice(Transform transform, string name)
        {
            for (var current = transform; current; current = current.parent)
                if (current.name == name) return true;
            return false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Fail(Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(Running, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayStateChanged;
            if (Application.isBatchMode) EditorApplication.Exit(2);
            else if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
