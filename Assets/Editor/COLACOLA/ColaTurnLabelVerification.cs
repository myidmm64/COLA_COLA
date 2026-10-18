using System;
using System.Collections;
using System.Text;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColaCola.Editor
{
    [InitializeOnLoad]
    public static class ColaTurnLabelVerification
    {
        private const string Key = "COLA_COLA.TurnLabelVerification";
        internal const string Folder = ProjectVerificationBridge.Root + "/turn-label";
        static ColaTurnLabelVerification() { if (SessionState.GetBool(Key, false)) Hook(); }
        public static void Begin()
        {
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Started", false);
            Hook(); EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
            var previous = EditorWindow.focusedWindow;
            SessionState.SetString(Key + ".Focus", previous ? previous.GetEntityId().ToString() : "");
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var window = EditorWindow.GetWindow(type);
            window.Show(); window.Focus();
            EditorApplication.isPlaying = true;
        }
        private static void Hook()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || Time.frameCount < 5 ||
                SessionState.GetBool(Key + ".Started", false)) return;
            SessionState.SetBool(Key + ".Started", true);
            new GameObject("TurnLabelPlayProbe").AddComponent<ColaTurnLabelProbe>();
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Changed;
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (window.GetEntityId().ToString() == SessionState.GetString(Key + ".Focus", "")) window.Focus();
        }
    }

    public sealed class ColaTurnLabelProbe : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var match = FindAnyObjectByType<ColaDuelController>();
            var hud = FindAnyObjectByType<ColaDuelHUD>();
            var log = new StringBuilder();
            if (!match || !hud) { Debug.LogError("Turn-label HUD/controller missing."); yield break; }
            bool originalW = match.GuaranteedBurst;
            var ai = match.GetComponent<ColaBossItemAI>();
            bool originalAi = ai && ai.automaticItemUse;
            float originalThink = match.interaction.bossThinkSeconds;
            try
            {
                if (originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
                if (ai) ai.automaticItemUse = false;
                match.RestartRun(123);
                match.interaction.bossThinkSeconds = 1;
                match.interaction.BeginBottle(999);
                yield return new WaitForSecondsRealtime(1);
                log.AppendLine($"Player ready: hud enabled={hud.isActiveAndEnabled}, state={match.State}, playerTurn={match.IsPlayerTurn}, input={match.interaction.InputEnabled}, busy={match.interaction.IsBusy}, screen={Screen.width}x{Screen.height}");
                yield return Capture("Player-ready");
                if (Time.frameCount - hud.TurnLabelRepaintFrame > 2) throw new InvalidOperationException("Player-ready label was not repainted.");
                log.AppendLine($"Label repaint frame={hud.TurnLabelRepaintFrame}, rect={hud.TurnLabelBounds}, HUD match same={hud.match == match}");
                if (!match.interaction.SelectStrength(1) || !match.interaction.ConfirmShake())
                    throw new InvalidOperationException("Actual player action rejected.");
                float deadline = Time.unscaledTime + 16;
                while (!match.interaction.IsBossTurn && Time.unscaledTime < deadline) yield return null;
                if (!match.interaction.IsBossTurn) throw new InvalidOperationException("Boss turn missing.");
                yield return null;
                yield return null;
                log.AppendLine($"Boss turn: playerTurn={match.IsPlayerTurn}, busy={match.interaction.IsBusy}");
                yield return Capture("Boss-turn");
                if (Time.frameCount - hud.TurnLabelRepaintFrame < 2) throw new InvalidOperationException("Label still drawn on boss turn.");
                while (match.interaction.IsBusy && Time.unscaledTime < deadline) yield return null;
                if (match.interaction.IsBusy || !match.IsPlayerTurn) throw new InvalidOperationException("Player return missing.");
                log.AppendLine("Player turn restored after actual boss action.");
                yield return Capture("Player-return");
                if (Time.frameCount - hud.TurnLabelRepaintFrame > 2) throw new InvalidOperationException("Player-return label was not repainted.");
            }
            finally
            {
                if (match.GuaranteedBurst != originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
                if (ai) ai.automaticItemUse = originalAi;
                match.interaction.bossThinkSeconds = originalThink;
                ProjectVerificationBridge.WriteReport(ColaTurnLabelVerification.Folder + "/Play-review.txt", log.ToString());
            }
            Debug.Log("COLA_TURN_LABEL_PASS");
            UnityEditor.EditorApplication.isPlaying = false;
        }
        private static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            System.IO.Directory.CreateDirectory(ColaTurnLabelVerification.Folder);
            ScreenCapture.CaptureScreenshot(ColaTurnLabelVerification.Folder + "/" + name + ".png");
        }
    }
}
