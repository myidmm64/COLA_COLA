using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using ColaCola.Core;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColaCola.Editor
{
    [InitializeOnLoad]
    internal static class ColaReadabilityPlayReview
    {
        private const string Key = "COLA_COLA.ReadabilityReview";
        internal const string Folder = ProjectVerificationBridge.Root + "/fullscreen-ui";
        internal static bool VisualOnly => SessionState.GetBool(Key + ".Visual", false);
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly string[] IntPrefs = { "COLA_COLA.Width", "COLA_COLA.Height", "COLA_COLA.Fullscreen", "COLA_COLA.DisplayDefaultsVersion" };
        private static readonly string[] StringPrefs = { "COLA_COLA.Language", "COLA_COLA.Achievements.v1" };
        static ColaReadabilityPlayReview() { if (SessionState.GetBool(Key, false)) Hook(); }
        internal static void Begin(bool visualOnly = false)
        {
            SessionState.SetBool(Key + ".Visual", visualOnly);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Started", false);
            foreach (string pref in IntPrefs) { SessionState.SetBool(Key + pref, PlayerPrefs.HasKey(pref)); SessionState.SetInt(Key + pref + ".Value", PlayerPrefs.GetInt(pref)); }
            foreach (string pref in StringPrefs) { SessionState.SetBool(Key + pref, PlayerPrefs.HasKey(pref)); SessionState.SetString(Key + pref + ".Value", PlayerPrefs.GetString(pref)); }
            SessionState.SetBool(Key + ".Volume", PlayerPrefs.HasKey("COLA_COLA.MasterVolume"));
            SessionState.SetFloat(Key + ".Volume.Value", PlayerPrefs.GetFloat("COLA_COLA.MasterVolume", .8f));
            SessionState.SetFloat(Key + ".Listener", AudioListener.volume);
            var previous = EditorWindow.focusedWindow;
            SessionState.SetString(Key + ".Focus", previous ? previous.GetEntityId().ToString() : "");
            var game = GameView();
            SessionState.SetInt(Key + ".Size", (int)game.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(game));
            SessionState.SetBool(Key + ".Max", game.maximized);
            game.maximized = true; game.Show(); game.Focus();
            SetSize(1920, 1080);
            Hook(); EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        private static EditorWindow GameView() => EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        internal static void SetSize(int width, int height)
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = type.BaseType.GetProperty("instance", Flags).GetValue(null);
            var groupType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizeGroupType");
            object group = type.GetMethod("GetGroup", Flags).Invoke(singleton, new[] { Enum.Parse(groupType, "Standalone") });
            int count = (int)group.GetType().GetMethod("GetTotalCount", Flags).Invoke(group, null);
            for (int i = 0; i < count; i++)
            {
                var size = group.GetType().GetMethod("GetGameViewSize", Flags).Invoke(group, new object[] { i });
                if ((int)size.GetType().GetProperty("width", Flags).GetValue(size) != width || (int)size.GetType().GetProperty("height", Flags).GetValue(size) != height) continue;
                var game = GameView(); game.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(game, i); game.Repaint(); return;
            }
            throw new InvalidOperationException("Built-in Game-view size unavailable: " + width + "x" + height);
        }
        private static void Hook()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || Time.frameCount < 8 || SessionState.GetBool(Key + ".Started", false)) return;
            SessionState.SetBool(Key + ".Started", true);
            new GameObject("FullscreenUiPlayReview").AddComponent<ColaReadabilityProbe>();
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            foreach (string pref in IntPrefs) if (SessionState.GetBool(Key + pref, false)) PlayerPrefs.SetInt(pref, SessionState.GetInt(Key + pref + ".Value", 0)); else PlayerPrefs.DeleteKey(pref);
            foreach (string pref in StringPrefs) if (SessionState.GetBool(Key + pref, false)) PlayerPrefs.SetString(pref, SessionState.GetString(Key + pref + ".Value", "")); else PlayerPrefs.DeleteKey(pref);
            if (SessionState.GetBool(Key + ".Volume", false)) PlayerPrefs.SetFloat("COLA_COLA.MasterVolume", SessionState.GetFloat(Key + ".Volume.Value", .8f)); else PlayerPrefs.DeleteKey("COLA_COLA.MasterVolume");
            PlayerPrefs.Save(); ColaLocalization.Reload(); AudioListener.volume = SessionState.GetFloat(Key + ".Listener", .8f);
            var game = GameView(); game.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(game, SessionState.GetInt(Key + ".Size", 0)); game.maximized = SessionState.GetBool(Key + ".Max", false);
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>()) if (window.GetEntityId().ToString() == SessionState.GetString(Key + ".Focus", "")) window.Focus();
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Changed;
        }
    }

    [DefaultExecutionOrder(30000)]
    public sealed class ColaReadabilityProbe : MonoBehaviour
    {
        private readonly StringBuilder report = new();
        private ColaDuelController match;
        private ColaFrontEnd front;
        private ColaItemPresentation inventory;
        private ColaOrderSheet sheet;
        private ColaDuelHUD hud;
        private bool saved, originalW, originalE, originalAi;
        private int originalTokens;
        private int reviewSlot = -1;
        private int reviewEntry = (int)ColaAbilityId.FirstOrderService;
        private void Update()
        {
            if (!sheet || !sheet.IsOpen || !sheet.IsSettled) return;
            if (sheet.Page == ColaOrderSheet.SheetPage.Items) sheet.ShowDescription(ColaItemId.SafetyCap);
            else sheet.InspectProgressionEntry(reviewEntry);
        }
        private void LateUpdate()
        {
            if (reviewSlot >= 0 && match && match.Items && match.Items.ItemAt(reviewSlot).HasValue && match.Items.CanInspectItems && sheet && !sheet.IsOpen)
                Inspect(reviewSlot);
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(ColaReadabilityPlayReview.Folder);
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                object next = null; bool more;
                try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
                catch (Exception error) { Restore(); File.WriteAllText(ColaReadabilityPlayReview.Folder + "/Report.md", report + "\nFAIL: " + error); Debug.LogError(error); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (next is IEnumerator nested) stack.Push(nested); else yield return next;
            }
            Restore();
            File.WriteAllText(ColaReadabilityPlayReview.Folder + (ColaReadabilityPlayReview.VisualOnly ? "/Visual.md" : "/Report.md"), report + "\nVisual assessment of the sparse actual Game-view screenshots is recorded separately. No preview cameras, RenderTextures or temporary builds.\n");
            Debug.Log(ColaReadabilityPlayReview.VisualOnly ? "COLA_READABILITY_VISUAL_PASS" : "COLA_READABILITY_PLAY_PASS");
        }
        private IEnumerator Run()
        {
            match = FindAnyObjectByType<ColaDuelController>();
            Require(Application.isPlaying && match && match.interaction && match.Items, "Original project in actual Unity Play");
            originalW = match.GuaranteedBurst; originalE = match.ItemTestMenuEnabled; originalTokens = match.PlayerTokens;
            var ai = match.GetComponent<ColaBossItemAI>(); originalAi = ai.automaticItemUse; saved = true;
            front = ColaFrontEnd.EnsureFor(match); inventory = match.GetComponent<ColaItemPresentation>(); sheet = match.GetComponent<ColaOrderSheet>(); hud = match.GetComponent<ColaDuelHUD>();
            ColaLocalization.SetLanguage("ko"); front.MenuFontCandidate = 1; front.ShowMainMenu();
            yield return new WaitForSecondsRealtime(.6f);
            Require(Screen.width == 1920 && Screen.height == 1080, "Actual Game view is native 1920x1080");
            var boss = match.interaction.shakeAnimator.bossAnimator;
            float idleBefore = boss.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return new WaitForSecondsRealtime(1.2f);
            Require(ColaGameClock.Paused && boss.updateMode == AnimatorUpdateMode.UnscaledTime && boss.GetCurrentAnimatorStateInfo(0).normalizedTime > idleBefore, "Main-menu boss idle advances while the duel clock remains paused");
            yield return Capture("01-main-1080p.png");
            front.ShowSettings(); front.SetMasterVolume(.35f);
            Require(Mathf.Abs(AudioListener.volume - .35f) < .001f, "Settings volume uses real listener gain");
            front.SetResolutionPreset(1920, 1080); front.SetFullscreen(true);
            Require(PlayerPrefs.GetInt("COLA_COLA.Width") == 1920 && PlayerPrefs.GetInt("COLA_COLA.Fullscreen") == 1, "Resolution and fullscreen use shared settings paths");
            yield return Capture("02-settings-1080p.png");
            front.CloseSettings(); front.StartGame();
            yield return new WaitForSecondsRealtime(1);
            Require(!ColaFrontEnd.BlocksGameplay && !ColaGameClock.Paused && boss.updateMode != AnimatorUpdateMode.UnscaledTime, "Start resumes the duel and restores the boss animation clock");
            Require(match.ApplyDebugCommand(ColaDuelController.DebugCommand.SetTokens999) && match.PlayerTokens == 999, "Q sets the actual wallet to 999");
            ai.automaticItemUse = false;
            if (match.GuaranteedBurst) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            match.ApplyDebugCommand(ColaDuelController.DebugCommand.ClearItems);
            Require(match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu) && sheet.IsOpen, "E opens the real free test menu");
            Require(sheet.SetCartQuantity(ColaItemId.IceCup, 1) && sheet.SetCartQuantity(ColaItemId.SafetyCap, 1), "Free menu sliders select two different items");
            sheet.ShowDescription(ColaItemId.SafetyCap);
            yield return Capture("03-items-1080p.png");
            Require(sheet.PrintedTextFits, "FHD menu print fits its actual paper regions");
            Require(Click(sheet.CheckoutCollider), "Actual checkout collider accepts the free batch");
            yield return new WaitForSecondsRealtime(.8f);
            sheet.Close();
            if (match.ItemTestMenuEnabled)
            {
                if (!sheet.IsOpen) Require(sheet.Toggle(), "Reopen free menu before disabling E");
                match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu);
            }
            Require(!match.ItemTestMenuEnabled && match.Items.InventoryCount == 2, "Free menu delivers both items; E off before paid service");
            int slot = FindItem();
            Inspect(slot);
            Require(inventory.HasHoverDescription && hud.ShowsTabHint, "Item description and persistent TAB hint coexist");
            yield return Capture("04-hover-1080p.png");
            if (ColaReadabilityPlayReview.VisualOnly)
            {
                ColaReadabilityPlayReview.SetSize(2560, 1440);
                yield return new WaitForSecondsRealtime(.4f);
                Inspect(slot); yield return Capture("07-hover-1440p.png");
                yield break;
            }
            int tokensBefore = match.PlayerTokens;
            Inspect(slot);
            Require(inventory.TryReturnInspectedItem() && match.PlayerTokens == tokensBefore + ColaItemBalance.ReturnTokens, "Right-click shared path returns immediately and pays exactly once");
            Require(!inventory.TryReturnInspectedItem(), "The same removed item cannot be returned twice");
            var bell = match.GetComponent<ColaWaiterBell>();
            int cost = match.Items.WaiterCostFor(), count = match.Items.WaiterCountFor(), beforeCount = match.Items.InventoryCount;
            tokensBefore = match.PlayerTokens;
            Require(bell.TryRing(), "Actual bell-hand request accepted with E off");
            yield return new WaitForSecondsRealtime(.9f);
            Require(match.PlayerTokens == tokensBefore - cost && match.Items.InventoryCount == beforeCount + count, "Real bell contact charges the real price and delivers the configured count");
            Require(bell.CanRing, "Bell becomes reusable after the shortened hand animation");
            Require(match.interaction.SelectStrength(1) && match.interaction.ConfirmShake(), "Normal probability player shake accepted with W off");
            yield return new WaitForSecondsRealtime(1.4f);
            Require(match.interaction.IsPlayerShaking && !hud.ShowsTabHint && !match.Items.CanInspectItems, "Only the player shake hides descriptions and TAB");
            yield return Capture("05-player-shake-1080p.png");
            float deadline = Time.unscaledTime + 40;
            bool observedBoss = false;
            while (match.interaction.IsBusy && Time.unscaledTime < deadline)
            {
                if (match.interaction.IsShaking && match.interaction.LastActionWasBoss)
                {
                    Require(hud.ShowsTabHint && match.Items.CanInspectItems, "Boss shaking leaves TAB and item inspection available");
                    Inspect(FindItem());
                    Require(inventory.HasHoverDescription, "Boss-shake item description is visible through the real inspection path");
                    int before = match.PlayerTokens;
                    Require(inventory.TryReturnInspectedItem() && match.PlayerTokens == before + ColaItemBalance.ReturnTokens, "Player can return a stored item during the boss shake");
                    observedBoss = true; break;
                }
                yield return null;
            }
            yield return Ready(45);
            report.AppendLine("Normal sequence observed boss shake: " + observedBoss + ".");
            foreach (string language in ColaLocalization.Languages)
            {
                ColaLocalization.SetLanguage(language);
                Require(match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleAbilityWindow), "Open ability test page: " + language);
                yield return new WaitForSecondsRealtime(.2f);
                sheet.InspectProgressionEntry((int)ColaAbilityId.FirstOrderService);
                yield return new WaitForEndOfFrame(); yield return null;
                Require(sheet.PrintedTextFits, "Long ability text fits: " + language);
                sheet.Close();
            }
            ColaLocalization.SetLanguage("ko");
            match.Progression.DebugGrantAbility(ColaAbilityId.FirstOrderService);
            Require(sheet.ToggleRecords(), "TAB shared path opens the physical records");
            yield return new WaitForSecondsRealtime(.5f);
            sheet.InspectProgressionEntry((int)ColaAbilityId.FirstOrderService);
            yield return Capture("06-records-1080p.png");
            Require(sheet.PrintedTextFits, "Record text and tabs fit at 1080p");
            sheet.Close();
            ColaReadabilityPlayReview.SetSize(2560, 1440);
            yield return new WaitForSecondsRealtime(.6f);
            Require(Screen.width == 2560 && Screen.height == 1440 && Mathf.Approximately(ColaUiLayout.Scale, 2), "Native 1440p uses proportional UI scale");
            Inspect(FindItem());
            yield return Capture("07-hover-1440p.png");
            ColaReadabilityPlayReview.SetSize(1366, 768);
            yield return new WaitForSecondsRealtime(.4f);
            Require(sheet.ToggleRecords(), "Records open at the small-screen preset");
            yield return new WaitForSecondsRealtime(.5f);
            sheet.InspectProgressionEntry((int)ColaAbilityId.FirstOrderService);
            yield return new WaitForEndOfFrame(); yield return null;
            Require(sheet.PrintedTextFits, "Small-screen records fit without overflowing the paper");
            sheet.Close(); ColaReadabilityPlayReview.SetSize(1920, 1080);
            match.RestartRun(1729);
            ai.automaticItemUse = false;
            Require(match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst) && match.GuaranteedBurst, "W on only for forced-burst and ability checks");
            for (int i = 0; i < 3; i++)
            {
                Require(match.interaction.SelectStrength(1) && match.interaction.ConfirmShake(), "Guaranteed-burst action " + (i + 1));
                float until = Time.unscaledTime + 25;
                while (!match.Progression.ChoiceReady && (match.interaction.IsBusy || !match.IsPlayerTurn) && Time.unscaledTime < until) yield return null;
                Require(Time.unscaledTime < until, "Burst/blackout/fade returned control " + (i + 1));
            }
            yield return new WaitForSecondsRealtime(.7f);
            Require(match.Progression.ChoiceReady && sheet.Page == ColaOrderSheet.SheetPage.AbilityChoice && sheet.IsOpen, "Three real bursts open the mandatory ability choice");
            reviewEntry = (int)match.Progression.Choices[0];
            yield return Capture("08-ability-choice-1080p.png");
            Require(sheet.PrintedTextFits && Click(sheet.PurchaseCollider(ColaItemCatalog.All[0].Id)) && !match.Progression.HasPendingChoice, "Actual choice collider grants the ability and releases the pending reward");
            yield return Ready(15);
            if (match.GuaranteedBurst) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            for (int stage = 1; stage <= 5; stage++)
            {
                Require(match.ApplyDebugCommand((ColaDuelController.DebugCommand)((int)ColaDuelController.DebugCommand.Stage1 + stage - 1)), "Stage shared debug path " + stage);
                Require(match.Level == stage && match.BossMaxHealth == ColaDuelController.BossHealthForStage(stage), "Correct boss health for stage " + stage);
            }
            Require(match.ApplyDebugCommand(ColaDuelController.DebugCommand.WinCurrentStage) && match.State == ColaDuelController.DuelState.Completed, "Final stage reaches challenge complete");
            yield return Capture("09-complete-1080p.png");
            match.RestartRun(1729, true);
            yield return new WaitForSecondsRealtime(1);
            Require(match.State == ColaDuelController.DuelState.Playing && match.Level == 1 && !match.interaction.IsBusy, "New run resets after completion");
            front.ShowSettings(); front.CloseSettings();
            Require(!ColaFrontEnd.BlocksGameplay && !ColaGameClock.Paused, "ESC settings shared path resumes the active game");
            front.ShowMainMenu();
            Require(ColaFrontEnd.BlocksGameplay && ColaGameClock.Paused, "Return to main menu pauses gameplay");
            int enabledScenes = 0;
            foreach (var scene in EditorBuildSettings.scenes) if (scene.enabled) { enabledScenes++; Require(File.Exists(scene.path), "Build scene exists: " + scene.path); }
            Require(enabledScenes == 1 && EditorBuildSettings.scenes[0].path == MainSceneTools.ScenePath, "Build starts in the intended main scene only");
            report.AppendLine("Build target: " + EditorUserBuildSettings.activeBuildTarget + ". No standalone build was generated.");
        }
        private bool Click(Collider target)
        {
            if (!target) return false;
            Vector3 point = match.interaction.playerCamera.WorldToScreenPoint(target.bounds.center);
            return match.interaction.HandlePointer(new Vector2(point.x, point.y));
        }
        private int FindItem()
        { for (int i = 0; i < match.Items.CapacityFor(); i++) if (match.Items.ItemAt(i).HasValue) return i; throw new InvalidOperationException("Inspection item missing"); }
        private void Inspect(int slot)
        {
            reviewSlot = slot;
            var root = inventory.StorageRoot();
            foreach (var cube in root.GetComponentsInChildren<ColaItemCube>(true)) if (cube.Slot == slot)
            {
                var collider = cube.GetComponent<Collider>();
                Vector3 point = match.interaction.playerCamera.WorldToScreenPoint(collider.bounds.center);
                inventory.InspectPointer(new Vector2(point.x, point.y)); return;
            }
            throw new InvalidOperationException("Inspection collider missing");
        }
        private IEnumerator Ready(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while ((match.interaction.IsBusy || !match.IsPlayerTurn) && Time.unscaledTime < until) yield return null;
            Require(!match.interaction.IsBusy && match.IsPlayerTurn, "Duel returned to player control");
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.2f); yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(ColaReadabilityPlayReview.Folder + "/" + name);
            yield return new WaitForSecondsRealtime(.2f);
            report.AppendLine("GAME VIEW: " + name + " / " + Screen.width + "x" + Screen.height);
        }
        private void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            report.AppendLine("PASS: " + message);
            File.WriteAllText(ColaReadabilityPlayReview.Folder + "/Progress.txt", message);
        }
        private void Restore()
        {
            if (!saved || !match) return;
            sheet?.Close();
            if (match.GuaranteedBurst != originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            if (match.ItemTestMenuEnabled != originalE)
            {
                if (!originalE && sheet && !sheet.IsOpen) sheet.Toggle();
                match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu); sheet?.Close();
                match.debugItemTestMenu = originalE;
            }
            typeof(ColaDuelController).GetProperty("PlayerTokens").SetValue(match, originalTokens);
            match.GetComponent<ColaBossItemAI>().automaticItemUse = originalAi;
            report.AppendLine("Restored initial Q wallet, W/E flags and boss AI. Editor hook restores language, settings, achievements and Game-view layout.");
            saved = false;
        }
        private void OnDestroy() => Restore();
    }
}
