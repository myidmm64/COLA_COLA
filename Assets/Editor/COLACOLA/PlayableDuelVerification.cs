using System;
using System.Globalization;
using System.IO;
using System.Text;
using ColaCola.Core;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColaCola.Editor
{
    /// <summary>Exercises the saved duel through real Play-mode input and live rig state, without rendering previews.</summary>
    [InitializeOnLoad]
    public static class PlayableDuelVerification
    {
        private const string Key = "COLA_COLA.PlayableDuelVerification";
        private const string Output = "output/verification/latest/playable-duel/Playmode-validation.txt";
        private static ColaDuelController match;
        private static ShakeSelectionInteraction interaction;
        private static ColaShakeAnimator driver;
        private static StatusTerminalDisplay terminal;
        private static StringBuilder report;
        private static int phase, phaseFrame, resolved, playerRewards, bossRewards, playerBursts, bossBursts;
        private static int committedBeforeCancel, resolvedBeforeCancel, savedPlayerTokens, savedBossTokens;
        private static int savedPlayerHealth, savedBossHealth, savedLevel;
        private static float phaseStarted, playerMotion, bossMotion;
        private static int playerGripSamples, bossGripSamples;
        private static bool victoryBusyGateObserved;
        private static Vector3 playerHandRest, bossHandRest, bottleRest, bottleScale, capRest;
        private static readonly Vector3[] strengthButtonRest = new Vector3[3];
        private static ColaDuelController firstPlayMatch;
        private static Transform bottleParent;

        static PlayableDuelVerification()
        {
            if (SessionState.GetBool(Key, false)) Hook();
        }

        public static void Begin()
        {
            try
            {
                Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Start duel verification from the saved edit-time scene.");
                EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                Directory.CreateDirectory(Path.GetDirectoryName(Output));
                SessionState.SetBool(Key, true);
                SessionState.SetBool(Key + ".Initialized", false);
                SessionState.SetBool(Key + ".Completed", false);
                SessionState.SetBool(Key + ".BetweenPlayEntries", false);
                SessionState.SetString(Key + ".Error", "");
                SessionState.SetInt(Key + ".Stage", 0);
                SessionState.SetString(Key + ".Started", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                Hook();
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void Hook()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= PlayChanged;
            EditorApplication.playModeStateChanged += PlayChanged;
        }

        private static void InitializePlay()
        {
            Check(Application.isPlaying, "Duel verification must run in Play mode.");
            match = UnityEngine.Object.FindFirstObjectByType<ColaDuelController>();
            Check(match && match.isActiveAndEnabled, "The saved scene has no active duel controller.");
            // Base duel fixtures assert exact action/reward counts; boss-item policy has its own live Play suite.
            match.GetComponent<ColaBossItemAI>().automaticItemUse = false;
            interaction = match.interaction;
            terminal = match.terminal;
            driver = interaction ? interaction.shakeAnimator : null;
            Check(interaction && terminal && driver, "Duel, terminal and original-hand bindings are required.");
            Check(!driver.previewPlayerOnPlay && !driver.IsPlaying, "The automatic hand preview must be disabled for actual gameplay.");
            Check(driver.CanPlay(false, 1) && driver.CanPlay(true, 1), "Both original character hand rigs must be bound.");
            Check(terminal.TryValidateBindings(out var terminalMessage), terminalMessage);
            var root = SceneManager.GetActiveScene().GetRootGameObjects();
            Transform itemGroup = null;
            foreach (var sceneRoot in root)
                foreach (var node in sceneRoot.GetComponentsInChildren<Transform>(true))
                    if (node.name == "04_TabletopItems") itemGroup = node;
            Check(itemGroup && !itemGroup.gameObject.activeInHierarchy, "The playable version must hide the tabletop test items.");
            CheckBootState();
            bottleRest = interaction.bottle.localPosition;
            bottleScale = interaction.bottle.localScale;
            bottleParent = interaction.bottle.parent;
            capRest = interaction.cap ? interaction.cap.localPosition : Vector3.zero;
            playerHandRest = driver.playerHand.position;
            bossHandRest = driver.bossHand.position;
            interaction.ShakeResolved -= OnResolved;
            interaction.ShakeResolved += OnResolved;
            ResetObservations();
            if (SessionState.GetInt(Key + ".Stage", 0) == 1)
            {
                Check(match == firstPlayMatch, "The second Play entry must reuse the existing scene instead of reopening or reloading it.");
                CheckStrengthButtonsAtRest();
                report = new StringBuilder(File.ReadAllText(Output));
                phase = 20;
                phaseStarted = Time.unscaledTime;
            }
            else
            {
                firstPlayMatch = match;
                for (int i = 0; i < strengthButtonRest.Length; i++)
                    strengthButtonRest[i] = interaction.strengthButtons[i].localPosition;
                report = new StringBuilder("Unity Play-mode playable duel verification\n");
                report.AppendLine("PASS: saved MainScene boots level 1, full 3/3 health, zero tokens, player input enabled, original hand bindings, item props hidden and animation preview disabled.");
                SaveMatchSnapshot();
                Check(interaction.SelectStrength(3) && interaction.Model.SelectedStrength == 3,
                    "The idle selection cancellation probe did not select strength 3.");
                interaction.enabled = false;
                CheckStrengthButtonsAtRest();
                Check(interaction.Model.SelectedStrength == 0 && interaction.CommittedActionCount == 0 && resolved == 0,
                    "Disabling idle interaction must clear selection without committing or resolving an action.");
                interaction.enabled = true;
                CheckStrengthButtonsAtRest();
                CheckMatchSnapshot();
                Check(interaction.Model.SelectedStrength == 0 && interaction.CommittedActionCount == 0 && resolved == 0,
                    "Re-enabling idle interaction retained a selected key, committed action or stale reward callback.");
                report.AppendLine("PASS: idle strength-3 selection followed by interaction disable/re-enable restores all three key positions, clears selection and produces no action, health change or token reward.");
                Restart(123);
                interaction.BeginBottle(999);
                Check(!match.ContinueToNextBoss(), "A live encounter allowed a level skip.");
                Check(Click(3), "The actual strength/cola collider raycasts did not begin a player action.");
                Check(!interaction.LastResult.Burst, "The seeded normal-turn probe must begin with a safe shake.");
                Check(interaction.IsBusy && !interaction.SelectStrength(1) && !interaction.ConfirmShake(), "A playing hand animation must lock competing input.");
                phase = 0;
            }
            SessionState.SetBool(Key + ".Initialized", true);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            try
            {
                if (SessionState.GetBool(Key + ".BetweenPlayEntries", false)) return;
                var started = DateTime.Parse(SessionState.GetString(Key + ".Started", ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                Check(DateTime.UtcNow - started < TimeSpan.FromSeconds(100), "Playable duel verification exceeded 100 seconds.");
                if (!EditorApplication.isPlaying || Time.frameCount < 3) return;
                if (!SessionState.GetBool(Key + ".Initialized", false)) { InitializePlay(); return; }
                bool performingBurst = interaction.burstPresentation && interaction.burstPresentation.Stage != ColaBurstPresentation.BurstStage.Idle;
                Check(interaction.bottle.parent == bottleParent && (performingBurst || Vector3.Distance(interaction.bottle.localScale, bottleScale) < .00001f),
                    "Gameplay changed the bottle parent or failed to restore scale outside the burst performance.");
                Check(!interaction.cap || Vector3.Distance(interaction.cap.localPosition, capRest) < .00001f,
                    "The original cap transform must remain bound; the burst uses a separate visual cap.");
                ObserveLiveHand();
                switch (phase)
                {
                    case 0:
                        if (interaction.IsBusy) return;
                        Check(resolved == 2 && interaction.CommittedActionCount == 2 && playerBursts == 0 && bossBursts == 0,
                            "A normal turn pair must resolve exactly one safe player and one safe boss action.");
                        Check(playerMotion > .05f && bossMotion > .05f && playerGripSamples >= 2 && bossGripSamples >= 2,
                            "Both original hands must move and hold the live bottle during actual alternating turns.");
                        Check(match.PlayerTokens == 3 && match.BossTokens == bossRewards && bossRewards >= 1 && bossRewards <= 3,
                            "Safe actions must award the chosen strength to the acting participant.");
                        Check(match.PlayerHealth == 3 && match.BossHealth == 3 && match.IsPlayerTurn && interaction.InputEnabled,
                            "Safe turns must preserve health and return control to the player.");
                        CheckTerminal();
                        CheckIdleAndBottle();
                        report.AppendLine($"PASS: real collider input -> original FPS right hand -> original boss hand -> player input; safe rewards player=3, boss={bossRewards}; health and physical readouts remain synchronized.");
                        match.playerMaxHealth = 1;
                        Restart(123);
                        ResetObservations();
                        interaction.BeginBottle(1);
                        Check(Click(3) && interaction.LastResult.Burst, "The guaranteed fatal player shake did not start.");
                        phase = 1;
                        break;
                    case 1:
                        if (interaction.IsBusy) return;
                        CheckLost();
                        CheckIdleAndBottle();
                        CheckTerminal();
                        Check(!match.ContinueToNextBoss(), "A lost encounter allowed continuation.");
                        Check(!interaction.SelectStrength(1) && !interaction.ConfirmShake() && !Click(1), "The loss state must reject physical and direct shake input.");
                        phase = 2;
                        phaseStarted = Time.unscaledTime;
                        break;
                    case 2:
                        if (Time.unscaledTime - phaseStarted < Mathf.Max(1, interaction.bossThinkSeconds + .25f)) return;
                        CheckLost();
                        report.AppendLine("PASS: a fatal strength-3 player burst gives exactly 5 tokens total, removes one health, ends the run, rejects further input and suppresses the pending boss turn; bottle resets after the burst performance.");
                        match.playerMaxHealth = 3;
                        match.bossBaseHealth = 1;
                        Restart(FindSafeFirstRollSeed());
                        ResetObservations();
                        interaction.BeginBottle(2);
                        Check(Click(1) && !interaction.LastResult.Burst, "The seeded victory probe must begin with a safe strength-1 action.");
                        phase = 3;
                        break;
                    case 3:
                        if (interaction.IsBusy) return;
                        Check(match.State.ToString() == "Won" && match.PlayerHealth == 3 && match.BossHealth == 0 &&
                              match.PlayerTokens == 1 && match.BossTokens == 5 && resolved == 2 && playerBursts == 0 && bossBursts == 1,
                            "A guaranteed boss burst must damage the boss, award exactly 5 boss tokens and produce a win.");
                        Check(victoryBusyGateObserved && !interaction.InputEnabled && !interaction.automaticBossTurns,
                            "A terminal result must lock input and reject next-level continuation until its turn sequence finishes.");
                        CheckTerminal();
                        CheckIdleAndBottle();
                        Check(match.ContinueToNextBoss(), "The completed victory did not allow the next encounter.");
                        Check(match.Level == 2 && match.BossMaxHealth == 2 && match.BossHealth == 2 && match.PlayerHealth == 3 &&
                              match.PlayerTokens == 1 && match.IsPlayerTurn && interaction.InputEnabled && interaction.automaticBossTurns,
                            "The next encounter must increase boss health, heal the player and retain player tokens.");
                        CheckTerminal();
                        Check(interaction.IsBusy && interaction.burstPresentation.BlackoutOpacity == 1 && !Click(2),
                            "Next difficulty must begin under black with input locked.");
                        phase = 30;
                        break;
                    case 30:
                        if (interaction.IsBusy) return;
                        Check(interaction.burstPresentation.BlackoutOpacity == 0, "Next difficulty failed to fade in.");
                        report.AppendLine("PASS: safe player then guaranteed boss burst awards 1/5 tokens and wins; continuation is busy-gated, then advances to level 2 with boss 2/2, player healed and player tokens retained.");
                        ResetObservations();
                        interaction.BeginBottle(999);
                        Check(Click(2), "The next encounter did not accept a normal player action.");
                        phase = 4;
                        break;
                    case 4:
                        if (!driver.IsPlaying || !driver.PlayingBoss || driver.PlaybackTime < 1.65f) return;
                        Check(resolved == 1 && interaction.CommittedActionCount == 2, "The cancellation probe must stop a real boss action after only the player's resolution.");
                        SaveMatchSnapshot();
                        committedBeforeCancel = interaction.CommittedActionCount;
                        resolvedBeforeCancel = resolved;
                        interaction.enabled = false;
                        Check(!interaction.IsBusy && !driver.IsPlaying, "Disabling interaction left the boss hand or pending action running.");
                        CheckIdleAndBottle();
                        phaseFrame = Time.frameCount;
                        phase = 5;
                        break;
                    case 5:
                        if (Time.frameCount < phaseFrame + 2) return;
                        CheckMatchSnapshot();
                        interaction.enabled = true;
                        phaseStarted = Time.unscaledTime;
                        phase = 6;
                        break;
                    case 6:
                        if (Time.unscaledTime - phaseStarted < 1) return;
                        CheckMatchSnapshot();
                        Check(resolved == resolvedBeforeCancel && interaction.CommittedActionCount == 0 && committedBeforeCancel == 2 && !interaction.IsBusy,
                            "Re-enabling interaction retained a cancelled boss callback or stale action count.");
                        CheckIdleAndBottle();
                        report.AppendLine("PASS: disabling/re-enabling interaction during an actual boss grip cancels its hand animation and delayed resolution without extra health/token changes.");
                        match.bossBaseHealth = 3;
                        Restart(123);
                        ResetObservations();
                        interaction.BeginBottle(999);
                        Check(Click(1), "Controller cancellation probe did not begin.");
                        phase = 7;
                        break;
                    case 7:
                        if (!driver.IsPlaying || driver.PlaybackTime < 1.65f) return;
                        match.enabled = false;
                        Check(!interaction.IsBusy && !driver.IsPlaying && !interaction.InputEnabled,
                            "Disabling the match controller must cancel pending hand work and disable input.");
                        CheckIdleAndBottle();
                        match.enabled = true;
                        ObserveResolutionsLast();
                        phaseStarted = Time.unscaledTime;
                        phase = 8;
                        break;
                    case 8:
                        if (Time.unscaledTime - phaseStarted < 1) return;
                        CheckBootState();
                        Check(resolved == 0 && interaction.CommittedActionCount == 0 && !driver.IsPlaying,
                            "Re-enabled match received a cancelled player resolution.");
                        CheckStrengthButtonsAtRest();
                        report.AppendLine("PASS: disabling the match mid-player-grip cancels pending work; re-enabling starts a fresh run and receives no stale reward callback.");
                        Check(interaction.SelectStrength(3) && interaction.Model.SelectedStrength == 3,
                            "The no-scene-reload Play re-entry probe must leave strength 3 selected before exiting Play.");
                        ProjectVerificationBridge.WriteReport(Output, report.ToString());
                        SessionState.SetInt(Key + ".Stage", 1);
                        SessionState.SetBool(Key + ".Initialized", false);
                        SessionState.SetBool(Key + ".BetweenPlayEntries", true);
                        interaction.ShakeResolved -= OnResolved;
                        EditorApplication.isPlaying = false;
                        phase = 9;
                        break;
                    case 20:
                        if (Time.unscaledTime - phaseStarted < 1) return;
                        CheckBootState();
                        CheckStrengthButtonsAtRest();
                        Check(!driver.IsPlaying && interaction.CommittedActionCount == 0 && resolved == 0,
                            "A second Play entry must not begin preview actions or receive callbacks from the previous Play session.");
                        match.playerMaxHealth = 1;
                        Restart(123);
                        interaction.BeginBottle(1);
                        Check(Click(1), "The second Play entry did not accept input.");
                        phase = 21;
                        break;
                    case 21:
                        if (interaction.IsBusy) return;
                        CheckLost();
                        CheckTerminal();
                        match.playerMaxHealth = 3;
                        Restart(123);
                        CheckBootState();
                        report.AppendLine("PASS: a second complete Play entry reuses the existing scene with no reopen/reload; the previously selected strength-3 key and both other keys return to their original positions, health/level/tokens/input reset, and one real fatal action awards exactly 5 tokens once.");
                        report.AppendLine("Scope: automated live Play-mode state, physics input, original hand-bone movement, turn, health, token and physical readout checks. No Game-view visual confirmation, preview renders, frame captures or standalone build.");
                        ProjectVerificationBridge.WriteReport(Output, report.ToString());
                        SessionState.SetBool(Key + ".Completed", true);
                        Debug.Log("COLA_PLAYABLE_DUEL_PASS\n" + report);
                        interaction.ShakeResolved -= OnResolved;
                        EditorApplication.isPlaying = false;
                        phase = 22;
                        break;
                }
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void ResetObservations()
        {
            resolved = playerRewards = bossRewards = playerBursts = bossBursts = 0;
            playerMotion = bossMotion = 0;
            playerGripSamples = bossGripSamples = 0;
            victoryBusyGateObserved = false;
        }

        private static void Restart(int seed)
        {
            match.RestartRun(seed);
            ObserveResolutionsLast();
        }

        private static void ObserveResolutionsLast()
        {
            // RestartRun refreshes the controller subscription; observe its finalized result.
            interaction.ShakeResolved -= OnResolved;
            interaction.ShakeResolved += OnResolved;
        }

        private static void OnResolved(ShakeResult result)
        {
            resolved++;
            int reward = result.Burst ? 5 : result.Prediction.Strength;
            if (interaction.LastActionWasBoss)
            {
                bossRewards += reward;
                if (result.Burst) bossBursts++;
            }
            else
            {
                playerRewards += reward;
                if (result.Burst) playerBursts++;
            }
            if (phase == 3 && match.State.ToString() == "Won")
                victoryBusyGateObserved = interaction.IsBusy && !match.ContinueToNextBoss();
        }

        private static void ObserveLiveHand()
        {
            if (!driver.IsPlaying) return;
            var hand = driver.PlayingBoss ? driver.bossHand : driver.playerHand;
            Check(driver.ActiveHand == hand && driver.UsesAnimatorGraph && interaction.IsBusy,
                "Actual gameplay must animate the acting participant's original hand and lock shared input.");
            float travel = Vector3.Distance(hand.position, driver.PlayingBoss ? bossHandRest : playerHandRest);
            if (driver.PlayingBoss) bossMotion = Mathf.Max(bossMotion, travel);
            else playerMotion = Mathf.Max(playerMotion, travel);
            if (driver.PlaybackTime < 1.3f || driver.PlaybackTime > 3.15f) return;
            Check(driver.BottleHeld, "The held bottle is not attached to the live acting hand.");
            if (driver.PlayingBoss) bossGripSamples++;
            else
            {
                playerGripSamples++;
                float rightOffset = Vector3.Dot(hand.position - interaction.bottle.position, interaction.playerCamera.transform.right);
                bool recoiling = interaction.burstPresentation &&
                    (interaction.burstPresentation.Stage == ColaBurstPresentation.BurstStage.Rupture ||
                     interaction.burstPresentation.Stage == ColaBurstPresentation.BurstStage.Recovery);
                // Burst recoil rotates the attached hand/bottle pair. Keep the side invariant during recoil,
                // and the original 6-cm clearance check during the steady grip and pressure stall.
                Check(rightOffset > (recoiling ? 0 : .06f),
                    "The player must grip from the right: offset=" + rightOffset.ToString("F4") +
                    ", time=" + driver.PlaybackTime.ToString("F3") + ", recoil=" + recoiling);
            }
        }

        private static bool Click(int strength)
        {
            Physics.SyncTransforms();
            var key = interaction.strengthButtons[strength - 1].GetComponent<Collider>();
            Check(key, "The strength button needs its physical collider.");
            return interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(key.bounds.center)) &&
                interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(interaction.bottleCollider.bounds.center));
        }

        private static int FindSafeFirstRollSeed()
        {
            // ResetSession draws the first bottle limit before the first actual pressure roll.
            for (int seed = 0; seed < 256; seed++)
            {
                var random = new System.Random(seed);
                int minimum = Mathf.Clamp(interaction.settings.minimumBottleLimit, 1, 999);
                int maximum = Mathf.Clamp(interaction.settings.maximumBottleLimit, minimum, 999);
                random.Next(minimum, maximum + 1);
                if (random.NextDouble() >= Math.Pow(.5d, interaction.settings.probabilityExponent)) return seed;
            }
            throw new InvalidOperationException("Could not find a deterministic safe first player roll.");
        }

        private static void CheckLost()
        {
            Check(match.State.ToString() == "Lost" && match.PlayerHealth == 0 && match.BossHealth == match.BossMaxHealth &&
                  match.PlayerTokens == 5 && match.BossTokens == 0 && resolved == 1 && playerBursts == 1 && bossBursts == 0 &&
                  interaction.CommittedActionCount == 1 && !interaction.InputEnabled && !interaction.automaticBossTurns,
                "Fatal player result must resolve exactly once, give 5 total tokens and prevent every boss action.");
        }

        private static void CheckBootState()
        {
            Check(match.State.ToString() == "Playing" && match.Level == 1 && match.PlayerHealth == 3 &&
                  match.BossHealth == 3 && match.BossMaxHealth == 3 && match.PlayerTokens == 0 && match.BossTokens == 0 &&
                  match.IsPlayerTurn && interaction.InputEnabled && interaction.automaticBossTurns && !interaction.IsBusy,
                "A fresh run must start at level 1, player/boss 3/3, zero tokens and unlocked player input.");
            CheckTerminal();
        }

        private static void CheckTerminal()
        {
            Check(terminal.PlayerHealth == match.PlayerHealth && terminal.PlayerMaxHealth == match.playerMaxHealth &&
                  terminal.BossHealth == match.BossHealth && terminal.BossMaxHealth == match.BossMaxHealth &&
                  terminal.PlayerTokens == match.PlayerTokens && terminal.BossTokens == match.BossTokens,
                "The physical health/token terminal is out of sync with the duel rules.");
        }

        private static void CheckIdleAndBottle()
        {
            CheckStrengthButtonsAtRest();
            Check(!driver.IsPlaying && interaction.bottle.parent == bottleParent && Vector3.Distance(interaction.bottle.localPosition, bottleRest) < .0001f,
                "The completed or cancelled action did not return the shared bottle to its tabletop rest pose.");
            foreach (var actor in new[] { driver.playerAnimator, driver.bossAnimator })
                Check(actor.enabled && actor.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "The original character did not return to seated Idle: " + actor.name);
        }

        private static void CheckStrengthButtonsAtRest()
        {
            for (int i = 0; i < strengthButtonRest.Length; i++)
                Check(Vector3.Distance(interaction.strengthButtons[i].localPosition, strengthButtonRest[i]) < .00001f,
                    "Strength key " + (i + 1) + " retained a pressed position or drifted from its original rest position.");
        }

        private static void SaveMatchSnapshot()
        {
            savedPlayerTokens = match.PlayerTokens;
            savedBossTokens = match.BossTokens;
            savedPlayerHealth = match.PlayerHealth;
            savedBossHealth = match.BossHealth;
            savedLevel = match.Level;
        }

        private static void CheckMatchSnapshot()
        {
            Check(match.PlayerTokens == savedPlayerTokens && match.BossTokens == savedBossTokens &&
                  match.PlayerHealth == savedPlayerHealth && match.BossHealth == savedBossHealth && match.Level == savedLevel,
                "A cancelled action changed the retained match health, tokens or level.");
        }

        private static void PlayChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            if (!string.IsNullOrEmpty(SessionState.GetString(Key + ".Error", "")))
            {
                Unhook();
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }
            if (SessionState.GetInt(Key + ".Stage", 0) == 1 && !SessionState.GetBool(Key + ".Completed", false))
            {
                ProjectVerificationBridge.Schedule(() =>
                {
                    try
                    {
                        SessionState.SetBool(Key + ".Initialized", false);
                        SessionState.SetBool(Key + ".BetweenPlayEntries", false);
                        EditorApplication.isPlaying = true;
                    }
                    catch (Exception exception) { Fail(exception); }
                });
                return;
            }
            if (!SessionState.GetBool(Key + ".Completed", false))
            {
                Fail(new InvalidOperationException("Play mode ended before duel verification completed."));
                return;
            }
            Unhook();
            ProjectVerificationBridge.Schedule(() =>
            {
                try { MainSceneTools.Verify(); }
                catch (Exception exception) { Fail(exception); }
            });
        }

        private static void Unhook()
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayChanged;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Fail(Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            ProjectVerificationBridge.WriteReport(Output, (report?.ToString() ?? "") + "FAIL: " + exception);
            SessionState.SetString(Key + ".Error", exception.ToString());
            Debug.LogException(exception);
            EditorApplication.update -= Tick;
            if (interaction) interaction.ShakeResolved -= OnResolved;
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
            else
            {
                Unhook();
                if (Application.isBatchMode) EditorApplication.Exit(2);
            }
        }
    }
}
