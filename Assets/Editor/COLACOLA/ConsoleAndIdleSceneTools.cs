using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColaCola.Editor
{
    [InitializeOnLoad]
    public static class ConsoleAndIdleSceneTools
    {
        private const string Running = "COLACOLA.ConsoleIdleVerification";
        private const string Started = Running + ".Started";
        private const string Error = Running + ".Error";
        private const string RuntimeStarted = Running + ".RuntimeStarted";
        private const string ReferenceFile = MainSceneTools.Output + "/Idle-reference-pose.json";
        private static PoseData reference;
        private static float nextCapture;
        private static int frames;
        private static float maxBossMotion;
        private static float maxArmsMotion;
        private static bool finishing;
        private static readonly List<float> frameTimes = new List<float>();
        private static float maxContactDrift;
        private static float minHandSurface;
        private static float nextSurfaceCheck;

        [Serializable] private class NodePose
        {
            public string character;
            public string path;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }
        [Serializable] private class ContactPose
        {
            public string character, path;
            public Vector3 worldPosition;
        }
        [Serializable] private class PoseData { public NodePose[] nodes; public ContactPose[] contacts; }

        static ConsoleAndIdleSceneTools()
        {
            if (SessionState.GetBool(Running, false)) InstallCallbacks();
        }

        public static GameObject InstantiateConsole(Transform parent, Vector3 position, Quaternion rotation)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ShakeConsoleModelFactory.PrefabPath);
            if (!asset) asset = ShakeConsoleModelFactory.BuildPrefab();
            var console = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            console.name = "ShakeConsole";
            console.transform.SetParent(parent, false);
            console.transform.SetPositionAndRotation(position, rotation);
            return console;
        }

        [MenuItem("COLA_COLA/Apply Console and Character Idles")]
        public static void Apply()
        {
            ApplyCore(false);
        }

        [MenuItem("COLA_COLA/Revise Player Facing Devices and Natural Idle")]
        public static void ReviseNaturalIdle()
        {
            ApplyCore(true);
        }

        private static void ApplyCore(bool revise)
        {
            try
            {
                finishing = false;
                Directory.CreateDirectory(MainSceneTools.Output);
                string backup = MainSceneTools.Output + "/MainScene-before-console-idle.unity";
                if (!File.Exists(backup)) File.Copy(MainSceneTools.ScenePath, backup);
                if (revise)
                {
                    string revisionBackup = MainSceneTools.Output + "/MainScene-before-natural-idle.unity";
                    if (!File.Exists(revisionBackup)) File.Copy(MainSceneTools.ScenePath, revisionBackup);
                    foreach (string clipName in new[] { "Boss_SeatedIdle", "PlayerArms_TableIdle" })
                    {
                        string oldClip = MainSceneTools.Output + "/" + clipName + "-before-natural.anim";
                        if (!File.Exists(oldClip)) File.Copy(CharacterIdleFactory.Folder + "/" + clipName + ".anim", oldClip);
                    }
                }
                var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene");
                var targets = root.GetComponentsInChildren<Transform>(true).Where(t =>
                    t.name == "PLACEHOLDER_ShakeConsole" || t.name.StartsWith("PLACEHOLDER_ShakeButton") || t.name == "ShakeConsole").ToArray();
                var oldConsole = targets.FirstOrDefault(t => t.name == "PLACEHOLDER_ShakeConsole" || t.name == "ShakeConsole");
                if (!oldConsole) throw new InvalidOperationException("No console anchor in saved MainScene.");
                Vector3 position = oldConsole.position;
                Quaternion rotation = oldConsole.rotation;
                var unchanged = root.GetComponentsInChildren<Transform>(true)
                    .Where(t => !targets.Any(target => t == target || t.IsChildOf(target)))
                    .Where(t => !t.GetComponentsInParent<Transform>().Any(p => p.name == "PressureGauge" || p.name == "03_Characters"))
                    .ToDictionary(t => t, t => new NodePose { position = t.localPosition, rotation = t.localRotation, scale = t.localScale });

                if (!revise) ShakeConsoleModelFactory.BuildPrefab();
                var devices = root.transform.Find("08_Devices");
                if (!devices) throw new InvalidOperationException("08_Devices group is missing.");
                var console = revise ? oldConsole.gameObject : InstantiateConsole(devices, position, rotation);
                if (!revise) foreach (var target in targets) UnityEngine.Object.DestroyImmediate(target.gameObject);
                string facingReport = PlayerFacingDeviceLayout.Apply(root, Camera.main);
                ProjectVerificationBridge.WriteReport(MainSceneTools.Output + "/PlayerFacingDevices-validation.txt", facingReport);
                CharacterIdleFactory.Apply(root);
                foreach (var pair in unchanged)
                    if (!pair.Key || Vector3.Distance(pair.Key.localPosition, pair.Value.position) > .00001f ||
                        Quaternion.Angle(pair.Key.localRotation, pair.Value.rotation) > .02f ||
                        Vector3.Distance(pair.Key.localScale, pair.Value.scale) > .00001f)
                        throw new InvalidOperationException("Unrelated placement/pose changed: " + pair.Key);

                if (!EditorSceneManager.SaveScene(scene, MainSceneTools.ScenePath)) throw new IOException("Could not save MainScene.");
                AssetDatabase.SaveAssets();
                ValidateAssets(root, console, unchanged.Count);
                MainSceneTools.WriteReport(root, Camera.main);
                var characters = new[] { CharacterIdleFactory.BossName, CharacterIdleFactory.ArmsName };
                reference = new PoseData
                {
                    nodes = characters.SelectMany(name =>
                    {
                        var character = GameObject.Find(name).transform;
                        return character.GetComponentsInChildren<Transform>(true).Select(t => new NodePose
                        {
                            character = name, path = AnimationUtility.CalculateTransformPath(t, character),
                            position = t.localPosition, rotation = t.localRotation, scale = t.localScale
                        });
                    }).ToArray(),
                    contacts = characters.SelectMany(name =>
                    {
                        var character = GameObject.Find(name).transform;
                        bool boss = name == CharacterIdleFactory.BossName;
                        return character.GetComponentsInChildren<Transform>().Where(t => t.name == (boss ? "hand.L" : "Wrist.L") || t.name == (boss ? "hand.R" : "Wrist.R"))
                            .Select(t => new ContactPose { character = name, path = AnimationUtility.CalculateTransformPath(t, character), worldPosition = t.position });
                    }).ToArray()
                };
                File.WriteAllText(ReferenceFile, JsonUtility.ToJson(reference, true));
                Directory.CreateDirectory(MainSceneTools.Output + "/idle-frames");
                SessionState.SetBool(Running, true);
                SessionState.SetString(Started, DateTime.UtcNow.ToString("O"));
                SessionState.EraseString(Error);
                SessionState.SetFloat(RuntimeStarted, -1);
                InstallCallbacks();
                Debug.Log("COLA_CONSOLE_IDLE_APPLIED: entering real Play mode for autoplay validation.");
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void ValidateAssets(GameObject root, GameObject console, int unchangedCount)
        {
            var report = new StringBuilder();
            var meshes = console.GetComponentsInChildren<MeshFilter>(true);
            if (meshes.Length < 6 || meshes.Any(m => !m.sharedMesh || m.sharedMesh.name == "Cube"))
                throw new InvalidOperationException("Expected actual custom console meshes.");
            for (int i = 1; i <= 3; i++)
            {
                var button = console.GetComponentsInChildren<Transform>().Single(t => t.name == "ShakeButton" + i);
                if (!button.GetComponentInChildren<MeshFilter>()) throw new InvalidOperationException("Missing keycap mesh.");
            }
            report.AppendLine("PASS: console and three button Cubes replaced with an authored mesh prefab.");
            report.AppendLine("PASS: " + unchangedCount + " unrelated stage local transforms preserved; supported character rest poses calibrated separately.");
            report.AppendLine("Console meshes: " + meshes.Length + ", triangles: " + meshes.Sum(m => m.sharedMesh.triangles.Length / 3));
            report.AppendLine("Independent key transforms: ShakeButton1 / ShakeButton2 / ShakeButton3; press direction: local -Y.");
            foreach (var name in new[] { CharacterIdleFactory.BossName, CharacterIdleFactory.ArmsName })
            {
                var character = root.GetComponentsInChildren<Transform>().Single(t => t.name == name);
                var animator = character.GetComponent<Animator>();
                if (!animator || !animator.enabled || !animator.runtimeAnimatorController || animator.applyRootMotion)
                    throw new InvalidOperationException("Idle Animator configuration missing: " + name);
                var clip = animator.runtimeAnimatorController.animationClips.Single();
                if (!AnimationUtility.GetAnimationClipSettings(clip).loopTime) throw new InvalidOperationException("Clip is not a loop.");
                float maxSeam = 0;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (string.IsNullOrEmpty(binding.path) || !character.Find(binding.path))
                        throw new InvalidOperationException("Invalid/placement-root animation binding: " + binding.path);
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    maxSeam = Mathf.Max(maxSeam, Mathf.Abs(curve.Evaluate(0) - curve.Evaluate(clip.length)));
                    if (Mathf.Abs(curve.keys[0].outTangent - curve.keys[curve.length - 1].inTangent) > .002f)
                        throw new InvalidOperationException("Loop tangent seam: " + binding.path + "/" + binding.propertyName);
                }
                if (maxSeam > .00001f) throw new InvalidOperationException("Loop pose seam: " + name);
                report.AppendLine($"PASS: {name}: {clip.name}, {clip.length:F2}s, loop enabled, all bindings valid, endpoint difference {maxSeam:F8}.");
            }
            ProjectVerificationBridge.WriteReport(MainSceneTools.Output + "/ConsoleIdle-assets-validation.txt", report.ToString());
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
                if (DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Started, "")) > TimeSpan.FromSeconds(100))
                    throw new TimeoutException("Idle Play-mode validation exceeded 100 seconds.");
                if (!EditorApplication.isPlaying || finishing) return;
                if (Time.frameCount < 2) return;
                if (reference == null) reference = JsonUtility.FromJson<PoseData>(File.ReadAllText(ReferenceFile));
                float began = SessionState.GetFloat(RuntimeStarted, -1);
                if (began < 0)
                {
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = 30;
                    foreach (var name in new[] { CharacterIdleFactory.BossName, CharacterIdleFactory.ArmsName })
                    {
                        var animator = GameObject.Find(name).GetComponent<Animator>();
                        var automaticallyStartedState = animator.GetCurrentAnimatorStateInfo(0);
                        if (!automaticallyStartedState.IsName("Idle") || automaticallyStartedState.normalizedTime <= 0)
                            throw new InvalidOperationException("Default Idle did not start automatically: " + name);
                        // Default startup has already been observed; reset only to compare the exact reference pose.
                        animator.Play("Idle", 0, 0);
                        animator.Update(0);
                    }
                    foreach (var pose in reference.nodes)
                    {
                        var bone = Resolve(pose);
                        if (Vector3.Distance(bone.localPosition, pose.position) > .00003f ||
                            Quaternion.Angle(bone.localRotation, pose.rotation) > .04f ||
                            Vector3.Distance(bone.localScale, pose.scale) > .00003f)
                            throw new InvalidOperationException("Animator start changed authored pose: " + pose.character + "/" + pose.path);
                    }
                    SessionState.SetFloat(RuntimeStarted, Time.unscaledTime);
                    nextCapture = 0;
                    frames = 0;
                    frameTimes.Clear();
                    maxBossMotion = maxArmsMotion = 0;
                    maxContactDrift = 0;
                    minHandSurface = float.PositiveInfinity;
                    nextSurfaceCheck = 0;
                    MainSceneTools.Capture(Camera.main, MainSceneTools.Output + "/MainScene-idle-start.png");
                    CaptureDevices();
                    Debug.Log("COLA_IDLE_PLAY_START_POSE_PASS");
                    return;
                }
                float elapsed = Time.unscaledTime - began;
                foreach (var pose in reference.nodes)
                {
                    var bone = Resolve(pose);
                    float angle = Quaternion.Angle(bone.localRotation, pose.rotation);
                    if (string.IsNullOrEmpty(pose.path))
                    {
                        if (Vector3.Distance(bone.localPosition, pose.position) > .00002f || angle > .02f)
                            throw new InvalidOperationException("Character placement moved during Idle: " + pose.character);
                    }
                    if (angle > 8) throw new InvalidOperationException("Idle pose exceeded subtle movement budget: " + pose.path);
                    if (pose.character == CharacterIdleFactory.BossName) maxBossMotion = Mathf.Max(maxBossMotion, angle);
                    else maxArmsMotion = Mathf.Max(maxArmsMotion, angle);
                }
                foreach (var contact in reference.contacts)
                {
                    var character = GameObject.Find(contact.character);
                    var wrist = character.transform.Find(contact.path);
                    maxContactDrift = Mathf.Max(maxContactDrift, Vector3.Distance(wrist.position, contact.worldPosition));
                    if (elapsed >= nextSurfaceCheck)
                        minHandSurface = Mathf.Min(minHandSurface, CharacterIdleFactory.HandMinimumY(character, wrist));
                }
                if (elapsed >= nextSurfaceCheck) nextSurfaceCheck = elapsed + .75f;
                if (maxContactDrift > .0008f) throw new InvalidOperationException("Resting hand slid on the table: " + maxContactDrift);
                if (minHandSurface < .829f) throw new InvalidOperationException("Hand mesh penetrated tabletop: " + minHandSurface);
                if (elapsed >= nextCapture && elapsed <= CharacterIdleFactory.Duration + .6f)
                {
                    MainSceneTools.Capture(Camera.main, MainSceneTools.Output + $"/idle-frames/Idle-{frames:D3}.png", 960, 540);
                    frameTimes.Add(elapsed);
                    frames++;
                    nextCapture = elapsed + .18f;
                }
                if (elapsed < CharacterIdleFactory.Duration + 1f) return;
                if (new[] { CharacterIdleFactory.BossName, CharacterIdleFactory.ArmsName }
                    .Any(name => GameObject.Find(name).GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)) return;
                var report = new StringBuilder();
                foreach (var name in new[] { CharacterIdleFactory.BossName, CharacterIdleFactory.ArmsName })
                {
                    var animator = GameObject.Find(name).GetComponent<Animator>();
                    var state = animator.GetCurrentAnimatorStateInfo(0);
                    if (!state.IsName("Idle") || state.normalizedTime < 1 || !state.loop)
                        throw new InvalidOperationException("Animator did not autoplay a full Idle loop: " + name + " time=" + state.normalizedTime);
                    report.AppendLine($"PASS: {name}: active Idle, normalizedTime={state.normalizedTime:F3}, loop={state.loop}.");
                }
                if (maxBossMotion < .3f || maxArmsMotion < .3f) throw new InvalidOperationException("Idle bones did not move.");
                report.AppendLine("PASS: Animator starts at the exact scene-authored seated/arm pose; no T-pose reset.");
                report.AppendLine("PASS: both controllers entered their default Idle automatically before any explicit Animator.Play call.");
                report.AppendLine("PASS: character placement roots stayed fixed throughout real Play mode.");
                report.AppendLine($"PASS: resting wrist contact drift <= {maxContactDrift * 1000:F4} mm; minimum sampled hand surface Y={minHandSurface:F5}, tabletop Y=.83000.");
                report.AppendLine($"Maximum observed local joint motion: boss={maxBossMotion:F3} degrees, arms={maxArmsMotion:F3} degrees.");
                report.AppendLine($"Runtime capture: {frames} frames over {elapsed:F2} seconds, Unity {Application.unityVersion}.");
                report.AppendLine("No root motion, input, shake action, boss AI or gameplay rules added.");
                ProjectVerificationBridge.WriteReport(MainSceneTools.Output + "/Idle-playmode-validation.txt", report.ToString());
                File.WriteAllLines(MainSceneTools.Output + "/Idle-frame-times.txt", frameTimes.Select(t => t.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)));
                MainSceneTools.Capture(Camera.main, MainSceneTools.Output + "/MainScene-idle-playing.png");
                Debug.Log("COLA_IDLE_REAL_PLAYMODE_PASS\n" + report);
                finishing = true;
                EditorApplication.isPlaying = false;
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static Transform Resolve(NodePose pose)
        {
            var root = GameObject.Find(pose.character).transform;
            return string.IsNullOrEmpty(pose.path) ? root : root.Find(pose.path);
        }

        private static void CaptureDevices()
        {
            var deviceGroup = GameObject.Find("COLA_COLA_MainScene").transform.Find("08_Devices");
            var nodes = deviceGroup.GetComponentsInChildren<Transform>();
            var layers = nodes.Select(t => t.gameObject.layer).ToArray();
            var go = new GameObject("__ConsolePreview", typeof(Camera));
            try
            {
                foreach (var node in nodes) node.gameObject.layer = 31;
                var camera = go.GetComponent<Camera>();
                camera.cullingMask = 1 << 31;
                camera.orthographic = true;
                camera.orthographicSize = .34f;
                camera.nearClipPlane = .02f;
                camera.farClipPlane = 5;
                camera.backgroundColor = new Color(.055f, .068f, .065f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.allowHDR = camera.allowMSAA = false;
                camera.transform.position = new Vector3(-.34f, 1.48f, -1.1f);
                camera.transform.LookAt(new Vector3(-.74f, 1.1f, -.15f));
                MainSceneTools.Capture(camera, MainSceneTools.Output + "/ShakeConsole-closeup.png", 1000, 1000);
            }
            finally
            {
                for (int i = 0; i < nodes.Length; i++) nodes[i].gameObject.layer = layers[i];
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void PlayStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayStateChanged;
            string error = SessionState.GetString(Error, "");
            if (!string.IsNullOrEmpty(error))
            {
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }
            // Reopen the saved edit-time scene; Play-mode changes never get serialized.
            MainSceneTools.Verify();
        }

        private static void Fail(Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetString(Error, exception.ToString());
            ProjectVerificationBridge.WriteReport(MainSceneTools.Output + "/ConsoleIdle-error.txt", exception.ToString());
            finishing = true;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else
            {
                SessionState.SetBool(Running, false);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
