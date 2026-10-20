using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColaCola.Editor
{
    /// <summary>Local file mailbox. Runs only whitelisted Play probes inside the already-open Editor.</summary>
    [InitializeOnLoad]
    public static class ProjectVerificationBridge
    {
        public const string Root = "output/verification/latest";
        public const string RequestPath = "Library/COLA_COLA/verification-request.json";
        private const string SessionKey = "COLA_COLA.VerificationBridge";
        [Serializable] public sealed class Request { public string id; public string suite; }
        [Serializable] private sealed class SceneRecord { public string path; public bool isLoaded, isActive; }
        [Serializable] private sealed class Run
        {
            public string id, suite, phase, message, startedUtc, heartbeatUtc;
            public int editorProcessId;
            public bool batchMode, success;
            public bool playMode;
            public int frameCount;
            public List<string> passed = new();
            public List<SceneRecord> scenes = new();
        }
        private static Run run;
        private static double pollAt;
        private static double heartbeatAt;
        private static bool subscribed;
        public static bool FullRun => run != null && run.suite == "full" && run.phase == "running";
        public static bool MusicOnly => run != null && run.suite == "music";
        public static bool ArmMotionOnly => run != null && run.id.StartsWith("shake-arms-", StringComparison.Ordinal);
        public static bool CameraMotionOnly => run != null && run.id.StartsWith("shake-camera-", StringComparison.Ordinal);
        public static bool RetroScreenOnly => run != null && run.id.StartsWith("shake-retro-", StringComparison.Ordinal);
        public static bool PizzeriaLookOnly => RetroScreenOnly || run != null && run.id.StartsWith("shake-look-", StringComparison.Ordinal);
        private static bool Active => run != null && run.phase != "passed" && run.phase != "failed";

        public static void Schedule(EditorApplication.CallbackFunction action)
        {
            // delayCall depends on Inspector updates; use the Editor update loop even when its window is in the background.
            void Once()
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                EditorApplication.update -= Once;
                if (run == null || run.phase != "stopping" && run.phase != "failed") action();
            }
            EditorApplication.update += Once;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public static void WriteReport(string path, string contents)
        {
            path = path.Replace('\\', '/');
            if (!path.StartsWith(Root + "/", StringComparison.Ordinal))
            {
                if (!path.StartsWith("output/", StringComparison.Ordinal))
                    throw new ArgumentException("Verification reports must use an output path.", nameof(path));
                path = Root + "/" + path.Substring("output/".Length);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
        }

        static ProjectVerificationBridge()
        {
            string saved = SessionState.GetString(SessionKey, "");
            if (!string.IsNullOrEmpty(saved)) run = JsonUtility.FromJson<Run>(saved);
            if (run != null && run.id.StartsWith("shake-", StringComparison.Ordinal)) run.suite = "shake";
            if (run != null && run.id.StartsWith("turnui-", StringComparison.Ordinal)) run.suite = "turn-ui";
            if (run != null && run.id.StartsWith("fonts-", StringComparison.Ordinal)) run.suite = "fonts";
            if (run != null && run.editorProcessId != Process.GetCurrentProcess().Id) run = null;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
            subscribed = true;
        }

        [MenuItem("COLA_COLA/Validation/Run Full Play Verification")]
        public static void RequestFull() => Enqueue("full");
        [MenuItem("COLA_COLA/Validation/Verify BGM In Play Mode")]
        public static void RequestMusic() => Enqueue("music");
        [MenuItem("COLA_COLA/Validation/Cancel Verification")]
        public static void Cancel() { if (Active) Stop(false, "Cancelled by user."); }

        private static void Enqueue(string suite)
        {
            if (Active) return;
            Directory.CreateDirectory(Path.GetDirectoryName(RequestPath));
            File.WriteAllText(RequestPath, JsonUtility.ToJson(new Request { id = Guid.NewGuid().ToString("N"), suite = suite }, true));
        }

        private static void Persist()
        {
            run.heartbeatUtc = DateTime.UtcNow.ToString("O");
            run.playMode = EditorApplication.isPlaying;
            run.frameCount = Time.frameCount;
            Directory.CreateDirectory(Root);
            string json = JsonUtility.ToJson(run, true);
            SessionState.SetString(SessionKey, json);
            File.WriteAllText(Root + "/status.json", json);
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < pollAt) return;
            pollAt = EditorApplication.timeSinceStartup + .3;
            try
            {
                if (Active && File.Exists(RequestPath))
                {
                    var cancellation = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
                    if (cancellation != null && cancellation.suite == "cancel")
                    { File.Delete(RequestPath); Stop(false, "Cancelled through verification mailbox."); }
                }
                if (!Active && File.Exists(RequestPath))
                {
                    var request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
                    File.Delete(RequestPath);
                    if (request == null || string.IsNullOrWhiteSpace(request.id) ||
                        !new[] { "music", "full", "burst", "orders", "items", "duel" }.Contains(request.suite))
                        return;
                    run = new Run { id = request.id, suite = request.suite, phase = "preparing",
                        message = "Importing project changes in the open Editor.", startedUtc = DateTime.UtcNow.ToString("O"),
                        editorProcessId = Process.GetCurrentProcess().Id, batchMode = Application.isBatchMode };
                    if (request.id.StartsWith("shake-", StringComparison.Ordinal)) run.suite = "shake";
                    if (request.id.StartsWith("turnui-", StringComparison.Ordinal)) run.suite = "turn-ui";
                    if (request.id.StartsWith("fonts-", StringComparison.Ordinal)) run.suite = "fonts";
                    Directory.CreateDirectory(Root);
                    File.WriteAllText(Root + "/verification.log", "Open-editor Play verification " + run.id + "\n");
                    Persist();
                    if (request.suite == "music") ColaMusicVerification.WriteLiveSnapshot();
                    AssetDatabase.Refresh();
                    return;
                }
                if (!Active || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                if (EditorApplication.timeSinceStartup >= heartbeatAt)
                { heartbeatAt = EditorApplication.timeSinceStartup + 2; Persist(); }
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.isPlaying)
                {
                    Application.runInBackground = true;
                    EditorApplication.isPaused = false;
                }
                if (run.phase == "stopping")
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
                    if (run.scenes.Count > 0)
                        EditorSceneManager.RestoreSceneManagerSetup(run.scenes.Select(s => new SceneSetup
                            { path = s.path, isLoaded = s.isLoaded, isActive = s.isActive }).ToArray());
                    run.phase = run.success ? "passed" : "failed";
                    run.message += " Original scene setup restored; Editor left open.";
                    Persist();
                    File.WriteAllText(Root + "/Summary.md", "# Latest Play verification\n\n" +
                        "- Result: " + run.phase + "\n- Suite: " + run.suite + "\n- Editor process: " + run.editorProcessId +
                        "\n- Batch mode: " + run.batchMode + "\n- Started UTC: " + run.startedUtc + "\n- " + run.message +
                        "\n\n" + string.Join("\n", run.passed.Select(p => "- PASS: " + p)) +
                        "\n\nScope: actual Unity Play-mode automated state/input/audio checks. Game-view visual inspection and listening are separate.\n");
                    if (run.suite == "full") File.Copy(Root + "/Summary.md", Root + "/Full-suite.md", true);
                    return;
                }
                if (run.phase == "preparing" || run.phase == "waiting_for_saved_scenes")
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
                    if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                    {
                        run.phase = "waiting_for_saved_scenes";
                        run.message = "Finish and save Prefab Mode before verification; prefab work has not been discarded.";
                        Persist(); return;
                    }
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                        if (SceneManager.GetSceneAt(i).isDirty || string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                        {
                            run.phase = "waiting_for_saved_scenes";
                            run.message = "Save the open scenes to continue; no scene changes have been discarded.";
                            Persist(); return;
                        }
                    run.scenes = EditorSceneManager.GetSceneManagerSetup().Select(s => new SceneRecord
                        { path = s.path, isLoaded = s.isLoaded, isActive = s.isActive }).ToList();
                    run.phase = "running"; run.message = "Running actual Play-mode probes."; Persist();
                    Schedule(Dispatch);
                    return;
                }
                if (DateTime.UtcNow - DateTime.Parse(run.startedUtc) > TimeSpan.FromMinutes(15))
                    Stop(false, "Verification timed out; review verification.log.");
            }
            catch (Exception exception) { if (Active) Stop(false, exception.ToString()); }
        }

        private static void Dispatch()
        {
            if (!Active || run.phase != "running") return;
            switch (run.suite)
            {
                case "full": case "music": ColaMusicVerification.Begin(); break;
                case "orders": ColaOrderSheetVerification.Begin(); break;
                case "burst": ColaBurstVerification.Begin(); break;
                case "items": ColaItemVerification.Begin(); break;
                case "duel": PlayableDuelVerification.Begin(); break;
                case "shake": ColaShakePlayReview.Begin(); break;
                case "turn-ui": ColaTurnLabelVerification.Begin(); break;
                case "fonts": ColaTurnLabelVerification.Begin(true); break;
            }
        }

        private static void OnLog(string message, string trace, LogType type)
        {
            if (!Active || !subscribed) return;
            File.AppendAllText(Root + "/verification.log", DateTime.UtcNow.ToString("O") + " [" + type + "] " + message +
                (type == LogType.Exception || type == LogType.Error ? "\n" + trace : "") + "\n");
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            { Stop(false, message); return; }
            if (message.StartsWith("Can't add script behaviour", StringComparison.Ordinal))
            { Stop(false, message); return; }
            string first = message.Split('\n')[0].Trim();
            if (!first.StartsWith("COLA_") || !first.EndsWith("_PASS")) return;
            if (!run.passed.Contains(first)) run.passed.Add(first);
            run.message = first; Persist();
            if (first == "COLA_FONT_READABILITY_PASS" && run.suite == "fonts")
            {
                Stop(true, "Bundled-font/UI checks completed in actual Play. Actual Game-view captures require visual review.");
                return;
            }
            if (first == "COLA_TURN_LABEL_PASS" && run.suite == "turn-ui")
            {
                Stop(true, "Physical turn-indicator checks completed in actual Play. Game-view captures saved for visual review.");
                return;
            }
            if (first == "COLA_SHAKE_REVIEW_PASS" && run.suite == "shake")
            {
                Stop(true, RetroScreenOnly ? "Retro pixel screen and natural/burst glitch paths exercised in actual Play. Actual Game-view captures saved for visual inspection." :
                    PizzeriaLookOnly ? "Pizzeria staff and lighting observed during actual Play actions. Two actual Game-view captures saved for visual inspection." :
                    CameraMotionOnly ? "Camera onset and actual frame timing measured in Play. No screenshots or continuous Game-view visual review." :
                    "Original-rig shake checks completed in Play. Actual Game-view captures saved for separate visual inspection.");
                return;
            }
            if (first == "COLA_MUSIC_PLAYMODE_PASS" && run.suite == "music")
            {
                Stop(true, "Requested BGM Play checks passed; audio clip identity and digital playback checked. Speaker listening and Game view not inspected.");
                return;
            }
            if (first == "COLA_MAIN_SCENE_VALIDATION_PASS")
            {
                var required = run.suite == "full" ? new[] { "COLA_MUSIC_PLAYMODE_PASS", "COLA_ORDER_SHEET_PLAYMODE_PASS",
                    "COLA_BOSS_ITEMS_PLAYMODE_PASS", "COLA_BURST_PLAYMODE_PASS", "COLA_ITEMS_PLAYMODE_PASS", "COLA_PLAYABLE_DUEL_PASS" } :
                    run.suite == "music" ? new[] { "COLA_MUSIC_PLAYMODE_PASS" } : Array.Empty<string>();
                Stop(required.All(run.passed.Contains), required.All(run.passed.Contains) ? "All requested Play checks passed." : "A required suite did not complete.");
            }
        }

        private static void Stop(bool success, string reason)
        {
            if (!Active) return;
            run.success = success; run.phase = "stopping"; run.message = reason;
            if (!success)
                foreach (string key in new[] { "COLA_COLA.MusicVerification", "COLA_COLA.OrderSheetVerification", "COLA_COLA.BossItemVerification",
                    "COLA_COLA.BurstVerification", "COLA_COLA.ItemVerification", "COLA_COLA.PlayableDuelVerification", "COLA_COLA.MainScene.PlayVerification" })
                    SessionState.SetBool(key, false);
            if (!success) SessionState.SetBool("COLA_COLA.ShakePlayReview", false);
            Persist();
        }
    }
}
