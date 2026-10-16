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
    /// <summary>Exercises live bindings, input and bone movement in Play mode without preview rendering.</summary>
    [InitializeOnLoad]
    public static class ColaShakeAnimationVerification
    {
        public const string Output = "output/shake-animation";
        private const string Running = "COLACOLA.ColaShakeAnimation.Verify";
        private const string Initialized = Running + ".Initialized";
        private const string Error = Running + ".Error";
        private const string Completed = Running + ".Completed";
        private static readonly float[] KeyTimes = { .9f, 1.8f, 2.2f };

        private static ColaShakeAnimator driver;
        private static ShakeSelectionInteraction interaction;
        private static RootPose playerRoot, bossRoot, bottleRest;
        private static StringBuilder report;
        private static System.Random expectedRandom;
        private static int phase, resolvedEvents, phaseFrame, previewPressure, previewLimit, previewBottle, previewCount;
        private static float phaseStarted, maximumGripPositionError, maximumGripAngleError;
        private static float maximumWristMotion, maximumBottleLift, maximumBottleMotion;
        private static int keyIndex, gripSamples;
        private static bool gripCaptured;
        private static Vector3 gripPosition, firstGripBottlePosition, wristAtRest;
        private static Quaternion gripRotation;
        private static Transform wrist;
        private static Transform[] otherHands;
        private static Vector3[] otherHandsAtRest;
        private static float maximumOtherHandMotion;
        private static OriginalHandProbe handMesh;
        private static int duelResolvedBefore, liveBossStrength, cancelResolvedCount;

        private sealed class OriginalHandProbe
        {
            private readonly SkinnedMeshRenderer renderer;
            private readonly Transform[] bones;
            private readonly Matrix4x4[] bind;
            private readonly Vector3[] vertices, rest;
            private readonly BoneWeight[] weights;
            private readonly int[] indices;
            public readonly string assetPath;
            public float MaximumMotion { get; private set; }

            public OriginalHandProbe(Animator actor, Transform hand, bool boss)
            {
                string folder = boss ? "Assets/2.Assets/03_Simple_Character_PSX/" : "Assets/2.Assets/02_Player_Arms/";
                renderer = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r =>
                    AssetDatabase.GetAssetPath(r.sharedMesh).StartsWith(folder, StringComparison.Ordinal) &&
                    r.bones.Any(b => b && b.IsChildOf(hand)));
                Check(renderer, "The visible hand must use its original imported FPS/boss skinned mesh: " + actor.name);
                Check(renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff,
                    "The original hand renderer must actually be visible: " + actor.name);
                var mesh = renderer.sharedMesh;
                assetPath = AssetDatabase.GetAssetPath(mesh);
                bones = renderer.bones; bind = mesh.bindposes; vertices = mesh.vertices; weights = mesh.boneWeights;
                var matching = Enumerable.Range(0, vertices.Length).Where(i =>
                {
                    var w = weights[i];
                    return Influence(w.boneIndex0, w.weight0) + Influence(w.boneIndex1, w.weight1) +
                        Influence(w.boneIndex2, w.weight2) + Influence(w.boneIndex3, w.weight3) > .65f;
                }).ToArray();
                Check(matching.Length > 8, "Original hand has no supported skinned vertices.");
                indices = matching.Where((_, index) => index % Mathf.Max(1, matching.Length / 24) == 0).Take(24).ToArray();
                rest = indices.Select(Skin).ToArray();

                float Influence(int bone, float weight) => bone < bones.Length && bones[bone] && bones[bone].IsChildOf(hand) ? weight : 0;
            }

            private Vector3 Skin(int index)
            {
                var w = weights[index]; var point = Vector3.zero;
                Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1);
                Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                return point;
                void Add(int bone, float weight)
                {
                    if (weight > 0 && bones[bone]) point += (bones[bone].localToWorldMatrix * bind[bone]).MultiplyPoint3x4(vertices[index]) * weight;
                }
            }

            public void Observe()
            {
                for (int i = 0; i < indices.Length; i++) MaximumMotion = Mathf.Max(MaximumMotion, Vector3.Distance(Skin(indices[i]), rest[i]));
            }
        }
        private static readonly List<float> ObservationTimes = new List<float>();

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
            Check(interaction.automaticBossTurns, "Normal scene must automatically play the boss after player confirmation.");
            Check(!GameObject.Find("COLA_COLA_MainScene").transform.Find("08_Devices/PlayerSleeveContinuation"),
                "The rejected custom sleeve mesh is still present.");
            interaction.automaticBossTurns = false; // Isolate preview/cancellation probes before exercising the normal turn pair.
            Check(driver.playerHand && driver.playerHand.name == "Wrist.R" && driver.playerHand.IsChildOf(driver.playerAnimator.transform) &&
                  driver.bossHand && driver.bossHand.name == "hand.R" && driver.bossHand.IsChildOf(driver.bossAnimator.transform) &&
                  driver.playerHand != driver.bossHand, "Each actor must bind its own original right-hand bone.");
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
            StartObservation(false);
            SessionState.SetBool(Initialized, true);
        }

        private static void StartObservation(bool boss)
        {
            ObservationTimes.Clear();
            keyIndex = 0;
            gripCaptured = false;
            gripSamples = 0;
            maximumGripPositionError = maximumGripAngleError = maximumWristMotion = maximumBottleLift = maximumBottleMotion = 0;
            var actor = boss ? driver.bossAnimator : driver.playerAnimator;
            string wristName = boss ? "hand.R" : "Wrist.R";
            wrist = actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == wristName);
            handMesh = new OriginalHandProbe(actor, wrist, boss);
            var otherActor = boss ? driver.playerAnimator : driver.bossAnimator;
            string otherPrefix = boss ? "Wrist." : "hand.";
            otherHands = otherActor.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == otherPrefix + "R" || t.name == otherPrefix + "L").ToArray();
            Check(otherHands.Length == 2, "The waiting actor must retain both original hand bones.");
            otherHandsAtRest = otherHands.Select(t => t.position).ToArray();
            maximumOtherHandMotion = 0;
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
                        FinishObservation(false);
                        CheckIdleAndBottle();
                        previewPressure = interaction.Model.Pressure;
                        previewLimit = interaction.Model.Limit;
                        previewBottle = interaction.BottleNumber;
                        previewCount = interaction.CommittedActionCount;
                        driver.PreviewBossShake();
                        Check(driver.IsPlaying && driver.PlayingBoss && interaction.IsBusy, "Boss context preview did not start or lock shared input.");
                        Check(!interaction.SelectStrength(1) && !interaction.ConfirmShake(), "Boss preview allowed a competing pressure action.");
                        phase = 1;
                        StartObservation(true);
                        break;
                    case 1:
                        CheckPreviewHasNoRuleEffects();
                        if (driver.IsPlaying) { ObservePerformance(true); return; }
                        if (interaction.IsBusy || Time.unscaledTime - phaseStarted < ColaShakeAnimationFactory.Duration) return;
                        Check(driver.LastPlaybackCompleted, "Boss preview ended without completing its clip.");
                        FinishObservation(true);
                        CheckIdleAndBottle();
                        report.AppendLine("PASS: boss preview locks shared input and changes no pressure, public limit, bottle number, action count or resolution events.");
                        driver.PreviewBossShake();
                        Check(driver.IsPlaying && driver.PlayingBoss, "Boss cancellation probe did not start.");
                        phase = 2;
                        break;
                    case 2:
                        CheckPreviewHasNoRuleEffects();
                        if (driver.PlaybackTime < 1.6f) return;
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
                        interaction.automaticBossTurns = true;
                        interaction.ResetSession(123);
                        interaction.BeginBottle(20);
                        duelResolvedBefore = resolvedEvents;
                        Check(ClickBottleWithStrength(3), "Normal player click failed to begin an automatic turn pair.");
                        phase = 6;
                        break;
                    case 6:
                        Check(interaction.IsBusy, "Normal turn pair unlocked input before the boss started.");
                        if (!interaction.LastActionWasBoss) return;
                        Check(driver.IsPlaying && driver.PlayingBoss && interaction.IsBossTurn && interaction.CommittedActionCount == 2,
                            "Normal gameplay did not actually start the boss rig after the player action.");
                        Check(resolvedEvents == duelResolvedBefore + 1 && !interaction.SelectStrength(1) && !interaction.ConfirmShake(),
                            "Player/boss sequencing or input locking failed.");
                        liveBossStrength = interaction.LastResult.Prediction.Strength;
                        StartObservation(true);
                        phase = 7;
                        break;
                    case 7:
                        if (driver.IsPlaying) { ObservePerformance(true); return; }
                        if (interaction.IsBusy) return;
                        FinishObservation(true);
                        CheckIdleAndBottle();
                        Check(!interaction.IsBossTurn && interaction.CommittedActionCount == 2 && resolvedEvents == duelResolvedBefore + 2 &&
                              interaction.Model.Pressure == 3 + liveBossStrength && interaction.Model.SelectedStrength == 0,
                            "Normal player -> boss -> player flow did not complete exactly two pressure actions.");
                        report.AppendLine("PASS: actual strength/cola raycasts play the original FPS arm, automatically play the original boss hand, and return input to the player; two pressure actions and two results exactly.");
                        interaction.ResetSession(123); interaction.BeginBottle(999);
                        Check(ClickBottleWithStrength(1), "Live boss cancellation sequence did not begin.");
                        phase = 8;
                        break;
                    case 8:
                        if (!driver.PlayingBoss || driver.PlaybackTime < 1.6f) return;
                        cancelResolvedCount = resolvedEvents;
                        interaction.enabled = false;
                        Check(!driver.IsPlaying && !interaction.IsBusy && !interaction.IsBossTurn, "Live boss turn did not cancel with interaction.");
                        CheckIdleAndBottle();
                        interaction.enabled = true;
                        phaseStarted = Time.unscaledTime;
                        phase = 9;
                        break;
                    case 9:
                        if (Time.unscaledTime - phaseStarted < 1) return;
                        Check(!driver.IsPlaying && !interaction.IsBusy && !interaction.IsBossTurn && interaction.CommittedActionCount == 0 && resolvedEvents == cancelResolvedCount,
                            "Cancelled automatic boss action emitted a stale callback or restarted.");
                        report.AppendLine("PASS: disabling interaction during a real automatic boss turn cancels both hand and delayed callbacks and restores normal input.");
                        report.AppendLine("Scope: live Play-mode state/input/bone and CPU skinning checks; no Game-view visual confirmation or preview rendering. Original FPS/boss hand playback and a basic alternating turn loop. Health, rewards, items, shop and boss tactics remain separate.");
                        File.WriteAllText(Output + "/Playmode-validation.txt", report.ToString());
                        SessionState.SetBool(Completed, true);
                        Debug.Log("COLA_SHAKE_ANIMATION_PLAYMODE_PASS\n" + report);
                        phase = 10;
                        EditorApplication.isPlaying = false;
                        break;
                }
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static bool ClickBottleWithStrength(int strength)
        {
            Physics.SyncTransforms();
            var key = interaction.strengthButtons[strength - 1].GetComponent<Collider>();
            return interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(key.bounds.center)) &&
                interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(interaction.bottleCollider.bounds.center));
        }

        private static void ObservePerformance(bool boss)
        {
            Check(driver.PlayingBoss == boss && interaction.IsBusy, "Playback ownership or input lock changed during performance.");
            Check(driver.ActiveHand == wrist, "The bottle is being driven by another actor's hand.");
            var activeActor = boss ? driver.bossAnimator : driver.playerAnimator;
            var waitingActor = boss ? driver.playerAnimator : driver.bossAnimator;
            Check(activeActor.enabled && driver.UsesAnimatorGraph && waitingActor.enabled && waitingActor.GetCurrentAnimatorStateInfo(0).IsName("Idle"),
                "The waiting actor must remain in its own seated Idle.");
            for (int hand = 0; hand < otherHands.Length; hand++)
                maximumOtherHandMotion = Mathf.Max(maximumOtherHandMotion, Vector3.Distance(otherHands[hand].position, otherHandsAtRest[hand]));
            float time = driver.PlaybackTime;
            handMesh.Observe();
            maximumWristMotion = Mathf.Max(maximumWristMotion, Vector3.Distance(wrist.position, wristAtRest));
            maximumBottleLift = Mathf.Max(maximumBottleLift, driver.bottle.localPosition.y - bottleRest.position.y);
            if (time >= ColaShakeAnimationFactory.GripStart + .1f && time <= ColaShakeAnimationFactory.GripEnd - .1f)
            {
                if (!boss) Check(Vector3.Dot(wrist.position - driver.bottle.position, Camera.main.transform.right) > .06f,
                    "Player right hand must hold the right side of the bottle.");
                Check(driver.BottleHeld, "The gripping actor did not own the live hand attachment.");
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
            ObservationTimes.Add(time);
            if (keyIndex < KeyTimes.Length && time >= KeyTimes[keyIndex]) keyIndex++;
        }

        private static void FinishObservation(bool boss)
        {
            string label = boss ? "Boss" : "Player";
            Check(gripCaptured && gripSamples >= 3, label + " performance had no sustained sampled grip.");
            Check(maximumGripPositionError <= .0001f && maximumGripAngleError <= .1f,
                label + " bottle slipped relative to its gripping wrist: " + (maximumGripPositionError * 1000).ToString("F3") + "mm, " + maximumGripAngleError.ToString("F3") + " degrees.");
            Check(maximumWristMotion > .05f && maximumBottleLift > .025f && maximumBottleMotion > .005f,
                label + " hand/bottle performance did not reach, lift and move visibly.");
            Check(ObservationTimes.Count >= 8 && keyIndex == KeyTimes.Length, label + " Play-mode observations did not cover all phases.");
            Check(maximumOtherHandMotion <= .003f, label + " shake displaced the waiting actor's hands.");
            Check(handMesh.MaximumMotion > .03f, "The original visible hand mesh did not move with the rig: " + label);
            report.AppendLine($"PASS: {label}: {ObservationTimes.Count} live Play-mode observations; grip samples {gripSamples}; maximum wrist/bottle drift {maximumGripPositionError * 1000:F3}mm / {maximumGripAngleError:F3} degrees; wrist travel {maximumWristMotion:F3}m, bottle lift {maximumBottleLift:F3}m.");
            report.AppendLine($"PASS: {label} grips through its own {wrist.name}; waiting actor remains in Idle, maximum waiting-hand displacement {maximumOtherHandMotion * 1000:F4}mm.");
            report.AppendLine($"PASS: original skinned hand CPU surface probe from {handMesh.assetPath}; tracked surface movement {handMesh.MaximumMotion:F4}m.");
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
