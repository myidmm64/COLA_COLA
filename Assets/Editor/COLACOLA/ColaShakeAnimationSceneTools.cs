using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ColaCola.Presentation;

namespace ColaCola.Editor
{
    [InitializeOnLoad]
    public static class ColaShakeAnimationSceneTools
    {
        public const string Output = "output/shake-animation";
        private const string VerifyAutomaticPreview = "COLA_COLA.VerifyAutomaticPlayerPreview";
        private static float automaticPreviewStarted;
        private static Vector3 automaticPreviewRestHand;
        private static int automaticPreviewCycles;
        private static bool automaticPreviewWasPlaying;

        static ColaShakeAnimationSceneTools()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModePreview;
            EditorApplication.playModeStateChanged += HandlePlayModePreview;
        }

        private static void HandlePlayModePreview(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode &&
                (!Application.isBatchMode || SessionState.GetBool(VerifyAutomaticPreview, false)))
            {
                var driver = UnityEngine.Object.FindAnyObjectByType<ColaShakeAnimator>();
                if (driver && driver.previewPlayerOnPlay) driver.BeginPlayerPlayPreview();
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
                UnityEngine.Object.FindAnyObjectByType<ColaShakeAnimator>()?.StopPlayerPlayPreview();
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(VerifyAutomaticPreview, false))
            {
                SessionState.SetBool(VerifyAutomaticPreview, false);
                EditorApplication.update -= VerifyAutomaticPreviewTick;
                EditorApplication.delayCall += () =>
                {
                    EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                    ColaShakeAnimationVerification.Begin();
                };
            }
        }

        [MenuItem("COLA_COLA/Enable Player Hand Preview On Play")]
        public static void EnablePlayerPreviewOnPlay()
        {
            try
            {
                Directory.CreateDirectory(Output);
                var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                var driver = UnityEngine.Object.FindAnyObjectByType<ColaShakeAnimator>();
                if (!driver || !driver.CanPlay(false, 2)) throw new InvalidOperationException("Player hand/clip bindings are incomplete.");
                driver.previewPlayerOnPlay = true;
                driver.playPreviewStrength = 2;
                EditorUtility.SetDirty(driver);
                if (!EditorSceneManager.SaveScene(scene, MainSceneTools.ScenePath)) throw new IOException("MainScene save failed.");
                if (!Application.isBatchMode) return;
                automaticPreviewStarted = (float)EditorApplication.timeSinceStartup;
                automaticPreviewRestHand = driver.playerHand.position;
                automaticPreviewCycles = 0;
                automaticPreviewWasPlaying = false;
                SessionState.SetBool(VerifyAutomaticPreview, true);
                EditorApplication.update += VerifyAutomaticPreviewTick;
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void VerifyAutomaticPreviewTick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - automaticPreviewStarted > 60)
                    throw new TimeoutException("Automatic Play preview did not run.");
                if (!EditorApplication.isPlaying) return;
                var driver = UnityEngine.Object.FindAnyObjectByType<ColaShakeAnimator>();
                if (!driver) throw new InvalidOperationException("Play preview driver was not found.");
                if (driver.IsPlaying && !automaticPreviewWasPlaying) automaticPreviewCycles++;
                if (driver.IsPlaying && driver.BottleHeld && driver.PlaybackTime >= 1.8f)
                {
                    if (driver.PlayingBoss || !driver.UsesAnimatorGraph || !driver.playerAnimator.enabled ||
                        Vector3.Distance(driver.playerHand.position, automaticPreviewRestHand) < .1f ||
                        driver.playerHand.position.x - driver.bottle.position.x < .06f)
                        throw new InvalidOperationException("Automatic preview did not move the original right hand on the bottle's right side.");
                    if (automaticPreviewCycles >= 2)
                    {
                        driver.StopPlayerPlayPreview();
                        if (driver.IsPlaying || !driver.playerAnimator.enabled ||
                            !driver.playerAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
                            throw new InvalidOperationException("Stopping the automatic preview did not restore Idle.");
                        ProjectVerificationBridge.WriteReport(Output + "/Automatic-preview-validation.txt",
                            "PASS: entering Play automatically starts the player hand preview without clicks or menu actions.\n" +
                            "PASS: two preview cycles use a live Animator/PlayableGraph and the original right hand stays on the bottle's right side.\n" +
                            "PASS: stop returns the actor and bottle to Idle; the saved scene keeps previewPlayerOnPlay enabled.\n");
                        Debug.Log("COLA_AUTOMATIC_PLAYER_PREVIEW_PASS");
                        EditorApplication.isPlaying = false;
                    }
                }
                automaticPreviewWasPlaying = driver.IsPlaying;
            }
            catch (Exception exception)
            {
                EditorApplication.update -= VerifyAutomaticPreviewTick;
                SessionState.SetBool(VerifyAutomaticPreview, false);
                Fail(exception);
            }
        }

        [MenuItem("COLA_COLA/Revise Player Right Side Grip")]
        public static void ApplyPlayerRightSideGrip()
        {
            try
            {
                Directory.CreateDirectory(Output);
                var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene");
                ColaShakeAnimationFactory.BuildPlayerOnly(root);
                var at = EditorApplication.timeSinceStartup + 3;
                EditorApplication.CallbackFunction finish = null;
                finish = () =>
                {
                    if (EditorApplication.timeSinceStartup < at) return;
                    EditorApplication.update -= finish;
                    try
                    {
                        Debug.Log("COLA_PLAYER_RIGHT_SIDE_APPLIED");
                        ColaShakeAnimationVerification.Begin();
                    }
                    catch (Exception exception) { Fail(exception); }
                };
                EditorApplication.update += finish;
            }
            catch (Exception exception) { Fail(exception); }
        }

        [MenuItem("COLA_COLA/Build Player and Boss Cola Shake Animations")]
        public static void Apply()
        {
            try
            {
                Directory.CreateDirectory(Output);
                var backup = Output + "/MainScene-before-shake.unity";
                if (!File.Exists(backup)) File.Copy(MainSceneTools.ScenePath, backup);
                var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene");
                RemoveAddedSleeve(root);
                var preserved = root.GetComponentsInChildren<Transform>(true)
                    .ToDictionary(t => t, t => (position: t.position, rotation: t.rotation, scale: t.localScale));
                ColaShakeAnimationFactory.Build(root);
                ApplyToScene(root);
                foreach (var pair in preserved)
                    if (!pair.Key || Vector3.Distance(pair.Key.position, pair.Value.position) > .00001f ||
                        Quaternion.Angle(pair.Key.rotation, pair.Value.rotation) > .06f ||
                        Vector3.Distance(pair.Key.localScale, pair.Value.scale) > .00001f)
                        throw new InvalidOperationException("Authored rest placement changed: " + pair.Key);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene, MainSceneTools.ScenePath)) throw new IOException("MainScene save failed.");
                ProjectVerificationBridge.WriteReport(Output + "/Placement-preservation.txt",
                    $"PASS: {preserved.Count} existing transforms, root scales, seated poses, camera, cola, devices, inventories and lighting preserved.\n" +
                    "Updated own-hand bindings and six actor/bottle clip pairs. Original Idle clips/controllers and imported FBX assets are unchanged.\n");
                MainSceneTools.WriteReport(root, Camera.main);
                var at = EditorApplication.timeSinceStartup + 3;
                EditorApplication.CallbackFunction finish = null;
                finish = () =>
                {
                    if (EditorApplication.timeSinceStartup < at) return;
                    EditorApplication.update -= finish;
                    try
                    {
                        Debug.Log("COLA_CHARACTER_SHAKE_APPLIED");
                        ColaShakeAnimationVerification.Begin();
                    }
                    catch (Exception exception) { Fail(exception); }
                };
                EditorApplication.update += finish;
            }
            catch (Exception exception) { Fail(exception); }
        }

        public static void ApplyToScene(GameObject root)
        {
            RemoveAddedSleeve(root);
            if (!AssetDatabase.LoadAssetAtPath<AnimationClip>(ColaShakeAnimationFactory.ActorClipPath(false, 1)))
                ColaShakeAnimationFactory.Build(root);
            var group = root.transform.Find("08_Devices");
            var node = group.Find("ColaShakeAnimation");
            if (!node) { node = new GameObject("ColaShakeAnimation").transform; node.SetParent(group, false); }
            var driver = node.GetComponent<ColaShakeAnimator>();
            if (!driver) driver = node.gameObject.AddComponent<ColaShakeAnimator>();
            driver.playerAnimator = Find(root, CharacterIdleFactory.ArmsName).GetComponent<Animator>();
            driver.bossAnimator = Find(root, CharacterIdleFactory.BossName).GetComponent<Animator>();
            driver.playerHand = Find(driver.playerAnimator.gameObject, "Wrist.R");
            driver.bossHand = Find(driver.bossAnimator.gameObject, "hand.R");
            driver.bottle = Find(root, "HeroCola_ExistingModel");
            driver.playerClips = Clips(false, false); driver.bossClips = Clips(true, false);
            driver.playerBottleClips = Clips(false, true); driver.bossBottleClips = Clips(true, true);
            driver.idleBlendSeconds = .20f;
            var interaction = Find(root, "ShakeSelection").GetComponent<ShakeSelectionInteraction>();
            interaction.shakeAnimator = driver;
            interaction.automaticBossTurns = true;
            EditorUtility.SetDirty(driver); EditorUtility.SetDirty(interaction);
        }

        [MenuItem("COLA_COLA/Preview Player Cola Shake (Play Mode)")]
        public static void PreviewPlayer() => Preview(false);
        [MenuItem("COLA_COLA/Preview Boss Cola Shake (Play Mode)")]
        public static void PreviewBoss() => Preview(true);
        private static void Preview(bool boss)
        {
            if (!EditorApplication.isPlaying) { Debug.Log("Enter Play mode, then preview the cola shake."); return; }
            var driver = UnityEngine.Object.FindFirstObjectByType<ColaShakeAnimator>();
            if (!driver) throw new InvalidOperationException("Apply cola shake animations to MainScene first.");
            if (boss) driver.PreviewBossShake(); else driver.PreviewPlayerShake();
        }

        private static AnimationClip[] Clips(bool boss, bool bottle) => Enumerable.Range(1, 3)
            .Select(strength => AssetDatabase.LoadAssetAtPath<AnimationClip>(bottle
                ? ColaShakeAnimationFactory.BottleClipPath(boss, strength) : ColaShakeAnimationFactory.ActorClipPath(boss, strength))).ToArray();

        private static void RemoveAddedSleeve(GameObject root)
        {
            var extra = root.transform.Find("08_Devices/PlayerSleeveContinuation");
            if (extra) UnityEngine.Object.DestroyImmediate(extra.gameObject);
        }

        private static Transform Find(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        private static void Fail(Exception exception)
        {
            Directory.CreateDirectory(Output); ProjectVerificationBridge.WriteReport(Output + "/Animation-error.txt", exception.ToString());
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(2);
            else throw new InvalidOperationException("Cola shake animation authoring failed.", exception);
        }
    }
}
