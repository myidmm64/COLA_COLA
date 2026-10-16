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
    public static class ColaShakeAnimationSceneTools
    {
        public const string Output = "output/shake-animation";

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
                var preserved = root.GetComponentsInChildren<Transform>(true)
                    .ToDictionary(t => t, t => (position: t.position, rotation: t.rotation, scale: t.localScale));
                PlayerSleeveContinuationFactory.Apply(root);
                ColaShakeAnimationFactory.Build(root);
                ApplyToScene(root);
                foreach (var pair in preserved)
                    if (!pair.Key || Vector3.Distance(pair.Key.position, pair.Value.position) > .00001f ||
                        Quaternion.Angle(pair.Key.rotation, pair.Value.rotation) > .06f ||
                        Vector3.Distance(pair.Key.localScale, pair.Value.scale) > .00001f)
                        throw new InvalidOperationException("Authored rest placement changed: " + pair.Key);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene, MainSceneTools.ScenePath)) throw new IOException("MainScene save failed.");
                File.WriteAllText(Output + "/Placement-preservation.txt",
                    $"PASS: {preserved.Count} existing transforms, root scales, seated poses, camera, cola, devices, inventories and lighting preserved.\n" +
                    "Only a playback component and six actor/bottle clip pairs are added. Original Idle clips/controllers and imported FBX assets are unchanged.\n");
                MainSceneTools.WriteReport(root, Camera.main);
                var at = EditorApplication.timeSinceStartup + 3;
                EditorApplication.CallbackFunction finish = null;
                finish = () =>
                {
                    if (EditorApplication.timeSinceStartup < at) return;
                    EditorApplication.update -= finish;
                    try
                    {
                        CapturePoses();
                        CaptureSmoothPreviews();
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
            PlayerSleeveContinuationFactory.Apply(root);
            if (!AssetDatabase.LoadAssetAtPath<AnimationClip>(ColaShakeAnimationFactory.ActorClipPath(false, 1)))
                ColaShakeAnimationFactory.Build(root);
            var group = root.transform.Find("08_Devices");
            var node = group.Find("ColaShakeAnimation");
            if (!node) { node = new GameObject("ColaShakeAnimation").transform; node.SetParent(group, false); }
            var driver = node.GetComponent<ColaShakeAnimator>();
            if (!driver) driver = node.gameObject.AddComponent<ColaShakeAnimator>();
            driver.playerAnimator = Find(root, CharacterIdleFactory.ArmsName).GetComponent<Animator>();
            driver.bossAnimator = Find(root, CharacterIdleFactory.BossName).GetComponent<Animator>();
            driver.bottle = Find(root, "HeroCola_ExistingModel");
            driver.playerClips = Clips(false, false); driver.bossClips = Clips(true, false);
            driver.playerBottleClips = Clips(false, true); driver.bossBottleClips = Clips(true, true);
            driver.idleBlendSeconds = .20f;
            var interaction = Find(root, "ShakeSelection").GetComponent<ShakeSelectionInteraction>();
            interaction.shakeAnimator = driver;
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

        private static void CapturePoses()
        {
            foreach (var boss in new[] { false, true })
                foreach (var time in new[] { .95f, 1.9f })
                {
                    EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                    var driver = UnityEngine.Object.FindFirstObjectByType<ColaShakeAnimator>();
                    var actor = boss ? driver.bossAnimator : driver.playerAnimator;
                    actor.enabled = false;
                    (boss ? driver.bossClips[2] : driver.playerClips[2]).SampleAnimation(actor.gameObject, time);
                    (boss ? driver.bossBottleClips[2] : driver.playerBottleClips[2]).SampleAnimation(driver.bottle.gameObject, time);
                    UnityEngine.Object.FindAnyObjectByType<PlayerSleeveContinuation>()?.RefreshGeometry();
                    var camera = Camera.main; camera.aspect = 16f / 9f;
                    MainSceneTools.Capture(camera, Output + "/" + (boss ? "Boss" : "Player") + (time < 1 ? "-Reach.png" : "-Shake.png"));
                }
            EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
        }

        private static void CaptureSmoothPreviews()
        {
            const int fps = 24;
            foreach (var boss in new[] { false, true })
            {
                EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                var driver = UnityEngine.Object.FindFirstObjectByType<ColaShakeAnimator>();
                var actor = boss ? driver.bossAnimator : driver.playerAnimator;
                var actorClip = boss ? driver.bossClips[2] : driver.playerClips[2];
                var bottleClip = boss ? driver.bossBottleClips[2] : driver.playerBottleClips[2];
                actor.enabled = false;
                var camera = Camera.main; camera.aspect = 16f / 9f;
                var folder = Output + "/" + (boss ? "boss" : "player") + "-smooth-frames";
                Directory.CreateDirectory(folder);
                for (var frame = 0; frame <= Mathf.CeilToInt(ColaShakeAnimationFactory.Duration * fps); frame++)
                {
                    var time = Mathf.Min(ColaShakeAnimationFactory.Duration, frame / (float)fps);
                    actorClip.SampleAnimation(actor.gameObject, time);
                    bottleClip.SampleAnimation(driver.bottle.gameObject, time);
                    UnityEngine.Object.FindAnyObjectByType<PlayerSleeveContinuation>()?.RefreshGeometry();
                    MainSceneTools.Capture(camera, folder + $"/Frame-{frame:D3}.png", 900, 506);
                }
            }
            EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
        }

        private static Transform Find(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        private static void Fail(Exception exception)
        {
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Animation-error.txt", exception.ToString());
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(2);
            else throw new InvalidOperationException("Cola shake animation authoring failed.", exception);
        }
    }
}
