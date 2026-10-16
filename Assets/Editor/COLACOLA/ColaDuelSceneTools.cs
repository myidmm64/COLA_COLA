using System;
using System.Linq;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColaCola.Editor
{
    public static class ColaDuelSceneTools
    {
        [MenuItem("COLA_COLA/Set Up Playable Duel")]
        public static void Apply()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Set up the saved duel from Edit mode.");
                var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene");
                ApplyToScene(root);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("MainScene save failed.");
                AssetDatabase.SaveAssets();
                Debug.Log("COLA_PLAYABLE_DUEL_SAVED");
                PlayableDuelVerification.Begin();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(2);
            }
        }

        public static void ApplyToScene(GameObject root)
        {
            var node = root.transform.Find("08_Devices/DuelRules");
            if (!node)
            {
                node = new GameObject("DuelRules").transform;
                node.SetParent(root.transform.Find("08_Devices"), false);
            }
            var match = node.GetComponent<ColaDuelController>();
            if (!match) match = node.gameObject.AddComponent<ColaDuelController>();
            match.interaction = root.GetComponentInChildren<ShakeSelectionInteraction>(true);
            match.terminal = root.GetComponentInChildren<StatusTerminalDisplay>(true);
            match.itemRoot = root.transform.Find("04_TabletopItems").gameObject;
            match.itemLight = root.transform.Find("06_Lighting/TestItems_ReadabilityFill").GetComponent<Light>();
            match.playerMaxHealth = 3;
            match.bossBaseHealth = 3;
            match.itemRoot.SetActive(false);
            match.itemLight.enabled = false;
            match.interaction.automaticBossTurns = true;
            match.interaction.shakeAnimator.previewPlayerOnPlay = false;
            var breathing = ColaCameraBreathing.EnsureFor(root.transform);
            if (breathing) EditorUtility.SetDirty(breathing);
            match.terminal.SetPlayerState(3, 3, 0, false);
            match.terminal.SetBossState(3, 3, 0, false);
            match.terminal.SetActiveParticipant(true);
            var hud = node.GetComponent<ColaDuelHUD>();
            if (!hud) hud = node.gameObject.AddComponent<ColaDuelHUD>();
            hud.match = match;
            EditorUtility.SetDirty(match);
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(match.itemLight);
            EditorUtility.SetDirty(match.interaction);
            EditorUtility.SetDirty(match.interaction.shakeAnimator);
            EditorUtility.SetDirty(match.terminal);
        }
    }
}
