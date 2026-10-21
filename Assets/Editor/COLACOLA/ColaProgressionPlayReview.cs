using System;
using System.Collections;
using System.IO;
using System.Text;
using ColaCola.Core;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColaCola.Editor
{
    /// <summary>Live Game-view review controlled through the project's verification mailbox, never OS input.</summary>
    [InitializeOnLoad]
    public static class ColaProgressionPlayReview
    {
        private const string Key = "COLA_COLA.ProgressionReview";
        internal const string Folder = ProjectVerificationBridge.Root + "/progression";
        internal const string Control = "Library/COLA_COLA/progression-review-control.json";
        static ColaProgressionPlayReview() { if (SessionState.GetBool(Key, false)) Hook(); }
        public static void Begin()
        {
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Started", false);
            var previous = EditorWindow.focusedWindow;
            SessionState.SetString(Key + ".Previous", previous ? previous.GetEntityId().ToString() : "");
            EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var game = EditorWindow.GetWindow(type);
            SessionState.SetString(Key + ".Game", game.GetEntityId().ToString());
            SessionState.SetBool(Key + ".Max", game.maximized);
            game.Show(); game.maximized = true; game.Focus(); Hook();
            EditorApplication.isPlaying = true;
        }
        private static void Hook()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || SessionState.GetBool(Key + ".Started", false) || !EditorApplication.isPlaying || Time.frameCount < 8) return;
            SessionState.SetBool(Key + ".Started", true);
            new GameObject("ProgressionLiveReview").AddComponent<ColaProgressionReviewProbe>().Begin();
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (window.GetEntityId().ToString() == SessionState.GetString(Key + ".Game", "")) window.maximized = SessionState.GetBool(Key + ".Max", false);
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (window.GetEntityId().ToString() == SessionState.GetString(Key + ".Previous", "")) window.Focus();
            SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Started", false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Changed;
            if (File.Exists(Control)) File.Delete(Control);
        }
    }

    public sealed class ColaProgressionReviewProbe : MonoBehaviour
    {
        [Serializable] private sealed class Command { public string action; public int stage; public int entry; public string note; }
        [Serializable] private sealed class Live { public string phase, page, boss, note, fit; public int stage, bossHealth, playerHealth, outfitParts; public bool play; }
        private readonly StringBuilder report = new();
        private ColaDuelController match;
        private ColaOrderSheet sheet;
        private string savedAchievements;
        private bool hadSave, saved, restored, originalW, originalE;
        private double nextPoll;
        public void Begin() => StartCoroutine(GuardedStart());
        private IEnumerator GuardedStart()
        {
            yield return null;
            try
            {
                match = FindAnyObjectByType<ColaDuelController>();
                Require(Application.isPlaying && match && match.Progression != null, "Actual live duel and progression available");
                sheet = match.GetComponent<ColaOrderSheet>();
                hadSave = PlayerPrefs.HasKey("COLA_COLA.Achievements.v1");
                savedAchievements = PlayerPrefs.GetString("COLA_COLA.Achievements.v1", "");
                originalW = match.GuaranteedBurst; originalE = match.ItemTestMenuEnabled; saved = true;
                Require(match.ApplyDebugCommand(ColaDuelController.DebugCommand.SetTokens999) && match.PlayerTokens == 999, "Q shared debug path sets the real wallet");
                Require(match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu), "E shared debug path opens free test menu");
                Require(sheet.IsOpen && sheet.SetCartQuantity(ColaItemId.IceCup, 2), "Free test menu quantity path");
                sheet.Close();
                Require(match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleAbilityWindow), "Y shared path opens physical ability debug page");
                Require(sheet.Page == ColaOrderSheet.SheetPage.DebugAbilities && sheet.IsOpen && sheet.ModelRoot.gameObject.activeInHierarchy, "Y page uses actual clipboard model");
                Require(sheet.SelectProgressionEntry((int)ColaAbilityId.ExtraStorage), "Ability debug entry selected");
                Require(match.Progression.Has(ColaAbilityId.ExtraStorage) && match.Items.CapacityFor() == 10, "Ability granted and tray expanded");
                sheet.Close();
                Require(match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleAchievementWindow), "T shared path opens physical achievement debug page");
                bool before = match.Progression.IsUnlocked(ColaAchievementId.FirstOrder);
                Require(sheet.SelectProgressionEntry((int)ColaAchievementId.FirstOrder), "Achievement entry toggled");
                Require(match.Progression.IsUnlocked(ColaAchievementId.FirstOrder) != before, "Achievement debug changes actual state");
                match.Progression.DebugSetAchievement(ColaAchievementId.FirstOrder, before);
                sheet.Close();
                Require(sheet.ToggleRecords() && match.Progression.JournalOpen && sheet.Page == ColaOrderSheet.SheetPage.Abilities, "TAB shared record toggle opens owned abilities");
                Require(sheet.ToggleRecords() && !match.Progression.JournalOpen && !sheet.BlocksGameplay, "TAB closes record and releases input");
                match.RestartRun(1729);
                report.AppendLine("Saved achievement PlayerPrefs and W/E flags before checks; restore on review exit. No screenshot files or preview cameras.");
                WriteState("ready", "Stage 1 ready for live Game-view inspection");
            }
            catch (Exception error) { Fail(error); }
        }
        private void Update()
        {
            if (!saved || restored || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .25;
            if (!File.Exists(ColaProgressionPlayReview.Control)) return;
            try
            {
                var command = JsonUtility.FromJson<Command>(File.ReadAllText(ColaProgressionPlayReview.Control));
                File.Delete(ColaProgressionPlayReview.Control);
                if (command.action == "stage")
                {
                    sheet.Close();
                    Require(match.ApplyDebugCommand((ColaDuelController.DebugCommand)((int)ColaDuelController.DebugCommand.Stage1 + command.stage - 1)), "Stage debug path " + command.stage);
                    Require(match.Level == command.stage && match.BossMaxHealth == ColaDuelController.BossHealthForStage(command.stage), "Stage health " + match.BossMaxHealth);
                    StartCoroutine(Settled("ready", command.note));
                }
                else if (command.action == "choice")
                    StartCoroutine(ForceChoice(command.note));
                else if (command.action == "choose")
                {
                    var id = match.Progression.Choices[0];
                    var target = sheet.PurchaseCollider(ColaItemCatalog.All[0].Id);
                    Vector3 point = match.interaction.playerCamera.WorldToScreenPoint(target.bounds.center);
                    Require(sheet.HandlePointer(new Vector2(point.x, point.y)) && match.Progression.Has(id) && !match.Progression.HasPendingChoice, "Actual clipboard collider raycast selects and releases pending reward");
                    WriteState("ready", command.note);
                }
                else if (command.action == "debug-achievements" || command.action == "debug-abilities")
                {
                    sheet.Close();
                    Require(match.ApplyDebugCommand(command.action == "debug-achievements" ? ColaDuelController.DebugCommand.ToggleAchievementWindow : ColaDuelController.DebugCommand.ToggleAbilityWindow), "Debug clipboard window command");
                    sheet.InspectProgressionEntry(command.entry);
                    StartCoroutine(Settled("debug", command.note));
                }
                else if (command.action == "records")
                { sheet.Close(); Require(sheet.ShowProgression(ColaOrderSheet.SheetPage.Achievements), "Record achievement page"); sheet.InspectProgressionEntry(0); StartCoroutine(Settled("records", command.note)); }
                else if (command.action == "note")
                { report.AppendLine("LIVE GAME VIEW: " + command.note); WriteState("ready", command.note); }
                else if (command.action == "finish")
                {
                    Restore();
                    ProjectVerificationBridge.WriteReport(ColaProgressionPlayReview.Folder + "/Play-review.txt", report.ToString());
                    Debug.Log("COLA_PROGRESSION_REVIEW_PASS");
                }
            }
            catch (Exception error) { Fail(error); }
        }
        private IEnumerator Settled(string phase, string note)
        { yield return new WaitForSecondsRealtime(.7f); WriteState(phase, note); }
        private IEnumerator ForceChoice(string note)
        {
            match.RestartRun(1729);
            match.interaction.automaticBossTurns = false;
            match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            for (int i = 0; i < 3; i++)
            {
                bool committed = match.interaction.SelectStrength(1) && match.interaction.ConfirmShake();
                if (!committed) { Fail(new InvalidOperationException("Actual W burst commit " + i)); yield break; }
                float deadline = Time.realtimeSinceStartup + 30;
                while (Time.realtimeSinceStartup < deadline && (i == 2 ? !match.Progression.ChoiceReady : match.interaction.IsBusy)) yield return null;
                if (Time.realtimeSinceStartup >= deadline) { Fail(new TimeoutException("Waiting for actual burst/choice")); yield break; }
            }
            yield return null;
            if (sheet.Page != ColaOrderSheet.SheetPage.AbilityChoice || !sheet.IsOpen)
            { Fail(new InvalidOperationException("Automatic reward did not open physical menu")); yield break; }
            report.AppendLine("PASS: Three actual guaranteed W burst shakes naturally opened physical ability selection after fade-in.");
            WriteState("choice", note);
        }
        private void Require(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); report.AppendLine("PASS: " + label); }
        private void WriteState(string phase, string note)
        {
            var appearance = match.GetComponent<ColaBossAppearance>();
            var live = new Live {phase = phase, stage = match.Level, boss = match.BossName, bossHealth = match.BossMaxHealth,
                playerHealth = match.PlayerHealth, page = sheet.Page.ToString(), play = Application.isPlaying,
                outfitParts = appearance ? appearance.PartCount : 0, note = note};
            var fits = new StringBuilder();
            foreach (var renderer in match.interaction.shakeAnimator.bossAnimator.GetComponentsInChildren<Renderer>())
                fits.AppendLine(renderer.name + " centre=" + renderer.bounds.center.ToString("F3") + " size=" + renderer.bounds.size.ToString("F3"));
            live.fit = fits.ToString();
            ProjectVerificationBridge.WriteReport(ColaProgressionPlayReview.Folder + "/live-state.json", JsonUtility.ToJson(live, true));
        }
        private void Fail(Exception error)
        {
            Restore();
            ProjectVerificationBridge.WriteReport(ColaProgressionPlayReview.Folder + "/Play-review.txt", report + "\nFAIL: " + error);
            Debug.LogException(error);
        }
        private void Restore()
        {
            if (!saved || restored) return;
            restored = true;
            if (hadSave) PlayerPrefs.SetString("COLA_COLA.Achievements.v1", savedAchievements); else PlayerPrefs.DeleteKey("COLA_COLA.Achievements.v1");
            PlayerPrefs.Save();
            if (match)
            {
                sheet.Close(); match.RestartRun();
                if (match.GuaranteedBurst != originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
                if (match.ItemTestMenuEnabled != originalE) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu);
            }
            if (File.Exists(ColaProgressionPlayReview.Control)) File.Delete(ColaProgressionPlayReview.Control);
        }
        private void OnDestroy() => Restore();
    }
}
