using System;
using System.Linq;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColaCola.Editor
{
    /// <summary>Bind original rigs. Authoring never builds old shake clips or starts verification.</summary>
    [InitializeOnLoad]
    public static class ColaShakeAnimationSceneTools
    {
        static ColaShakeAnimationSceneTools()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }
        private static void Changed(PlayModeStateChange state)
        {
            var driver = UnityEngine.Object.FindAnyObjectByType<ColaShakeAnimator>();
            if (!driver) return;
            if (state == PlayModeStateChange.EnteredPlayMode && driver.previewPlayerOnPlay) driver.BeginPlayerPlayPreview();
            if (state == PlayModeStateChange.ExitingPlayMode) driver.StopPlayerPlayPreview();
        }
        [MenuItem("COLA_COLA/Bind Original Cola Shake Rigs")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Bind rigs in Edit mode.");
            var scene = SceneManager.GetActiveScene();
            var root = scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene");
            ApplyToScene(root);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        public static void ApplyPlayerRightSideGrip() => Apply();
        public static void ApplyToScene(GameObject root)
        {
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
            driver.previewPlayerOnPlay = false;
            var interaction = Find(root, "ShakeSelection").GetComponent<ShakeSelectionInteraction>();
            interaction.shakeAnimator = driver;
            EditorUtility.SetDirty(driver); EditorUtility.SetDirty(interaction);
        }
        [MenuItem("COLA_COLA/Enable Player Hand Preview On Play")]
        public static void EnablePlayerPreviewOnPlay()
        {
            if (EditorApplication.isPlaying) return;
            var driver = UnityEngine.Object.FindAnyObjectByType<ColaShakeAnimator>();
            if (!driver) return;
            driver.previewPlayerOnPlay = true;
            EditorUtility.SetDirty(driver);
            EditorSceneManager.MarkSceneDirty(driver.gameObject.scene);
        }
        [MenuItem("COLA_COLA/Preview Player Cola Shake (Play Mode)")]
        public static void PreviewPlayer() => Preview(false);
        [MenuItem("COLA_COLA/Preview Boss Cola Shake (Play Mode)")]
        public static void PreviewBoss() => Preview(true);
        private static void Preview(bool boss)
        {
            if (!EditorApplication.isPlaying) { Debug.Log("Enter Play mode to preview the original rig."); return; }
            var driver = UnityEngine.Object.FindAnyObjectByType<ColaShakeAnimator>();
            if (!driver) return;
            if (boss) driver.PreviewBossShake(); else driver.PreviewPlayerShake();
        }
        private static Transform Find(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
    }
}
