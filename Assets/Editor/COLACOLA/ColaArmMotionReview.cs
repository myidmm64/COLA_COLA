using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ColaCola.Presentation;
using UnityEngine;

namespace ColaCola.Editor
{
    /// <summary>Focused real Play input/bone/camera checks plus sparse actual Game-view evidence.</summary>
    internal sealed class ColaArmMotionReview
    {
        private const string Folder = ProjectVerificationBridge.Root + "/arm-motion";
        private readonly StringBuilder report = new();
        private ColaDuelController match;
        private ColaBossItemAI ai;
        private bool saved, originalW, originalE, originalAi, originalAuto;
        internal IEnumerator Execute() => Guard(Run());
        private IEnumerator Guard(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                bool more; object next = null;
                try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
                catch (Exception error) { Finish(error); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (next is IEnumerator nested) stack.Push(nested); else yield return next;
            }
            Finish(null);
        }
        private void Finish(Exception error)
        {
            Restore();
            ColaShakePlayReview.Complete(report.ToString(), error);
        }
        private IEnumerator Run()
        {
            Check(Application.isPlaying, "Actual Play mode required.");
            match = UnityEngine.Object.FindAnyObjectByType<ColaDuelController>(); Check(match && match.interaction, "No live duel.");
            ai = match.GetComponent<ColaBossItemAI>();
            originalW = match.GuaranteedBurst; originalE = match.ItemTestMenuEnabled;
            originalAuto = match.interaction.automaticBossTurns; originalAi = ai && ai.automaticItemUse;
            saved = true;
            if (originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            if (originalE) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu);
            match.interaction.automaticBossTurns = false;
            if (ai) ai.automaticItemUse = false;
            match.interaction.BeginBottle(999);
            report.AppendLine("Actual MainScene Play mode. W off; no paid purchases, Q/E unnecessary. Limit 999 isolates motion, not probability balance.");
            yield return new WaitForSecondsRealtime(2);
            var camera = match.interaction.playerCamera;
            Vector3 idlePosition = camera.transform.position;
            Quaternion idleRotation = camera.transform.rotation;
            float idleTravel = 0, idleAngle = 0, until = Time.unscaledTime + 2;
            while (Time.unscaledTime < until)
            {
                yield return new WaitForEndOfFrame();
                idleTravel = Mathf.Max(idleTravel, Vector3.Distance(idlePosition, camera.transform.position));
                idleAngle = Mathf.Max(idleAngle, Quaternion.Angle(idleRotation, camera.transform.rotation));
            }
            report.AppendLine($"Idle camera travel={idleTravel:F7}m; angle={idleAngle:F5}deg.");
            Check(idleTravel < .0001f && idleAngle < .01f, "Idle camera must stay still.");
            var actor = match.interaction.shakeAnimator.playerAnimator;
            var left = new ArmSamples(actor.transform, "L"); var right = new ArmSamples(actor.transform, "R");
            var press = ColaPressureButtonPress.EnsureFor(match.interaction);
            Check(match.interaction.SelectStrength(1), "Left press rejected.");
            float began = Time.unscaledTime;
            float buttonCameraTravel = 0;
            bool captured = false;
            while (Time.unscaledTime - began < .9f)
            {
                yield return new WaitForEndOfFrame(); left.Sample();
                if (press.IsPlaying) left.AssertStraight();
                buttonCameraTravel = Mathf.Max(buttonCameraTravel, Vector3.Distance(idlePosition, camera.transform.position));
                if (!captured && Time.unscaledTime - began >= .37f)
                { Capture("Left-press"); captured = true; }
            }
            report.AppendLine($"Button camera peak travel={buttonCameraTravel:F7}m.");
            Check(buttonCameraTravel > .0003f && buttonCameraTravel < .006f, "Button camera response missing or excessive.");
            var started = typeof(ColaPressureButtonPress).GetField("started", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(match.interaction.SelectStrength(2), "Second press rejected.");
            float initial = (float)started.GetValue(press);
            yield return null;
            Check(match.interaction.SelectStrength(2) && (float)started.GetValue(press) == initial,
                "Same selected button restarted the animation.");
            float maxHandY = left.Hand.position.y;
            for (int i = 0; i < 18; i++)
            {
                Check(match.interaction.SelectStrength(i % 3 + 1), "Rapid selection rejected.");
                until = Time.unscaledTime + .075f;
                while (Time.unscaledTime < until)
                {
                    yield return new WaitForEndOfFrame(); left.Sample();
                    if (press.IsPlaying) left.AssertStraight();
                    maxHandY = Mathf.Max(maxHandY, left.Hand.position.y);
                }
            }
            report.AppendLine($"Rapid selections max left wrist Y={maxHandY:F4}; button top={match.interaction.strengthButtons[0].position.y:F4}.");
            yield return new WaitForSecondsRealtime(1);
            Check(match.interaction.SelectStrength(3), "Right-shake strength rejected.");
            yield return new WaitForSecondsRealtime(.9f);
            Check(match.interaction.ConfirmShake(), "Right shake rejected.");
            var driver = match.interaction.shakeAnimator;
            until = Time.unscaledTime + 12; int shot = 0;
            while (match.interaction.IsBusy && Time.unscaledTime < until)
            {
                yield return new WaitForEndOfFrame(); right.Sample(driver.PlaybackTime);
                if (driver.IsPlaying) right.AssertStraight();
                if (driver.IsPlaying && shot == 0 && driver.PlaybackTime >= 1.33f)
                { Capture("Right-shake"); shot++; }
                if (driver.IsPlaying && shot == 1 && driver.PlaybackTime >= 3.65f)
                { Capture("Right-return"); shot++; }
            }
            Check(!match.interaction.IsBusy && shot == 2, "Right shake did not finish.");
            report.AppendLine(left.Describe("Left")); report.AppendLine(right.Describe("Right"));
            report.AppendLine("PASS: upper arm, elbow and wrist stayed collinear during every sampled active left/right animation frame (flex < 0.15deg). Idle frames are excluded from this requirement.");
            report.AppendLine("Bone metrics and three ScreenCapture screenshots are real Play/Game-view evidence. Images require separate visual inspection. No Camera.Render, preview camera or clip sampling.");
            Check(left.LengthDrift < .0005f && right.LengthDrift < .0005f, "Arm bone lengths changed.");
        }
        private void Capture(string label)
        {
            System.IO.Directory.CreateDirectory(Folder);
            ScreenCapture.CaptureScreenshot(Folder + "/" + label + ".png");
            report.AppendLine($"Game-view {label}, frame={Time.frameCount}.");
            var actor = match.interaction.shakeAnimator.playerAnimator;
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                report.AppendLine($"Skin {skin.name}: mesh={skin.sharedMesh.name}, readable={skin.sharedMesh.isReadable}, vertices={skin.sharedMesh.vertexCount}.");
                var mesh = skin.sharedMesh;
                if (!mesh.isReadable) continue;
                var vertices = mesh.vertices; var weights = mesh.boneWeights; var bind = mesh.bindposes;
                for (int boneIndex = 0; boneIndex < skin.bones.Length; boneIndex++)
                {
                    if (!skin.bones[boneIndex] || skin.bones[boneIndex].name != "UpperArm.R") continue;
                    float end = float.PositiveInfinity;
                    for (int i = 0; i < vertices.Length; i++)
                        if (HasWeight(weights[i], boneIndex)) end = Mathf.Min(end, bind[boneIndex].MultiplyPoint3x4(vertices[i]).y);
                    var baked = new Mesh(); skin.BakeMesh(baked); var live = baked.vertices;
                    int visible = 0, count = 0;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        if (!HasWeight(weights[i], boneIndex) || bind[boneIndex].MultiplyPoint3x4(vertices[i]).y > end + .0001f) continue;
                        var point = match.interaction.playerCamera.WorldToViewportPoint(skin.transform.TransformPoint(live[i]));
                        count++;
                        if (point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1) visible++;
                    }
                    UnityEngine.Object.Destroy(baked);
                    report.AppendLine($"Right sleeve shoulder-end vertices inside Game viewport: {visible}/{count}.");
                    if (count > 0) Check(visible == 0, "The sleeve shoulder cut is visible in Game view.");
                }
            }
        }
        private static bool HasWeight(BoneWeight w, int bone) =>
            (w.boneIndex0 == bone ? w.weight0 : 0) + (w.boneIndex1 == bone ? w.weight1 : 0) +
            (w.boneIndex2 == bone ? w.weight2 : 0) + (w.boneIndex3 == bone ? w.weight3 : 0) > .5f;
        internal void Restore()
        {
            if (!saved || !match) return;
            if (match.GuaranteedBurst != originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            if (match.ItemTestMenuEnabled != originalE) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu);
            if (ai) ai.automaticItemUse = originalAi;
            match.interaction.automaticBossTurns = originalAuto;
            saved = false;
        }
        private static void Check(bool value, string text) { if (!value) throw new InvalidOperationException(text); }
        private sealed class ArmSamples
        {
            private readonly Transform upper, lower;
            internal readonly Transform Hand;
            private readonly float a, b;
            private float minBend = 180, maxBend, maxStep, maxLiveStep, maxStepAt;
            private Quaternion previous;
            private bool sampled;
            internal float LengthDrift { get; private set; }
            internal void AssertStraight()
            {
                float flex = Vector3.Angle(lower.position - upper.position, Hand.position - lower.position);
                Check(flex < .15f, $"FPS arm bent during active animation: {flex:F4}deg.");
            }
            internal ArmSamples(Transform actor, string side)
            {
                foreach (var node in actor.GetComponentsInChildren<Transform>(true))
                {
                    if (node.name == "UpperArm." + side) upper = node;
                    if (node.name == "LowArm." + side) lower = node;
                    if (node.name == "Wrist." + side) Hand = node;
                }
                Check(upper && lower && Hand, "Original FPS bones missing.");
                a = Vector3.Distance(upper.position, lower.position); b = Vector3.Distance(lower.position, Hand.position);
            }
            internal void Sample(float actionTime = -1)
            {
                LengthDrift = Mathf.Max(LengthDrift, Mathf.Abs(Vector3.Distance(upper.position, lower.position) - a),
                    Mathf.Abs(Vector3.Distance(lower.position, Hand.position) - b));
                float bend = Vector3.Angle(lower.position - upper.position, Hand.position - lower.position);
                minBend = Mathf.Min(minBend, bend); maxBend = Mathf.Max(maxBend, bend);
                if (sampled)
                {
                    float step = Quaternion.Angle(previous, lower.rotation);
                    if (step > maxStep) { maxStep = step; maxStepAt = actionTime; }
                    if (Time.unscaledDeltaTime < .05f) maxLiveStep = Mathf.Max(maxLiveStep, step);
                }
                previous = lower.rotation; sampled = true;
            }
            internal string Describe(string side) => $"{side}: lengths={a:F4}/{b:F4}m; drift={LengthDrift:F7}m; elbow flex={minBend:F2}..{maxBend:F2}deg; max forearm step={maxStep:F2}deg at action {maxStepAt:F3}s (capture stalls included); normal-frame step={maxLiveStep:F2}deg.";
        }
    }
}
