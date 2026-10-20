using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using ColaCola.Presentation;
using UnityEngine;

namespace ColaCola.Editor
{
    /// <summary>Real Play camera onset/frame measurements, without screenshots that would stall the measured frames.</summary>
    internal sealed class ColaCameraMotionReview
    {
        private readonly StringBuilder report = new();
        private ColaDuelController match;
        private bool saved, originalW, originalE, originalAuto;
        private Camera camera;
        internal IEnumerator Execute()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                object next = null; bool more;
                try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
                catch (Exception error) { Restore(); ColaShakePlayReview.Complete(report.ToString(), error); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (next is IEnumerator nested) stack.Push(nested); else yield return next;
            }
            Restore(); ColaShakePlayReview.Complete(report.ToString(), null);
        }
        private IEnumerator Run()
        {
            Check(Application.isPlaying, "Actual Play required.");
            match = UnityEngine.Object.FindAnyObjectByType<ColaDuelController>(); Check(match && match.interaction, "No duel.");
            originalW = match.GuaranteedBurst; originalE = match.ItemTestMenuEnabled;
            originalAuto = match.interaction.automaticBossTurns; saved = true;
            if (originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            if (originalE) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu);
            match.interaction.automaticBossTurns = false;
            match.interaction.BeginBottle(999);
            camera = match.interaction.playerCamera;
            report.AppendLine("Actual MainScene Play, end-of-frame physical camera transform and wall-clock measurements. No screenshots, preview renders or disk I/O during sampled frames. W off; no purchases; Q/E unnecessary.");
            yield return new WaitForSecondsRealtime(2);
            yield return Sample("Cold button 1", () => Check(match.interaction.SelectStrength(1), "Button rejected."), 1.1f);
            yield return Sample("Warm button 2", () => Check(match.interaction.SelectStrength(2), "Button rejected."), 1.1f);
            yield return Sample("Warm button 3", () => Check(match.interaction.SelectStrength(3), "Button rejected."), 1.1f);
            yield return Sample("Isolated bell camera", () => ColaInteractionCamera.Cue(match.interaction, "Bell", match.interaction.bottle.position), 1.1f);
            yield return Sample("Isolated grip camera", () => ColaInteractionCamera.Cue(match.interaction, "Grip", match.interaction.bottle.position), 1.3f);
            yield return Sample("Actual shake start", () => Check(match.interaction.ConfirmShake(), "Shake rejected."), 5.5f);
            Check(!match.interaction.IsBusy, "Shake did not finish.");
            Vector3 rest = camera.transform.position;
            yield return new WaitForSecondsRealtime(1);
            Check(Vector3.Distance(rest, camera.transform.position) < .0001f, "Camera drift after settling.");
            report.AppendLine("PASS: all actual camera gestures moved and settled; original debug/automatic-turn flags restored. Scope is automated camera/source/frame timing, not speaker listening or continuous Game-view visual review.");
        }
        private IEnumerator Sample(string label, Action trigger, float seconds)
        {
            yield return new WaitForEndOfFrame();
            Vector3 initial = camera.transform.position, previous = initial;
            double started = Time.realtimeSinceStartupAsDouble, previousAt = started;
            var watch = Stopwatch.StartNew(); trigger(); watch.Stop();
            double onset = -1, maxGap = 0; float peak = 0, maxStep = 0;
            int frames = 0, flat = 0; var firstFrames = new StringBuilder();
            while (Time.realtimeSinceStartupAsDouble - started < seconds)
            {
                yield return new WaitForEndOfFrame();
                double now = Time.realtimeSinceStartupAsDouble;
                Vector3 position = camera.transform.position;
                float distance = Vector3.Distance(initial, position), step = Vector3.Distance(previous, position);
                if (onset < 0 && distance > .00001f) onset = now - started;
                if (onset < 0) flat++;
                peak = Mathf.Max(peak, distance); maxStep = Mathf.Max(maxStep, step); maxGap = Math.Max(maxGap, now - previousAt);
                if (frames < 12) firstFrames.Append($" {1000*(now-started):F1}ms:{distance*1000:F4}mm");
                previous = position; previousAt = now; frames++;
            }
            report.AppendLine($"{label}: input CPU={watch.Elapsed.TotalMilliseconds:F3}ms; first movement >0.01mm={onset*1000:F2}ms; initial flat frames={flat}; peak={peak*1000:F4}mm; max frame gap={maxGap*1000:F2}ms; max camera step={maxStep*1000:F4}mm; frames={frames}.");
            report.AppendLine("First output frames:" + firstFrames);
            Check(onset >= 0 && peak < .025f, label + " has missing/excessive camera movement.");
        }
        internal void Restore()
        {
            if (!saved || !match) return;
            if (match.GuaranteedBurst != originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            if (match.ItemTestMenuEnabled != originalE) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu);
            match.interaction.automaticBossTurns = originalAuto;
            saved = false;
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
