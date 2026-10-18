using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColaCola.Editor
{
    [InitializeOnLoad]
    public static class ColaShakePlayReview
    {
        internal const string Key = "COLA_COLA.ShakePlayReview";
        internal const string Folder = ProjectVerificationBridge.Root + "/shake-animation";
        private static EditorWindow previousWindow;
        private static EditorWindow gameWindow;
        private static bool wasMaximized;
        static ColaShakePlayReview() { if (SessionState.GetBool(Key, false)) Hook(); }
        public static void Begin()
        {
            // Older reviews did not persist window state across the Play domain reload.
            bool restoreLegacyDockedView = SessionState.GetBool(Key + ".Started", false) &&
                !SessionState.GetBool(Key + ".LayoutTracked", false);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Started", false);
            previousWindow = EditorWindow.focusedWindow;
            SessionState.SetString(Key + ".PreviousWindow", previousWindow ? previousWindow.GetEntityId().ToString() : "");
            Hook();
            EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (type != null)
            {
                gameWindow = EditorWindow.GetWindow(type);
                wasMaximized = !restoreLegacyDockedView && gameWindow.maximized;
                SessionState.SetString(Key + ".GameWindow", gameWindow.GetEntityId().ToString());
                SessionState.SetBool(Key + ".WasMaximized", wasMaximized);
                SessionState.SetBool(Key + ".LayoutTracked", true);
                gameWindow.Show(); gameWindow.maximized = true; gameWindow.Focus();
            }
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
            new GameObject("ActualGameViewShakeReview").AddComponent<ColaShakeReviewProbe>().Begin();
        }
        internal static void Complete(string report, Exception failure)
        {
            ProjectVerificationBridge.WriteReport(Folder + "/Play-review.txt", report + (failure == null ? "" : "\nFAIL: " + failure));
            if (failure == null) Debug.Log("COLA_SHAKE_REVIEW_PASS\n" + report); else Debug.LogException(failure);
            EditorApplication.isPlaying = false;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(Key, false);
            SessionState.SetBool(Key + ".Started", false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Changed;
            gameWindow = previousWindow = null;
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window.GetEntityId().ToString() == SessionState.GetString(Key + ".GameWindow", "")) gameWindow = window;
                if (window.GetEntityId().ToString() == SessionState.GetString(Key + ".PreviousWindow", "")) previousWindow = window;
            }
            wasMaximized = SessionState.GetBool(Key + ".WasMaximized", false);
            if (gameWindow) gameWindow.maximized = wasMaximized;
            if (previousWindow) previousWindow.Focus();
            ProjectVerificationBridge.WriteReport(Folder + "/Editor-restoration.txt",
                $"Game view maximized before={wasMaximized}, after={(gameWindow ? gameWindow.maximized.ToString() : "window unavailable")}. Original focus restored when the window still exists.");
        }
    }

    public sealed class ColaShakeReviewProbe : MonoBehaviour
    {
        private readonly StringBuilder report = new();
        private ColaDuelController match;
        private ColaShakeAnimator driver;
        private ColaBossItemAI ai;
        private bool saved, originalW, originalE, originalAi, originalAuto;
        private float originalThink;
        public void Begin() => StartCoroutine(Guard(Run()));
        private IEnumerator Guard(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                object step = null; bool more;
                try { more = stack.Peek().MoveNext(); if (more) step = stack.Peek().Current; }
                catch (Exception error) { Restore(); ColaShakePlayReview.Complete(report.ToString(), error); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (step is IEnumerator nested) stack.Push(nested); else yield return step;
            }
            Restore(); ColaShakePlayReview.Complete(report.ToString(), null);
        }
        private IEnumerator Run()
        {
            match = FindAnyObjectByType<ColaDuelController>(); Check(match && match.interaction, "No live duel.");
            driver = match.interaction.shakeAnimator; Check(driver, "No bound original-rig driver.");
            ai = match.GetComponent<ColaBossItemAI>();
            originalW = match.GuaranteedBurst; originalE = match.ItemTestMenuEnabled;
            originalAi = ai && ai.automaticItemUse; originalAuto = match.interaction.automaticBossTurns;
            originalThink = match.interaction.bossThinkSeconds; saved = true;
            if (originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            if (originalE) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu);
            Directory.CreateDirectory(ColaShakePlayReview.Folder);
            report.AppendLine("Actual Play mode, original MainScene and Game view; no extra camera or Camera.Render.");
            var camera = match.interaction.playerCamera;
            report.AppendLine($"Camera position={camera.transform.position:F3}, forward={camera.transform.forward:F3}, right={camera.transform.right:F3}.");
            foreach (var actor in new[] { driver.playerAnimator, driver.bossAnimator })
            {
                bool boss = actor == driver.bossAnimator;
                Transform upper = null, elbow = null;
                foreach (var bone in actor.GetComponentsInChildren<Transform>(true))
                {
                    if (bone.name == (boss ? "upper_arm.R" : "UpperArm.R")) upper = bone;
                    if (bone.name == (boss ? "forearm.R" : "LowArm.R")) elbow = bone;
                }
                var hand = boss ? driver.bossHand : driver.playerHand;
                report.AppendLine($"{actor.name}: root={actor.transform.position:F3}, shoulder={upper.position:F3}, elbow={elbow.position:F3}, wrist={hand.position:F3}, lengths={Vector3.Distance(upper.position,elbow.position):F4}/{Vector3.Distance(elbow.position,hand.position):F4}.");
            }
            for (int strength = 1; strength <= 3; strength++)
            {
                Prepare(FindSeed(strength), false);
                Check(match.interaction.SelectStrength(strength) && match.interaction.ConfirmShake(), "Player input rejected.");
                yield return Observe(false, strength, strength == 3);
            }
            for (int strength = 1; strength <= 3; strength++)
            {
                Prepare(FindSeed(strength), true);
                Check(match.interaction.SelectStrength(1) && match.interaction.ConfirmShake(), "Boss lead-in player input rejected.");
                yield return Observe(true, strength, strength == 3);
            }
            Prepare(123, true);
            Check(match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst), "W shared debug path failed.");
            int bursts = match.interaction.burstPresentation.BurstCount;
            Check(match.interaction.SelectStrength(2) && match.interaction.ConfirmShake(), "Burst input rejected.");
            float deadline = Time.unscaledTime + 22;
            bool playerPause = false, bossPause = false;
            while (match.interaction.IsBusy && Time.unscaledTime < deadline)
            {
                if (driver.IsAwaitingResult) { if (driver.PlayingBoss) bossPause = true; else playerPause = true; }
                yield return null;
            }
            Check(!match.interaction.IsBusy && playerPause && bossPause && match.interaction.burstPresentation.BurstCount == bursts + 2,
                "Both actual participants must hold for result then burst and restore input.");
            report.AppendLine("PASS: W shared path: actual player and boss burst after common result holds, blackout and return to player input.");
            match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            Prepare(123, false);
            Check(match.interaction.SelectStrength(3) && match.interaction.ConfirmShake(), "Cancellation input rejected.");
            while (driver.PlaybackTime < 1.6f) yield return null;
            match.RestartRun(123); yield return null;
            Check(!driver.IsPlaying && driver.playerAnimator.enabled && driver.bossAnimator.enabled && !match.interaction.IsBusy,
                "Restart did not release original rig ownership.");
            report.AppendLine("PASS: restart during strokes restores both Animators and shared input.");
            report.AppendLine("Normal sweeps use W off and pressure limit 999 to isolate repeatable animation, not probability balance. AI item use disabled during sweeps. Q/E unnecessary.");
            report.AppendLine("Sparse PNGs use ScreenCapture.CaptureScreenshot at actual Game-view end-of-frame; visual assessment is recorded separately after opening them.");
        }
        private int FindSeed(int desired)
        {
            for (int seed = 1; seed < 10000; seed++)
            {
                if (new System.Random(seed ^ 0x43A1).Next(1, 4) != desired) continue;
                var roll = new System.Random(seed);
                roll.Next(match.interaction.settings.minimumBottleLimit, match.interaction.settings.maximumBottleLimit + 1);
                if (roll.NextDouble() > .2 && roll.NextDouble() > .2) return seed;
            }
            throw new InvalidOperationException("No deterministic review seed.");
        }
        private void Prepare(int seed, bool boss)
        {
            match.RestartRun(seed);
            if (ai) ai.automaticItemUse = false;
            match.interaction.automaticBossTurns = boss;
            match.interaction.bossThinkSeconds = .05f;
            match.interaction.BeginBottle(999);
        }
        private IEnumerator Observe(bool boss, int strength, bool capture)
        {
            float[] times = { .45f, .69f, 1.25f, 1.38f, ColaShakeAnimator.ResultCheckTime, 3.29f, 3.68f };
            string[] labels = { "reach", "contact", "back", "forward", "result", "setdown", "return" };
            int next = 0, samples = 0;
            bool seen = false, held = false;
            Vector3 grip = default, previous = default;
            Quaternion gripRotation = default;
            float drift = 0, rotationDrift = 0, wristStep = 0, startPause = -1, pause = 0, minBottleY = float.PositiveInfinity;
            float tableHeight = driver.bottle.position.y;
            Vector3 minimum = Vector3.one * float.PositiveInfinity, maximum = Vector3.one * float.NegativeInfinity;
            float deadline = Time.unscaledTime + 20;
            while (match.interaction.IsBusy && Time.unscaledTime < deadline)
            {
                if (driver.IsPlaying && driver.PlayingBoss == boss)
                {
                    seen = true;
                    Check(driver.UsesOriginalRig && !driver.ActiveHand.GetComponentInParent<Animator>().enabled, "Original rig must have exactly one pose writer.");
                    if (boss) Check(match.interaction.LastResult.Prediction.Strength == strength, "Unexpected seeded boss strength.");
                    if (driver.BottleHeld)
                    {
                        Vector3 relative = driver.ActiveHand.InverseTransformPoint(driver.bottle.position);
                        var relativeRotation = Quaternion.Inverse(driver.ActiveHand.rotation) * driver.bottle.rotation;
                        if (!held) { grip = relative; gripRotation = relativeRotation; held = true; }
                        drift = Mathf.Max(drift, Vector3.Distance(grip, relative));
                        rotationDrift = Mathf.Max(rotationDrift, Quaternion.Angle(gripRotation, relativeRotation));
                    }
                    if (driver.PlaybackTime >= ColaShakeAnimator.ShakeStartTime && driver.PlaybackTime <= ColaShakeAnimator.ShakeEndTime)
                    {
                        Vector3 point = driver.ActiveHand.position;
                        minimum = Vector3.Min(minimum, point); maximum = Vector3.Max(maximum, point);
                        if (samples > 0) wristStep = Mathf.Max(wristStep, Vector3.Distance(previous, point));
                        previous = point; samples++;
                        minBottleY = Mathf.Min(minBottleY, driver.bottle.position.y);
                    }
                    if (driver.IsAwaitingResult)
                    {
                        if (startPause < 0) startPause = Time.unscaledTime;
                        pause = Time.unscaledTime - startPause;
                    }
                    if (capture && next < times.Length && driver.PlaybackTime >= times[next])
                    {
                        yield return new WaitForEndOfFrame();
                        string path = ColaShakePlayReview.Folder + "/" + (boss ? "Boss" : "Player") + "-" + labels[next] + ".png";
                        ScreenCapture.CaptureScreenshot(path);
                        report.AppendLine($"Game-view capture: {path}; frame {Time.frameCount}; action time {driver.PlaybackTime:F3}; wrist {driver.ActiveHand.position:F3}; bottle {driver.bottle.position:F3}.");
                        next++;
                    }
                }
                yield return null;
            }
            Check(!match.interaction.IsBusy && seen && held && samples > 8, "Actual action did not cover the original hand performance.");
            Check(drift < .001f && rotationDrift < .15f && pause > .35f, "Grip slipped or common result hold was absent.");
            Check(minBottleY > tableHeight + .025f, "Shake brought the bottle too close to the tabletop.");
            Check(driver.playerAnimator.enabled && driver.bossAnimator.enabled, "Idle Animator ownership was not restored.");
            report.AppendLine($"PASS: {(boss ? "Boss" : "Player")} strength {strength}: samples={samples}, wrist span={(maximum-minimum):F3}m, max sampled wrist step={wristStep:F3}m (capture stalls included), grip local drift={drift:F6}, grip rotation drift={rotationDrift:F3}deg, result hold observed={pause:F3}s, minimum bottle origin Y={minBottleY:F3}.");
            if (capture) Check(next == times.Length, "Required actual Game-view poses were not captured.");
        }
        private void Restore()
        {
            if (!saved || !match) return;
            if (match.GuaranteedBurst != originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            if (match.ItemTestMenuEnabled != originalE) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu);
            if (ai) ai.automaticItemUse = originalAi;
            match.interaction.automaticBossTurns = originalAuto;
            match.interaction.bossThinkSeconds = originalThink;
            saved = false;
        }
        private void OnDestroy() => Restore();
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
