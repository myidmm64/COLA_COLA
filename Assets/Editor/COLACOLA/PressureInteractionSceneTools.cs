using System;
using System.IO;
using System.Linq;
using System.Text;
using ColaCola.Core;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColaCola.Editor
{
    [InitializeOnLoad]
    public static class PressureInteractionSceneTools
    {
        public const string Output = "output/pressure-interaction";
        public const string SettingsPath = "Assets/_COLA_COLA/Data/PressureInteractionSettings.asset";
        private const string Running = "COLACOLA.PressureInteraction.Verify";
        private static int phase, resolvedEvents, bottleBeforeBurst;
        private static float phaseStarted;
        private static StringBuilder report;

        static PressureInteractionSceneTools()
        {
            if (SessionState.GetBool(Running, false)) Hook();
        }

        [MenuItem("COLA_COLA/Apply Pressure Preview and Cola Confirmation")]
        public static void Apply()
        {
            try
            {
                Directory.CreateDirectory(Output);
                var backup = Output + "/MainScene-before-pressure-interaction.unity";
                if (!File.Exists(backup)) File.Copy(MainSceneTools.ScenePath, backup);
                var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene");
                var oldGauge = Find(root, "PressureGauge");
                var preserved = root.GetComponentsInChildren<Transform>(true)
                    .Where(t => t != oldGauge && !t.IsChildOf(oldGauge))
                    .ToDictionary(t => t, t => new Pose(t.position, t.rotation));
                var parent = oldGauge.parent;
                var position = oldGauge.position;
                var rotation = oldGauge.rotation;
                var scale = oldGauge.localScale;
                var prefab = PressureGaugeModelFactory.BuildPrefab();
                UnityEngine.Object.DestroyImmediate(oldGauge.gameObject);
                var gauge = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                gauge.name = "PressureGauge";
                gauge.transform.SetPositionAndRotation(position, rotation);
                gauge.transform.localScale = scale;
                ApplyToScene(root, Camera.main);
                foreach (var entry in preserved)
                    Check(entry.Key && Vector3.Distance(entry.Key.position, entry.Value.position) < .00001f &&
                          Quaternion.Angle(entry.Key.rotation, entry.Value.rotation) < .01f,
                        "Unrelated transform changed: " + entry.Key);
                Check(EditorSceneManager.SaveScene(scene, MainSceneTools.ScenePath), "Scene save failed.");
                AssetDatabase.SaveAssets();
                File.WriteAllText(Output + "/Placement-preservation.txt", $"PASS: {preserved.Count} pre-existing non-gauge transforms preserved.\nCamera, cola, console, terminal, characters and lighting keep their saved placement.\n");
                StartVerification();
            }
            catch (Exception exception) { Fail(exception); }
        }

        public static void ApplyToScene(GameObject root, Camera camera)
        {
            Directory.CreateDirectory("Assets/_COLA_COLA/Data");
            AssetDatabase.Refresh();
            var settings = AssetDatabase.LoadAssetAtPath<PressureInteractionSettings>(SettingsPath);
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<PressureInteractionSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            var console = Find(root, "ShakeConsole");
            var bottle = Find(root, "HeroCola_ExistingModel");
            var deviceGroup = root.transform.Find("08_Devices");
            var node = deviceGroup.Find("ShakeSelection");
            if (!node) { node = new GameObject("ShakeSelection").transform; node.SetParent(deviceGroup, false); }
            var interaction = node.GetComponent<ShakeSelectionInteraction>();
            if (!interaction) interaction = node.gameObject.AddComponent<ShakeSelectionInteraction>();
            interaction.settings = settings;
            interaction.playerCamera = camera;
            interaction.gauge = Find(root, "PressureGauge").GetComponent<PressureGaugeDisplay>();
            interaction.strengthButtons = Enumerable.Range(1, 3).Select(i => console.Find("ShakeButton" + i)).ToArray();
            interaction.bottle = bottle;
            interaction.cap = bottle.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Cap_Pivot");
            var collider = bottle.GetComponent<BoxCollider>();
            if (!collider)
            {
                collider = bottle.gameObject.AddComponent<BoxCollider>();
                var renderers = bottle.GetComponentsInChildren<Renderer>(true);
                var localBounds = new Bounds(bottle.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
                foreach (var renderer in renderers)
                    for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
                        localBounds.Encapsulate(bottle.InverseTransformPoint(new Vector3(
                            x == 0 ? renderer.bounds.min.x : renderer.bounds.max.x,
                            y == 0 ? renderer.bounds.min.y : renderer.bounds.max.y,
                            z == 0 ? renderer.bounds.min.z : renderer.bounds.max.z)));
                collider.center = localBounds.center;
                collider.size = localBounds.size;
            }
            interaction.bottleCollider = collider;
            interaction.RefreshAuthoringView();
            EditorUtility.SetDirty(interaction);
        }

        [MenuItem("COLA_COLA/Verify Pressure Preview and Confirmation")]
        public static void Verify()
        {
            try
            {
                Directory.CreateDirectory(Output);
                EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                StartVerification();
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void StartVerification()
        {
            ValidateCore();
            ValidateSavedScene();
            SessionState.SetBool(Running, true);
            SessionState.SetString(Running + ".Started", DateTime.UtcNow.ToString("O"));
            SessionState.SetBool(Running + ".Initialized", false);
            Hook();
            EditorApplication.isPlaying = true;
        }

        private static void Hook()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= PlayChanged;
            EditorApplication.playModeStateChanged += PlayChanged;
        }

        private static void ValidateCore()
        {
            int rolls = 0;
            var model = new BottlePressureModel(20, 2, 8);
            Check(!model.TryCommit(() => { rolls++; return .9; }, out _) && rolls == 0, "Selection is required before committing.");
            double previous = 0;
            for (int i = 1; i <= 3; i++)
            {
                Check(model.SelectStrength(i), "Valid strength rejected.");
                var prediction = model.Predict(i);
                Check(model.Pressure == 8 && rolls == 0 && prediction.PressureAfter == 8 + i &&
                    prediction.BurstProbability > previous, "Preview modified pressure/randomness or probability was not monotonic.");
                previous = prediction.BurstProbability;
            }
            var expected = model.Predict(3);
            Check(model.TryCommit(() => { rolls++; return .99; }, out var result), "Commit failed.");
            Check(rolls == 1 && model.Pressure == 11 && !result.Burst && result.Prediction.BurstProbability == expected.BurstProbability,
                "Committed result does not use its displayed prediction.");
            Check(!model.TryCommit(() => { rolls++; return .1; }, out _) && rolls == 1, "Duplicate commit consumed a second roll.");
            model.BeginBottle(20, 19);
            model.SelectStrength(1);
            Check(model.TryCommit(() => .999999, out result) && result.Burst, "Limit reach must resolve the provisional 100% rule.");
            Check(!model.SelectStrength(2), "Burst bottle should reject further actions.");
            model.BeginBottle(24);
            model.SelectStrength(1);
            Check(model.TryCommit(() => 0, out result) && result.Burst && result.Prediction.PressureAfter < model.Limit,
                "Probabilistic bursting below the public limit did not work.");
            Check(PressureGaugeDisplay.FormatProbability(1) == "100%" &&
                PressureGaugeDisplay.FormatProbability(.00001) == "<0.1%", "Probability readout rounding is misleading.");
            File.WriteAllText(Output + "/Core-validation.txt", "PASS: selecting and comparing strengths never increases pressure or consumes RNG.\nPASS: one cola confirmation applies exactly one pressure increase and roll, using the forecast probability.\nPASS: unselected/duplicate actions rejected; probability rises with pressure; probabilistic bursts below limit and provisional 100% at limit.\nPASS: dynamic bottle reset and safe probability formatting.\n");
        }

        private static ShakeSelectionInteraction ValidateSavedScene()
        {
            var root = GameObject.Find("COLA_COLA_MainScene");
            var interaction = Find(root, "ShakeSelection").GetComponent<ShakeSelectionInteraction>();
            Check(interaction && interaction.gauge && interaction.gauge.TryValidateBindings(out _), "Gauge/interaction bindings are incomplete.");
            Check(interaction.settings && interaction.bottleCollider && interaction.strengthButtons.All(t => t && t.GetComponent<Collider>()), "Clickable objects/configuration are missing.");
            Check(!root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "HiddenLimit_QuestionMark" || t.name == "CurrentPressure_15"), "Old static gauge inscriptions remain.");
            Check(root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "Missing script references.");
            Check(root.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterials.All(m => m && m.shader)), "Missing materials/shaders.");
            var meshes = interaction.gauge.GetComponentsInChildren<MeshFilter>(true);
            File.WriteAllText(Output + "/Scene-validation.txt",
                "PASS: saved MainScene reopens with public bottle-limit digits, probability readout and separate pressure/forecast needles.\n" +
                "PASS: all three physical key colliders, central cola collider, input component and settings asset are bound.\n" +
                "PASS: old ? and fixed 15 inscriptions are absent from the scene.\n" +
                "PASS: all material/shader references present and no missing scripts.\n" +
                $"Gauge mesh instances: {meshes.Length}; displayed mesh triangles: {meshes.Sum(m => m.sharedMesh.triangles.Length / 3)}.\n" +
                $"Temporary probability exponent: {interaction.settings.probabilityExponent}; bottle limit range: {interaction.settings.minimumBottleLimit}..{interaction.settings.maximumBottleLimit}.\n" +
                "Display-only health/token terminal values are preserved. Match AI, damage and token awards remain separate.\n");
            return interaction;
        }

        private static bool Click(ShakeSelectionInteraction interaction, Transform target)
        {
            Physics.SyncTransforms();
            Vector3 point;
            if (target == interaction.bottle) point = interaction.bottleCollider.bounds.center;
            else
            {
                var collider = target.GetComponent<BoxCollider>();
                point = target.TransformPoint(collider.center + Vector3.up * collider.size.y * .35f);
            }
            var screen = interaction.playerCamera.WorldToScreenPoint(point);
            return interaction.HandlePointer(screen);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            try
            {
                if (DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Running + ".Started", "")) > TimeSpan.FromSeconds(100))
                    throw new TimeoutException("Pressure interaction verification timed out.");
                if (!EditorApplication.isPlaying || Time.frameCount < 3) return;
                var interaction = UnityEngine.Object.FindFirstObjectByType<ShakeSelectionInteraction>();
                if (!SessionState.GetBool(Running + ".Initialized", false))
                {
                    SessionState.SetBool(Running + ".Initialized", true);
                    phase = 0;
                    resolvedEvents = 0;
                    report = new StringBuilder();
                    interaction.ShakeResolved += _ => resolvedEvents++;
                    interaction.ResetSession(123);
                    interaction.BeginBottle(20, 8);
                    Check(!Click(interaction, interaction.bottle) && interaction.CommittedActionCount == 0,
                        "Clicking cola without a selected strength advanced play.");
                    for (int i = 1; i <= 3; i++)
                    {
                        Check(Click(interaction, interaction.strengthButtons[i - 1]), "Actual raycast could not select key " + i);
                        Check(interaction.Model.Pressure == 8 && interaction.CommittedActionCount == 0 && interaction.Model.SelectedStrength == i,
                            "Key selection committed the shake.");
                        var prediction = interaction.Model.Predict(i);
                        Check(interaction.gauge.PredictedPressure == prediction.PressureAfter &&
                            interaction.gauge.ProbabilityText == PressureGaugeDisplay.FormatProbability(prediction.BurstProbability),
                            "Gauge forecast differs from the rule model.");
                        MainSceneTools.Capture(Camera.main, Output + $"/Preview-Strength{i}.png");
                    }
                    CaptureDevices(interaction, Output + "/Preview-Closeup.png");
                    report.AppendLine("PASS: actual 3D raycasts select all keys; no pressure or action count changes before cola confirmation.");
                    Check(Click(interaction, interaction.bottle) && interaction.IsBusy && interaction.Model.Pressure == 11 && interaction.CommittedActionCount == 1,
                        "Cola did not resolve the selected strength exactly once.");
                    Check(!Click(interaction, interaction.bottle) && !interaction.SelectStrength(1) && interaction.CommittedActionCount == 1,
                        "Input was not locked during shake feedback.");
                    phaseStarted = Time.unscaledTime;
                    return;
                }
                if (phase == 0 && !interaction.IsBusy)
                {
                    Check(!interaction.LastResult.Burst && interaction.Model.Pressure == 11 && resolvedEvents == 1,
                        "Safe confirmation did not finish exactly once.");
                    Check(interaction.Model.SelectedStrength == 0 && !interaction.gauge.HasPrediction && interaction.gauge.ProbabilityText == "--%",
                        "Selection/forecast was not cleared after commit.");
                    interaction.BeginBottle(28, 12);
                    Check(interaction.gauge.CurrentLimit == 28, "New round limit did not update the dial.");
                    MainSceneTools.Capture(Camera.main, Output + "/ChangedLimit-28.png");
                    interaction.BeginBottle(20, 19);
                    Check(Click(interaction, interaction.strengthButtons[0]) && interaction.gauge.ProbabilityText == "100%", "Guaranteed burst preview is incorrect.");
                    bottleBeforeBurst = interaction.BottleNumber;
                    Check(Click(interaction, interaction.bottle), "Guaranteed burst could not commit.");
                    phase = 1;
                }
                else if (phase == 1 && !interaction.IsBusy)
                {
                    Check(interaction.LastResult.Burst && interaction.Model.Pressure == 0 &&
                        interaction.BottleNumber == bottleBeforeBurst + 1 && interaction.Model.Limit != 20 && resolvedEvents == 2,
                        "Burst did not prepare one fresh bottle and updated public limit.");
                    interaction.BeginBottle(999);
                    interaction.SelectStrength(1);
                    interaction.ConfirmShake();
                    interaction.enabled = false;
                    interaction.enabled = true;
                    phaseStarted = Time.unscaledTime;
                    phase = 2;
                }
                else if (phase == 2 && Time.unscaledTime - phaseStarted > 1.5f)
                {
                    Check(resolvedEvents == 2 && !interaction.IsBusy && interaction.CommittedActionCount == 0,
                        "Disabled interaction retained a stale animation or resolution callback.");
                    foreach (var name in new[] { CharacterIdleFactory.BossName, CharacterIdleFactory.ArmsName })
                        Check(GameObject.Find(name).GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Existing Idle was interrupted.");
                    report.AppendLine("PASS: separate cola click commits once; repeated clicks and changes during feedback are rejected.");
                    report.AppendLine("PASS: safe shake accumulates pressure; burst replaces bottle, resets selection and publishes a different limit.");
                    report.AppendLine("PASS: disable cancels in-flight feedback without a stale result; existing character Idles continue.");
                    report.AppendLine($"Temporary probability exponent: {interaction.settings.probabilityExponent}; new-bottle limits {interaction.settings.minimumBottleLimit}..{interaction.settings.maximumBottleLimit}.");
                    report.AppendLine("Scope: pressure-selection/confirmation prototype. AI, damage, token awards and shop logic are not simulated.");
                    File.WriteAllText(Output + "/Playmode-validation.txt", report.ToString());
                    phase = 3;
                    EditorApplication.isPlaying = false;
                }
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void PlayChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayChanged;
            EditorApplication.delayCall += () =>
            {
                try
                {
                    EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                    var interaction = ValidateSavedScene();
                    interaction.RefreshAuthoringView();
                    MainSceneTools.Capture(Camera.main, Output + "/MainScene-PressureInteraction.png");
                    MainSceneTools.Capture(Camera.main, MainSceneTools.Output + "/MainScene-preview.png");
                    CaptureDevices(interaction, Output + "/Gauge-Closeup.png");
                    MainSceneTools.WriteReport(GameObject.Find("COLA_COLA_MainScene"), Camera.main);
                    Debug.Log("COLA_PRESSURE_INTERACTION_VALIDATION_PASS");
                    if (Application.isBatchMode) MainSceneTools.Verify();
                }
                catch (Exception exception) { Fail(exception); }
            };
        }

        private static void CaptureDevices(ShakeSelectionInteraction interaction, string path)
        {
            var nodes = interaction.gauge.GetComponentsInChildren<Transform>(true)
                .Concat(interaction.strengthButtons[0].parent.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();
            var layers = nodes.Select(t => t.gameObject.layer).ToArray();
            var cameraObject = new GameObject("__GaugeInteractionPreview", typeof(Camera));
            try
            {
                foreach (var node in nodes) node.gameObject.layer = 31;
                var camera = cameraObject.GetComponent<Camera>();
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.045f, .058f, .052f);
                camera.orthographic = true;
                camera.orthographicSize = .32f;
                camera.nearClipPlane = .02f;
                camera.farClipPlane = 5;
                camera.transform.position = interaction.gauge.transform.TransformPoint(.03f, .10f, -.9f);
                camera.transform.LookAt(interaction.gauge.transform.position - Vector3.up * .10f);
                MainSceneTools.Capture(camera, path, 1100, 1100);
            }
            finally
            {
                for (int i = 0; i < nodes.Length; i++) nodes[i].gameObject.layer = layers[i];
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static Transform Find(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Fail(Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(Running, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayChanged;
            if (Application.isBatchMode) EditorApplication.Exit(2);
            else if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
