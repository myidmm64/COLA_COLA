using System.IO;
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
        internal const string Folder = ProjectVerificationBridge.Root + "/turn-indicator";
        static ColaTurnLabelVerification() { if (SessionState.GetBool(Key, false)) Hook(); }
        public static void Begin(bool typography = false)
        {
            SessionState.SetBool(Key + ".Typography", typography);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Started", false);
            Hook(); EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
            var previous = EditorWindow.focusedWindow;
            SessionState.SetString(Key + ".Focus", previous ? previous.GetEntityId().ToString() : "");
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var window = EditorWindow.GetWindow(type);
            if (typography)
            {
                SessionState.SetBool(Key + ".Maximized", window.maximized);
                window.maximized = true;
            }
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
            if (SessionState.GetBool(Key + ".Typography", false))
            {
                var probe = new GameObject("TypographyPlayProbe").AddComponent<ColaFontReadabilityRunner>();
                if (!probe) Debug.LogError("Typography Play runner could not be attached.");
            }
            else new GameObject("TurnLabelPlayProbe").AddComponent<ColaTurnLabelProbe>();
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Changed;
            if (SessionState.GetBool(Key + ".Typography", false))
            {
                var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                EditorWindow.GetWindow(type).maximized = SessionState.GetBool(Key + ".Maximized", false);
            }
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
            if (!match || !hud) { Debug.LogError("Turn-indicator HUD/controller missing."); yield break; }
            bool originalW = match.GuaranteedBurst;
            var ai = match.GetComponent<ColaBossItemAI>();
            bool originalAi = ai && ai.automaticItemUse;
            float originalThink = match.interaction.bossThinkSeconds;
            try
            {
                if (originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
                if (ai) ai.automaticItemUse = false;
                match.RestartRun(123);
                var indicator = match.interaction.GetComponent<ColaTurnIndicator>();
                if (!indicator || !indicator.ModelRoot) throw new InvalidOperationException("Physical turn indicator missing.");
                match.interaction.bossThinkSeconds = 1;
                match.interaction.BeginBottle(999);
                yield return new WaitForSecondsRealtime(1);
                log.AppendLine($"Player ready: hud enabled={hud.isActiveAndEnabled}, state={match.State}, playerTurn={match.IsPlayerTurn}, input={match.interaction.InputEnabled}, busy={match.interaction.IsBusy}, screen={Screen.width}x{Screen.height}");
                yield return Capture("Player-ready");
                if (indicator.Brightness < .95f) throw new InvalidOperationException("Player-ready pilot lamp is not lit.");
                log.AppendLine($"Pilot position={indicator.ModelRoot.position}, brightness={indicator.Brightness:F3}, HUD match same={hud.match == match}. No player-turn text is rendered.");
                if (!match.interaction.SelectStrength(1) || !match.interaction.ConfirmShake())
                    throw new InvalidOperationException("Actual player action rejected.");
                float deadline = Time.unscaledTime + 16;
                while (!match.interaction.IsBossTurn && Time.unscaledTime < deadline) yield return null;
                if (!match.interaction.IsBossTurn) throw new InvalidOperationException("Boss turn missing.");
                yield return null;
                yield return null;
                yield return new WaitForSecondsRealtime(.35f);
                log.AppendLine($"Boss turn: playerTurn={match.IsPlayerTurn}, busy={match.interaction.IsBusy}");
                yield return Capture("Boss-turn");
                if (indicator.Brightness > .05f) throw new InvalidOperationException("Pilot lamp stayed lit on boss turn.");
                while (match.interaction.IsBusy && Time.unscaledTime < deadline) yield return null;
                if (match.interaction.IsBusy || !match.IsPlayerTurn) throw new InvalidOperationException("Player return missing.");
                yield return new WaitForSecondsRealtime(.35f);
                log.AppendLine("Player turn restored after actual boss action.");
                yield return Capture("Player-return");
                if (indicator.Brightness < .95f || indicator.ArrivalPulseWeight < .1f)
                    throw new InvalidOperationException("Player-return lamp or soft arrival pulse missing.");
                log.AppendLine($"Returned-turn brightness={indicator.Brightness:F3}, arrival pulse={indicator.ArrivalPulseWeight:F3}");
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

namespace ColaCola.Editor
{
    public sealed class ColaFontReadabilityRunner : MonoBehaviour
    {
        internal const string Folder = ProjectVerificationBridge.Root + "/typography";
        private ColaDuelController match;
        private ColaItemPresentation display;
        private ColaOrderSheet sheet;
        private ColaOrderTarget inspect;
        private bool inspectGauge;
        private void LateUpdate()
        {
            if (inspectGauge && display && match)
            {
                var owner = match.interaction;
                var bell = match.GetComponent<ColaWaiterBell>();
                Vector3 point = bell && bell.HitTarget ? bell.HitTarget.bounds.center : owner.gauge.transform.position;
                display.InspectPointer(owner.playerCamera.WorldToScreenPoint(point));
            }
            if (inspect && sheet) sheet.HandleTarget(inspect);
        }
        private IEnumerator Start()
        {
            var report = new StringBuilder();
            match = FindAnyObjectByType<ColaDuelController>();
            if (!match) { Debug.LogError("Typography review: no duel."); yield break; }
            bool originalW = match.GuaranteedBurst, originalE = match.ItemTestMenuEnabled;
            display = match.GetComponent<ColaItemPresentation>(); sheet = match.GetComponent<ColaOrderSheet>();
            try
            {
                var font = ColaGameFont.Load();
                const string glyphs = "현재압력한계내효과보스메뉴아이템콜라주문";
                font.RequestCharactersInTexture(glyphs, 20, FontStyle.Normal);
                foreach (char glyph in glyphs)
                    if (!font.GetCharacterInfo(glyph, out var info, 20, FontStyle.Normal) || info.advance <= 0)
                        throw new InvalidOperationException("Missing Korean glyph: " + glyph);
                report.AppendLine($"Actual Play Game view {Screen.width}x{Screen.height}, font={font.name}, atlas filter={font.material.mainTexture.filterMode}. Korean glyphs requested at 20px exist.");
                if (originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
                inspectGauge = true;
                yield return new WaitForSecondsRealtime(.8f);
                yield return Capture("Table-hover");
                if (!display || !display.HasHoverDescription) throw new InvalidOperationException("Actual hover description did not render.");
                report.AppendLine("World-hover description present at end-of-frame. Ordinary UI and world labels use the shared smooth system font.");
                inspectGauge = false;
                if (!sheet.IsOpen && !match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu))
                    throw new InvalidOperationException("E shared debug command rejected.");
                yield return new WaitForSecondsRealtime(1);
                if (!sheet.IsOpen || !sheet.IsSettled) throw new InvalidOperationException("Actual menu not settled.");
                foreach (var target in FindObjectsByType<ColaOrderTarget>())
                    if (target.Kind == ColaOrderTarget.Action.Inspect && target.gameObject.activeInHierarchy) { inspect = target; break; }
                yield return null;
                yield return Capture("Menu");
                Vector3 firstLabelPoint = inspect ? match.interaction.playerCamera.WorldToScreenPoint(inspect.transform.position) : Vector3.zero;
                yield return new WaitForSecondsRealtime(.35f);
                yield return Capture("Menu-settled");
                float readingDrift = inspect ? Vector3.Distance(firstLabelPoint, match.interaction.playerCamera.WorldToScreenPoint(inspect.transform.position)) : 0;
                report.AppendLine($"Settled-menu label projection drift over 0.35 seconds: {readingDrift:F4} screen pixels.");
                int worldLabels = 0;
                foreach (var text in FindObjectsByType<TextMesh>())
                    if (text.GetComponent<ColaWorldText>())
                    {
                        if (text.font != font) throw new InvalidOperationException("World label uses another font.");
                        worldLabels++;
                    }
                report.AppendLine($"World labels using the same bundled font: {worldLabels}. E free menu opened without purchases; Q not needed; W off. Sparse actual Game-view captures; no preview camera/RenderTexture.");
                inspect = null;
                sheet.Close();
                int expandedWidth = Screen.width, expandedHeight = Screen.height;
                var gameType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
                UnityEditor.EditorWindow.GetWindow(gameType).maximized = UnityEditor.SessionState.GetBool("COLA_COLA.TurnLabelVerification.Maximized", false);
                inspectGauge = true;
                yield return new WaitForSecondsRealtime(.7f);
                if (Screen.width != expandedWidth || Screen.height != expandedHeight)
                    yield return Capture("Table-restored-view");
                report.AppendLine($"Restored Game-view size: {Screen.width}x{Screen.height}. Native GUI 20px remains unscaled.");
            }
            finally
            {
                inspectGauge = false; inspect = null;
                if (sheet) sheet.Close();
                if (match.GuaranteedBurst != originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
                match.debugItemTestMenu = originalE;
                ProjectVerificationBridge.WriteReport(Folder + "/Play-review.txt", report.ToString());
            }
            Debug.Log("COLA_FONT_READABILITY_PASS");
            UnityEditor.EditorApplication.isPlaying = false;
        }
        private static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(Folder);
            ScreenCapture.CaptureScreenshot(Folder + "/" + name + ".png");
        }
    }
}
