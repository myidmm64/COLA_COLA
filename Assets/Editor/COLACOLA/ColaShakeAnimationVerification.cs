using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColaCola.Editor
{
    /// <summary>Exercises saved bindings in real Play mode and captures the actual main camera.</summary>
    [InitializeOnLoad]
    public static class ColaShakeAnimationVerification
    {
        public const string Output = "output/shake-animation";
        private const string Running = "COLACOLA.ColaShakeAnimation.Verify";
        private const string Initialized = Running + ".Initialized";
        private const string Error = Running + ".Error";
        private const string Completed = Running + ".Completed";
        private const float CaptureInterval = .2f;
        private static readonly float[] KeyTimes = { .9f, 1.8f, 2.2f };

        private static ColaShakeAnimator driver;
        private static ShakeSelectionInteraction interaction;
        private static RootPose playerRoot, bossRoot, bottleRest;
        private static StringBuilder report;
        private static System.Random expectedRandom;
        private static int phase, resolvedEvents, phaseFrame, previewPressure, previewLimit, previewBottle, previewCount;
        private static float phaseStarted, nextCapture, maximumGripPositionError, maximumGripAngleError;
        private static float maximumWristMotion, maximumBottleLift, maximumBottleMotion;
        private static int keyIndex, gripSamples;
        private static bool gripCaptured;
        private static Vector3 gripPosition, firstGripBottlePosition, wristAtRest;
        private static Quaternion gripRotation;
        private static Transform wrist;
        private static readonly List<float> FrameTimes = new List<float>();

        private sealed class RootPose
        {
            public readonly Transform node, parent;
            public readonly Vector3 position, scale;
            public readonly Quaternion rotation;

            public RootPose(Transform transform)
            {
                node = transform;
                parent = transform.parent;
                position = transform.localPosition;
                rotation = transform.localRotation;
                scale = transform.localScale;
            }

            public void CheckRestored(string label)
            {
                Check(node && node.parent == parent && Vector3.Distance(node.localPosition, position) < .0001f &&
                      Quaternion.Angle(node.localRotation, rotation) < .03f && Vector3.Distance(node.localScale, scale) < .00001f,
                    label + " placement, parent or imported scale changed.");
            }
        }

        static ColaShakeAnimationVerification()
        {
            if (SessionState.GetBool(Running, false)) Hook();
        }

        public static void Begin()
        {
            try
            {
                Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Start verification from the saved edit-time scene.");
                Directory.CreateDirectory(Output);
                foreach (string owner in new[] { "player", "boss" })
                {
                    string folder = Output + "/" + owner + "-frames";
                    Directory.CreateDirectory(folder);
                    // These are only this verifier's numbered preview frames; retain all other artifacts.
                    foreach (string file in Directory.GetFiles(folder, "Frame-*.png")) File.Delete(file);
                }
                SessionState.SetString(Error, "");
                SessionState.SetBool(Completed, false);
                SessionState.SetBool(Initialized, false);
                SessionState.SetString(Running + ".Started", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                SessionState.SetBool(Running, true);
                Hook();
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void Hook()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayChanged;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayChanged;
        }

        private static void InitializePlay()
        {
            driver = UnityEngine.Object.FindFirstObjectByType<ColaShakeAnimator>();
            interaction = UnityEngine.Object.FindFirstObjectByType<ShakeSelectionInteraction>();
            Check(driver && interaction && interaction.shakeAnimator == driver, "Saved interaction does not bind the shake driver.");
            Check(Camera.main, "No actual main camera is available for runtime previews.");
            foreach (bool boss in new[] { false, true })
                for (int strength = 1; strength <= 3; strength++)
                    Check(driver.CanPlay(boss, strength), "Missing actor/bottle clip for " + (boss ? "boss" : "player") + " strength " + strength);
            playerRoot = new RootPose(driver.playerAnimator.transform);
            bossRoot = new RootPose(driver.bossAnimator.transform);
            bottleRest = new RootPose(driver.bottle);
            report = new StringBuilder("Real Play-mode verification of saved player and boss shake animation bindings.\n");
            resolvedEvents = 0;
            interaction.ShakeResolved += _ => resolvedEvents++;
            interaction.ResetSession(123);
            expectedRandom = new System.Random(123);
            int minimum = Mathf.Clamp(interaction.settings.minimumBottleLimit, 1, 999);
            int maximum = Mathf.Clamp(interaction.settings.maximumBottleLimit, minimum, 999);
            expectedRandom.Next(minimum, maximum + 1);
            interaction.BeginBottle(999);
            Check(interaction.SelectStrength(3) && interaction.ConfirmShake(), "Player strength-three confirmation did not start.");
            Check(driver.IsPlaying && !driver.PlayingBoss && interaction.IsBusy, "Player animation was not started by actual confirmation.");
            Check(!interaction.LastResult.Burst && interaction.Model.Pressure == 3 && interaction.CommittedActionCount == 1,
                "Seeded safe confirmation changed pressure/action count incorrectly.");
            Check(Math.Abs(interaction.LastResult.Roll - expectedRandom.NextDouble()) < 1e-12, "Initial pressure RNG diverged from the seeded control.");
            Check(!interaction.SelectStrength(1) && !interaction.ConfirmShake(), "Repeated player inputs were accepted during playback.");
            phase = 0;
            StartCapture(false);
            SessionState.SetBool(Initialized, true);
        }

        private static void StartCapture(bool boss)
        {
            FrameTimes.Clear();
            nextCapture = 0;
            keyIndex = 0;
            gripCaptured = false;
            gripSamples = 0;
            maximumGripPositionError = maximumGripAngleError = maximumWristMotion = maximumBottleLift = maximumBottleMotion = 0;
            var actor = boss ? driver.bossAnimator : driver.playerAnimator;
            string wristName = boss ? "hand.R" : "Wrist.R";
            wrist = actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == wristName);
            wristAtRest = wrist.position;
            phaseStarted = Time.unscaledTime;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            try
            {
                DateTime started = DateTime.Parse(SessionState.GetString(Running + ".Started", ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if (DateTime.UtcNow - started > TimeSpan.FromSeconds(180)) throw new TimeoutException("Shake animation Play-mode verification exceeded 180 seconds.");
                if (!EditorApplication.isPlaying || Time.frameCount < 3) return;
                if (!SessionState.GetBool(Initialized, false)) { InitializePlay(); return; }
                playerRoot.CheckRestored("Player actor root");
                bossRoot.CheckRestored("Boss actor root");
                Check(driver.bottle.parent == bottleRest.parent && Vector3.Distance(driver.bottle.localScale, bottleRest.scale) < .00001f,
                    "Playback changed the bottle parent or imported scale.");

                switch (phase)
                {
                    case 0:
                        if (driver.IsPlaying) { ObservePerformance(false); return; }
                        if (interaction.IsBusy || Time.unscaledTime - phaseStarted < ColaShakeAnimationFactory.Duration) return;
                        Check(driver.LastPlaybackCompleted && resolvedEvents == 1 && interaction.CommittedActionCount == 1 && interaction.Model.Pressure == 3,
                            "Player performance did not complete exactly one safe pressure action.");
                        FinishCapture(false);
                        CheckIdleAndBottle();
                        previewPressure = interaction.Model.Pressure;
                        previewLimit = interaction.Model.Limit;
                        previewBottle = interaction.BottleNumber;
                        previewCount = interaction.CommittedActionCount;
                        driver.PreviewBossShake();
                        Check(driver.IsPlaying && driver.PlayingBoss && interaction.IsBusy, "Boss context preview did not start or lock shared input.");
                        Check(!interaction.SelectStrength(1) && !interaction.ConfirmShake(), "Boss preview allowed a competing pressure action.");
                        phase = 1;
                        StartCapture(true);
                        break;
                    case 1:
                        CheckPreviewHasNoRuleEffects();
                        if (driver.IsPlaying) { ObservePerformance(true); return; }
                        if (interaction.IsBusy || Time.unscaledTime - phaseStarted < ColaShakeAnimationFactory.Duration) return;
                        Check(driver.LastPlaybackCompleted, "Boss preview ended without completing its clip.");
                        FinishCapture(true);
                        CheckIdleAndBottle();
                        report.AppendLine("PASS: boss preview locks shared input and changes no pressure, public limit, bottle number, action count or resolution events.");
                        driver.PreviewBossShake();
                        Check(driver.IsPlaying && driver.PlayingBoss, "Boss cancellation probe did not start.");
                        phase = 2;
                        break;
                    case 2:
                        CheckPreviewHasNoRuleEffects();
                        if (driver.PlaybackTime < 1.6f) return;
                        MainSceneTools.Capture(Camera.main, Output + "/Boss-Cancel-MidGrip.png", 900, 506);
                        driver.enabled = false;
                        Check(!driver.IsPlaying && !interaction.IsBusy, "Disabling the animation driver did not cancel a boss preview.");
                        CheckIdleAndBottle();
                        driver.enabled = true;
                        phase = 3;
                        phaseFrame = Time.frameCount;
                        break;
                    case 3:
                        if (Time.frameCount < phaseFrame + 2) return;
                        CheckIdleAndBottle();
                        CheckPreviewHasNoRuleEffects();
                        report.AppendLine("PASS: disabling/re-enabling the driver mid-grip restores the boss rig, bottle and Idle without a rule action.");
                        interaction.BeginBottle(999);
                        Check(interaction.SelectStrength(2) && interaction.ConfirmShake(), "Player cancellation probe did not start.");
                        Check(Math.Abs(interaction.LastResult.Roll - expectedRandom.NextDouble()) < 1e-12,
                            "Boss preview/cancellation consumed pressure randomness.");
                        report.AppendLine("PASS: the next real pressure result uses the expected next seeded RNG value after boss preview and cancellation.");
                        phase = 4;
                        break;
                    case 4:
                        if (driver.PlaybackTime < 1.6f) return;
                        MainSceneTools.Capture(Camera.main, Output + "/Player-Cancel-MidGrip.png", 900, 506);
                        interaction.enabled = false;
                        Check(!driver.IsPlaying && !interaction.IsBusy, "Disabling interaction left its player performance running.");
                        CheckIdleAndBottle();
                        interaction.enabled = true;
                        phase = 5;
                        phaseStarted = Time.unscaledTime;
                        break;
                    case 5:
                        if (Time.unscaledTime - phaseStarted < .4f) return;
                        CheckIdleAndBottle();
                        Check(!interaction.IsBusy && !driver.IsPlaying && interaction.CommittedActionCount == 0 && resolvedEvents == 1,
                            "Re-enabled interaction retained stale completion callbacks or a committed action.");
                        report.AppendLine("PASS: disabling/re-enabling interaction mid-grip restores both rig and bottle, cancels stale callbacks and resets input state.");
                        report.AppendLine("PASS: both seated actor roots, parents and imported scales remain fixed throughout full playback and cancellation.");
                        report.AppendLine("Scope: player input starts the player animation; boss animation is a context preview. No boss AI, damage, currency or automatic turn system is introduced.");
                        File.WriteAllText(Output + "/Playmode-validation.txt", report.ToString());
                        MainSceneTools.Capture(Camera.main, Output + "/MainScene-AfterShake.png");
                        SessionState.SetBool(Completed, true);
                        Debug.Log("COLA_SHAKE_ANIMATION_PLAYMODE_PASS\n" + report);
                        phase = 6;
                        EditorApplication.isPlaying = false;
                        break;
                }
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void ObservePerformance(bool boss)
        {
            Check(driver.PlayingBoss == boss && interaction.IsBusy, "Playback ownership or input lock changed during performance.");
            float time = driver.PlaybackTime;
            maximumWristMotion = Mathf.Max(maximumWristMotion, Vector3.Distance(wrist.position, wristAtRest));
            maximumBottleLift = Mathf.Max(maximumBottleLift, driver.bottle.localPosition.y - bottleRest.position.y);
            if (time >= ColaShakeAnimationFactory.GripStart + .1f && time <= ColaShakeAnimationFactory.GripEnd - .1f)
            {
                Quaternion inverse = Quaternion.Inverse(wrist.rotation);
                Vector3 relativePosition = inverse * (driver.bottle.position - wrist.position);
                Quaternion relativeRotation = inverse * driver.bottle.rotation;
                if (!gripCaptured)
                {
                    gripCaptured = true;
                    gripPosition = relativePosition;
                    gripRotation = relativeRotation;
                    firstGripBottlePosition = driver.bottle.position;
                }
                maximumGripPositionError = Mathf.Max(maximumGripPositionError, Vector3.Distance(relativePosition, gripPosition));
                maximumGripAngleError = Mathf.Max(maximumGripAngleError, Quaternion.Angle(relativeRotation, gripRotation));
                maximumBottleMotion = Mathf.Max(maximumBottleMotion, Vector3.Distance(driver.bottle.position, firstGripBottlePosition));
                gripSamples++;
            }
            string owner = boss ? "boss" : "player";
            // PlaybackTime describes the most recently sampled LateUpdate pose, so camera and measured hands agree.
            if (time + .0001f >= nextCapture)
            {
                MainSceneTools.Capture(Camera.main, Output + "/" + owner + "-frames/Frame-" + FrameTimes.Count.ToString("D3") + ".png", 900, 506);
                FrameTimes.Add(time);
                nextCapture = time + CaptureInterval;
            }
            if (keyIndex < KeyTimes.Length && time >= KeyTimes[keyIndex])
            {
                string label = boss ? "Boss" : "Player";
                MainSceneTools.Capture(Camera.main, Output + "/" + label + "-Phase" + (keyIndex + 1) + ".png");
                report.AppendLine(label + " phase " + (keyIndex + 1) + " captured at sampled clip time " + time.ToString("F4", CultureInfo.InvariantCulture) + "s.");
                keyIndex++;
            }
        }

        private static void FinishCapture(bool boss)
        {
            string label = boss ? "Boss" : "Player";
            Check(gripCaptured && gripSamples >= 3, label + " performance had no sustained sampled grip.");
            Check(maximumGripPositionError <= .003f && maximumGripAngleError <= 1f,
                label + " bottle slipped relative to its gripping wrist: " + (maximumGripPositionError * 1000).ToString("F3") + "mm, " + maximumGripAngleError.ToString("F3") + " degrees.");
            Check(maximumWristMotion > .05f && maximumBottleLift > .025f && maximumBottleMotion > .005f,
                label + " hand/bottle performance did not reach, lift and move visibly.");
            Check(FrameTimes.Count >= 8 && keyIndex == KeyTimes.Length, label + " runtime preview did not capture all phases.");
            string owner = boss ? "boss" : "player";
            File.WriteAllLines(Output + "/" + owner + "-frame-times.txt", FrameTimes.Select(t => t.ToString("F6", CultureInfo.InvariantCulture)));
            report.AppendLine($"PASS: {label}: {FrameTimes.Count} native runtime frames; grip samples {gripSamples}; maximum wrist/bottle drift {maximumGripPositionError * 1000:F3}mm / {maximumGripAngleError:F3} degrees; wrist travel {maximumWristMotion:F3}m, bottle lift {maximumBottleLift:F3}m.");
        }

        private static void CheckPreviewHasNoRuleEffects()
        {
            Check(interaction.Model.Pressure == previewPressure && interaction.Model.Limit == previewLimit &&
                  interaction.BottleNumber == previewBottle && interaction.CommittedActionCount == previewCount && resolvedEvents == 1,
                "Boss animation preview altered pressure rules or emitted a committed-turn result.");
        }

        private static void CheckIdleAndBottle()
        {
            playerRoot.CheckRestored("Player actor root");
            bossRoot.CheckRestored("Boss actor root");
            bottleRest.CheckRestored("Shared bottle");
            foreach (var actor in new[] { driver.playerAnimator, driver.bossAnimator })
                Check(actor.enabled && actor.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Seated Idle was not restored: " + actor.name);
        }

        private static void PlayChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayChanged;
            if (!string.IsNullOrEmpty(SessionState.GetString(Error, "")) || !SessionState.GetBool(Completed, false))
            {
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }
            EditorApplication.delayCall += () =>
            {
                try
                {
                    EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                    MainSceneTools.Capture(Camera.main, Output + "/MainScene-ShakeAnimation.png");
                    PressureInteractionSceneTools.Verify();
                }
                catch (Exception exception) { Fail(exception); }
            };
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Fail(Exception exception)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/Playmode-error.txt", exception.ToString());
            SessionState.SetString(Error, exception.ToString());
            Debug.LogException(exception);
            EditorApplication.update -= Tick;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (driver) driver.CancelAndRestore();
                EditorApplication.isPlaying = false;
            }
            else
            {
                SessionState.SetBool(Running, false);
                EditorApplication.playModeStateChanged -= PlayChanged;
                if (Application.isBatchMode) EditorApplication.Exit(2);
            }
        }
    }
}
