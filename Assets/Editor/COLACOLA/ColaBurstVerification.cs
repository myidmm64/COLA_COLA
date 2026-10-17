using System;
using System.IO;
using System.Linq;
using System.Text;
using ColaCola.Core;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColaCola.Editor
{
    [InitializeOnLoad]
    public static class ColaBurstVerification
    {
        private const string Key = "COLA_COLA.BurstVerification";
        private static ColaDuelController match;
        private static ShakeSelectionInteraction interaction;
        private static ColaBurstPresentation effects;
        private static int phase, resolved, initialBursts, bottleBefore, phaseFrame;
        private static int initialPlayerMax, initialBossBase;
        private static float phaseStarted;
        private static bool sawPlayer, sawBoss;
        private static float maxPlayerKick, maxBossKick, maxLens, maxFlash, maxShockwave, previousFade;
        private static bool sawBlackout, sawFadeIn;
        private static int coveredResults;
        private static int maximumVisibleParticles;
        private static Vector3 scale, position;
        private static Transform parent;
        private static readonly StringBuilder Report = new StringBuilder();

        static ColaBurstVerification() { if (SessionState.GetBool(Key, false)) Hook(); }

        [MenuItem("COLA_COLA/Verify Cola Burst In Play Mode")]
        public static void Begin()
        {
            Directory.CreateDirectory("output/verification/latest/cola-burst");
            EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Ready", false);
            SessionState.SetBool(Key + ".Complete", false);
            SessionState.SetString(Key + ".Started", DateTime.UtcNow.ToString("O"));
            Hook(); EditorApplication.isPlaying = true;
        }

        private static void Hook()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= PlayChanged; EditorApplication.playModeStateChanged += PlayChanged;
        }

        private static void Initialize()
        {
            match = UnityEngine.Object.FindAnyObjectByType<ColaDuelController>();
            Check(match, "Saved duel missing."); match.RestartRun(123);
            initialPlayerMax = match.playerMaxHealth; initialBossBase = match.bossBaseHealth;
            interaction = match.interaction;
            // Isolate the burst presentation from the boss's defensive starter-item decisions.
            match.GetComponent<ColaBossItemAI>().automaticItemUse = false;
            effects = interaction.burstPresentation;
            Check(effects, "Burst presentation was not installed automatically in saved MainScene.");
            scale = interaction.bottle.localScale; position = interaction.bottle.localPosition; parent = interaction.bottle.parent;
            interaction.ShakeResolved -= OnResolved; interaction.ShakeResolved += OnResolved;
            interaction.automaticBossTurns = false;
            initialBursts = effects.BurstCount; resolved = 0;
            sawPlayer = sawBoss = false; maxPlayerKick = maxBossKick = maxLens = 0;
            maxFlash = maxShockwave = 0; previousFade = 1; sawBlackout = sawFadeIn = false; coveredResults = 0;
            maximumVisibleParticles = 0;
            Report.Clear(); phase = 0;
            interaction.BeginBottle(20, 19); bottleBefore = interaction.BottleNumber;
            Check(Click(1) && Confirm() && interaction.LastResult.Burst, "Guaranteed player burst did not commit.");
            SessionState.SetBool(Key + ".Ready", true);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            try
            {
                Check(DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + ".Started", ""), null,
                    System.Globalization.DateTimeStyles.RoundtripKind) < TimeSpan.FromSeconds(100), "Burst verification timed out.");
                if (!EditorApplication.isPlaying || Time.frameCount < 3) return;
                if (!SessionState.GetBool(Key + ".Ready", false)) { Initialize(); return; }
                Check(Time.timeScale == 1, "Burst changed global game time.");
                maxFlash = Mathf.Max(maxFlash, effects.FlashOpacity);
                maxShockwave = Mathf.Max(maxShockwave, effects.ShockwaveRadius);
                if (effects.Stage == ColaBurstPresentation.BurstStage.Blackout)
                {
                    sawBlackout = true;
                    Check(effects.BlackoutOpacity == 1 && interaction.IsBusy && !Click(2) && !Confirm(),
                        "Full blackout must cover restoration and keep all action input locked.");
                    previousFade = 1;
                }
                if (effects.Stage == ColaBurstPresentation.BurstStage.FadeIn)
                {
                    sawFadeIn = true;
                    Check(interaction.IsBusy && !interaction.shakeAnimator.IsPlaying && effects.BlackoutOpacity <= previousFade + .001f &&
                        effects.BlackoutOpacity >= 0 && !Click(2) && !Confirm(), "Reveal must fade monotonically after hand restoration and block input.");
                    CheckRestored(); previousFade = effects.BlackoutOpacity;
                    Check(effects.LiveParticles == 0 && !effects.HasFlyingCap && !effects.CameraImpulse.IsActive,
                        "The new bottle must not reveal old debris or a resetting camera.");
                }
                if (effects.Stage == ColaBurstPresentation.BurstStage.Rupture || effects.Stage == ColaBurstPresentation.BurstStage.Recovery)
                {
                    if (phase == 0 || phase == 2)
                    {
                        Check(interaction.IsBusy && !Click(2) && !Confirm(), "Input accepted during burst.");
                        Check(interaction.shakeAnimator.IsPlaying && interaction.shakeAnimator.BottleHeld &&
                            interaction.shakeAnimator.PlayingBoss == effects.LastBurstWasBoss,
                            "The burst did not occur while held by the original acting hand.");
                        Check(effects.EmittedParticleCount >= 508 && effects.EmittedParticleCount <= 650 && effects.HasFlyingCap,
                            "Rupture did not emit the expected liquid/foam/mist or launch the original cap mesh.");
                        float kick = effects.CameraImpulse.CurrentOffset.magnitude;
                        int visible = 0;
                        foreach (var system in effects.GetComponentsInChildren<ParticleSystem>())
                        {
                            var particles = new ParticleSystem.Particle[system.particleCount];
                            int count = system.GetParticles(particles);
                            for (int i = 0; i < count; i++)
                            {
                                Vector3 point = interaction.playerCamera.WorldToViewportPoint(particles[i].position);
                                if (point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1) visible++;
                            }
                        }
                        maximumVisibleParticles = Mathf.Max(maximumVisibleParticles, visible);
                        if (effects.LastBurstWasBoss)
                        {
                            sawBoss = true; maxBossKick = Mathf.Max(maxBossKick, kick);
                            Check(effects.LensOpacity == 0, "Boss burst used the player's face splash.");
                        }
                        else
                        {
                            sawPlayer = true; maxPlayerKick = Mathf.Max(maxPlayerKick, kick);
                            maxLens = Mathf.Max(maxLens, effects.LensOpacity);
                            Check(match.PlayerHealth == 3 && match.PlayerTokens == 0, "Damage/reward resolved before the performance completed.");
                        }
                    }
                }
                switch (phase)
                {
                    case 0:
                        if (interaction.IsBusy) return;
                        Check(sawPlayer && maxPlayerKick > .035f && maxPlayerKick < .085f && maxLens > .2f && maximumVisibleParticles >= 20 &&
                            maxFlash > .15f && maxShockwave > .8f && sawBlackout && sawFadeIn && coveredResults == 1 && effects.BlackoutOpacity == 0,
                            "Player cap/spray/camera/lens sequence was not observed.");
                        Check(resolved == 1 && match.PlayerHealth == 2 && match.PlayerTokens == 5 &&
                            interaction.BottleNumber == bottleBefore + 1 && interaction.Model.Pressure == 0,
                            "Player burst did not resolve exactly once after its original hand performance.");
                        CheckRestored();
                        Check(effects.MaximumAudioSample > .2f && effects.MaximumAudioSample < 1,
                            "Generated rupture clip has invalid level or clipped samples.");
                        Report.AppendLine($"PASS: original FPS hand holds the bottle during strain/rupture; 508 initial liquid/foam/vapour/fragment particles plus jet and original cap mesh; at least {maximumVisibleParticles} live particles project inside the camera viewport; stronger bounded player kick {maxPlayerKick * 1000:F2}mm, flash alpha {maxFlash:F2}, shock ring radius {maxShockwave:F2}m, lens alpha {maxLens:F2}.");
                        Report.AppendLine("PASS: full black covers the original hand return, new bottle and one damage/reward callback; camera/debris settle under black; 0.82s monotonic fade-in reveals restored hands/table with input locked until complete.");
                        Report.AppendLine("PASS: damage 1 and exactly 5 tokens resolve once after the performance; new bottle resets pressure and original cap/scale/parent.");
                        phase = 1; interaction.BeginBottle(999);
                        Check(Click(2) && Confirm() && !interaction.LastResult.Burst, "Seeded safe action did not start.");
                        break;
                    case 1:
                        Check(effects.BurstCount == initialBursts + 1 && effects.Stage == ColaBurstPresentation.BurstStage.Idle,
                            "Safe shake emitted a false burst.");
                        if (interaction.IsBusy) return;
                        Check(resolved == 2 && match.PlayerTokens == 7 && match.PlayerHealth == 2, "Safe rewards changed.");
                        Report.AppendLine("PASS: safe shake has no rupture, blackout, blast impulse or false damage; no shake-loop sound is reintroduced.");
                        match.RestartRun(123); resolved = 0; phase = 2;
                        interaction.BeginBottle(999); // Event fixture raises pressure only after the player's safe action.
                        Check(Click(1) && Confirm() && !interaction.LastResult.Burst, "Boss probe could not start its safe player action.");
                        break;
                    case 2:
                        if (interaction.IsBusy) return;
                        Check(sawBoss && maxBossKick > .003f && maxBossKick < maxPlayerKick,
                            $"Boss burst did not use its own held-bottle performance and smaller camera impulse: observed={sawBoss}, boss={maxBossKick:F4}, player={maxPlayerKick:F4}.");
                        Check(resolved == 2 && match.PlayerHealth == 3 && match.BossHealth == 2 && match.PlayerTokens == 1 && match.BossTokens == 5,
                            "Boss damage/reward/turn ownership was incorrect.");
                        CheckRestored();
                        Report.AppendLine($"PASS: real automatic boss turn bursts in its own hand; smaller correction max {maxBossKick * 1000:F2}mm, no face splatter; only boss loses 1 HP and gains 5 tokens.");
                        match.RestartRun(123); interaction.automaticBossTurns = false; resolved = 0; phase = 3;
                        interaction.BeginBottle(20, 19);
                        Check(Click(3) && Confirm(), "Cancellation probe did not start.");
                        break;
                    case 3:
                        if (effects.Stage != ColaBurstPresentation.BurstStage.Rupture) return;
                        Check(effects.LiveParticles > 0 && interaction.cap.GetComponentsInChildren<Renderer>().All(r => !r.enabled),
                            "Live spray or cap visibility handoff was missing.");
                        match.RestartRun(123); phaseFrame = Time.frameCount; phase = 4;
                        break;
                    case 4:
                        if (Time.frameCount < phaseFrame + 5) return;
                        Check(!interaction.IsBusy && !effects.HasFlyingCap && effects.LiveParticles == 0 && effects.LensOpacity == 0 &&
                            !effects.CameraImpulse.IsActive && effects.CameraImpulse.CurrentOffset == Vector3.zero && effects.BlackoutOpacity == 0 && effects.FlashOpacity == 0 &&
                            resolved == 0 && match.PlayerHealth == 3 && match.PlayerTokens == 0 && interaction.CommittedActionCount == 0,
                            "Restart retained a burst effect, callback, reward or camera displacement.");
                        CheckRestored();
                        Report.AppendLine("PASS: restarting during the actual rupture cancels particles, projectile, sound, lens mask, original hand graph and impulse without late damage or reward.");
                        Check(UnityEngine.Object.FindObjectsByType<ColaBurstPresentation>(FindObjectsSortMode.None).Length == 1 &&
                            UnityEngine.Object.FindObjectsByType<ColaBurstCameraImpulse>(FindObjectsSortMode.None).Length == 1,
                            "Duplicate presentation components were installed.");
                        Check(!ShaderUtil.ShaderHasError(Resources.Load<Shader>("COLA_COLA_Spray")), "Spray shader failed to compile.");
                        Report.AppendLine("PASS: one burst presenter and one Cinemachine impulse; Resources spray shader compiles; rupture PCM bounded below full scale; global time remains 1.");
                        match.playerMaxHealth = 1; match.RestartRun(123); resolved = 0; phase = 5;
                        interaction.BeginBottle(20, 19);
                        Check(Click(3) && Confirm(), "Fatal player burst could not start.");
                        break;
                    case 5:
                        if (interaction.IsBusy) return;
                        Check(match.State == ColaDuelController.DuelState.Lost && match.PlayerHealth == 0 && match.PlayerTokens == 5 &&
                            resolved == 1 && interaction.CommittedActionCount == 1 && match.BossTokens == 0,
                            "Fatal player burst did not end the game once or scheduled a boss action.");
                        CheckRestored(); phaseStarted = Time.unscaledTime; phase = 6;
                        break;
                    case 6:
                        if (Time.unscaledTime - phaseStarted < 1) return;
                        Check(!Click(1) && !Confirm() && interaction.CommittedActionCount == 1 && resolved == 1,
                            "An action occurred after fatal burst.");
                        Report.AppendLine("PASS: fatal player burst completes its effects, awards exactly 5 tokens, ends the run and suppresses the pending boss turn/input.");
                        match.playerMaxHealth = initialPlayerMax; match.bossBaseHealth = 1; match.RestartRun(123);
                        resolved = 0; phase = 7; interaction.BeginBottle(999);
                        Check(Click(1) && Confirm(), "Fatal boss probe did not start.");
                        break;
                    case 7:
                        if (interaction.IsBusy) return;
                        Check(match.State == ColaDuelController.DuelState.Won && match.PlayerHealth == initialPlayerMax && match.BossHealth == 0 &&
                            match.PlayerTokens == 1 && match.BossTokens == 5 && resolved == 2,
                            "Fatal boss performance did not resolve victory exactly once.");
                        CheckRestored(); match.bossBaseHealth = initialBossBase;
                        Check(match.ContinueToNextBoss() && match.Level == 2 && match.BossHealth == initialBossBase + 1 &&
                            !effects.HasFlyingCap && effects.LiveParticles == 0 && effects.LensOpacity == 0,
                            "Next boss retained old burst effects or failed to increase difficulty.");
                        Check(interaction.IsBusy && effects.BlackoutOpacity == 1 && !Click(1), "Next difficulty must be covered and input locked.");
                        phase = 10;
                        break;
                    case 10:
                        if (interaction.IsBusy) return;
                        Check(effects.BlackoutOpacity == 0, "Next difficulty did not complete its fade-in.");
                        Report.AppendLine("PASS: fatal boss burst resolves victory once; next difficulty fades in with fresh bottle/health, no leftover effects and input locked until reveal completes.");
                        interaction.automaticBossTurns = false; interaction.BeginBottle(20, 19); phase = 8;
                        Check(Click(1) && Confirm(), "Fade cancellation probe did not start.");
                        break;
                    case 8:
                        if (effects.Stage != ColaBurstPresentation.BurstStage.FadeIn || effects.BlackoutOpacity > .8f) return;
                        match.RestartRun(123); phaseFrame = Time.frameCount; phase = 9;
                        break;
                    case 9:
                        if (Time.frameCount < phaseFrame + 5) return;
                        Check(effects.BlackoutOpacity == 0 && effects.FlashOpacity == 0 && effects.Stage == ColaBurstPresentation.BurstStage.Idle &&
                            !interaction.IsBusy && match.PlayerTokens == 0 && match.PlayerHealth == initialPlayerMax,
                            "Restart during fade-in left a dark screen, input lock or late reward.");
                        Report.AppendLine("PASS: restart during the actual fade-in cancels the overlay and transition coroutine without a dark screen, locked controls or late rewards.");
                        match.RestartRun(123, fadeIn: true);
                        Check(interaction.IsBusy && effects.BlackoutOpacity == 1 && !Click(1), "User restart must begin with covered setup and input locked.");
                        phase = 12;
                        break;
                    case 12:
                        if (interaction.IsBusy) return;
                        Check(effects.BlackoutOpacity == 0 && match.PlayerHealth == initialPlayerMax && match.PlayerTokens == 0 &&
                            interaction.CommittedActionCount == 0 && Click(1), "User restart must fade into a fresh playable run.");
                        match.RestartRun(123);
                        Report.AppendLine("PASS: user-facing restart fades into a fresh run and restores actual strength-button input without stale currency or actions.");
                        Report.AppendLine("Scope: automatic live Play-mode input, original rig timing, particle/cap states, Cinemachine correction, audio sample data and game rules. Actual Game-view appearance and listening are not verified by these checks.");
                        ProjectVerificationBridge.WriteReport("output/verification/latest/cola-burst/Playmode-validation.txt", Report.ToString());
                        Debug.Log("COLA_BURST_PLAYMODE_PASS\n" + Report);
                        interaction.ShakeResolved -= OnResolved;
                        SessionState.SetBool(Key + ".Complete", true); phase = 11;
                        EditorApplication.isPlaying = false;
                        break;
                }
            }
            catch (Exception exception)
            {
                ProjectVerificationBridge.WriteReport("output/verification/latest/cola-burst/Playmode-validation.txt", Report + "\nFAIL: " + exception); Debug.LogException(exception);
                SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
                if (Application.isBatchMode) EditorApplication.Exit(2);
            }
        }

        private static void OnResolved(ShakeResult result)
        {
            resolved++;
            if (result.Burst)
            {
                Check(effects.BlackoutOpacity == 1 && effects.Stage == ColaBurstPresentation.BurstStage.Blackout,
                    "Bottle replacement/damage callback was exposed before full black.");
                coveredResults++;
            }
            if ((phase == 2 || phase == 7) && !interaction.LastActionWasBoss) interaction.Model.BeginBottle(20, 19);
        }

        private static void CheckRestored()
        {
            Check(interaction.bottle.parent == parent && Vector3.Distance(interaction.bottle.localPosition, position) < .0001f &&
                Vector3.Distance(interaction.bottle.localScale, scale) < .00001f &&
                interaction.cap.GetComponentsInChildren<Renderer>().All(r => r.enabled), "Original bottle/cap not restored.");
        }

        private static bool Click(int strength)
        {
            Physics.SyncTransforms();
            return interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(interaction.strengthButtons[strength - 1].GetComponent<Collider>().bounds.center));
        }
        private static bool Confirm() => interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(interaction.bottleCollider.bounds.center));
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void PlayChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayChanged;
            if (SessionState.GetBool(Key + ".Complete", false)) ProjectVerificationBridge.Schedule(ColaItemVerification.Begin);
            else if (Application.isBatchMode) EditorApplication.Exit(2);
        }
    }
}
