using System;
using System.IO;
using System.Linq;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColaCola.Editor
{
    public static class PresentationMotionSceneTools
    {
        [MenuItem("COLA_COLA/Apply Livelier Idle and Animated Forecast")]
        public static void Apply()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene");
                CharacterIdleFactory.RebuildBossOnly(root);
                var breathing = ColaCameraBreathing.EnsureFor(root.transform);
                if (!breathing) throw new InvalidOperationException("Cinemachine camera is missing.");
                breathing.enabled = false;
                var gauge = root.GetComponentInChildren<PressureGaugeDisplay>();
                gauge.forecastTransitionSeconds = .38f;
                EditorUtility.SetDirty(gauge);
                EditorUtility.SetDirty(breathing);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("MainScene save failed.");
                PresentationMotionVerification.Begin();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(2);
            }
        }
    }
}
