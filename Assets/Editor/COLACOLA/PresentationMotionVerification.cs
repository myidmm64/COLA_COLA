using System;
using System.IO;
using System.Linq;
using System.Text;
using ColaCola.Presentation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace ColaCola.Editor
{
    [InitializeOnLoad]
    public static class PresentationMotionVerification
    {
        private const string Running = "COLA_COLA.PresentationMotionVerification";
        private static ColaDuelController match;
        private static ShakeSelectionInteraction interaction;
        private static PressureGaugeDisplay gauge;
        private static ColaCameraBreathing breathing;
        private static Animator boss;
        private static Transform head, leftHand, rightHand, anchor;
        private static Vector3 bossPosition, anchorPosition, leftRest, rightRest;
        private static Quaternion bossRotation, anchorRotation, headRest;
        private static float started, phaseStarted, cameraTravel, cameraTilt, headMotion, handTravel, previousAngle;
        private static double previousProbability;
        private static int phase, stopFrame;
        private static readonly StringBuilder Report = new StringBuilder();

        static PresentationMotionVerification()
        {
            if (SessionState.GetBool(Running, false)) Hook();
        }

        public static void Begin()
        {
            Directory.CreateDirectory("output/verification/latest/presentation-motion");
            SessionState.SetBool(Running, true);
            SessionState.SetBool(Running + ".Ready", false);
            SessionState.SetBool(Running + ".Complete", false);
            SessionState.SetString(Running + ".Started", DateTime.UtcNow.ToString("O"));
            Hook();
            EditorApplication.isPlaying = true;
        }

        private static void Hook()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= PlayChanged;
            EditorApplication.playModeStateChanged += PlayChanged;
        }

        private static void Initialize()
        {
            match = UnityEngine.Object.FindAnyObjectByType<ColaDuelController>();
            Check(match, "Playable duel is missing.");
            match.RestartRun(123);
            interaction = match.interaction;
            gauge = interaction.gauge;
            breathing = UnityEngine.Object.FindAnyObjectByType<ColaCameraBreathing>();
            Check(breathing && breathing.enabled, "Saved Cinemachine breathing extension is missing.");
            boss = interaction.shakeAnimator.bossAnimator;
            head = Find("spine.004"); leftHand = Find("hand.L"); rightHand = Find("hand.R");
            bossPosition = boss.transform.position; bossRotation = boss.transform.rotation;
            leftRest = leftHand.position; rightRest = rightHand.position; headRest = head.rotation;
            anchor = breathing.transform;
            anchorPosition = anchor.position; anchorRotation = anchor.rotation;
            started = phaseStarted = Time.unscaledTime;
            cameraTravel = cameraTilt = headMotion = handTravel = 0;
            Report.Clear();
            interaction.BeginBottle(20, 8);
            previousAngle = gauge.DisplayedPreviewAngle;
            Check(ClickStrength(1) && gauge.IsPreviewAnimating, "A real key click did not start forecast animation.");
            Check(Mathf.Abs(gauge.DisplayedPreviewAngle - 117) < .1f && gauge.DisplayedProbability < .001,
                "The first forecast snapped to its target instead of starting at current pressure.");
            phase = 0;
            SessionState.SetBool(Running + ".Ready", true);
        }

        private static Transform Find(string name) => boss.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        private static bool ClickStrength(int strength)
        {
            Physics.SyncTransforms();
            var key = interaction.strengthButtons[strength - 1].GetComponent<Collider>();
            return interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(key.bounds.center));
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            try
            {
                Check(DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Running + ".Started", ""), null,
                    System.Globalization.DateTimeStyles.RoundtripKind) < TimeSpan.FromSeconds(110), "Presentation Play verification timed out.");
                if (!EditorApplication.isPlaying || Time.frameCount < 3) return;
                if (!SessionState.GetBool(Running + ".Ready", false)) { Initialize(); return; }
                ObserveMotion();
                switch (phase)
                {
                    case 0:
                        if (Time.unscaledTime - phaseStarted < .12f) return;
                        Check(gauge.DisplayedPreviewAngle < 117 && gauge.DisplayedPreviewAngle > 103.5f &&
                            gauge.DisplayedProbability > 0 && gauge.DisplayedProbability < interaction.Model.Predict(1).BurstProbability,
                            "Forecast arrow/probability did not pass through intermediate values.");
                        CheckDisplayMatchesMeshes();
                        previousAngle = gauge.DisplayedPreviewAngle; previousProbability = gauge.DisplayedProbability;
                        Check(ClickStrength(3), "Retarget to key 3 failed.");
                        Check(Mathf.Abs(gauge.DisplayedPreviewAngle - previousAngle) < .5f && Math.Abs(gauge.DisplayedProbability - previousProbability) < .01,
                            "Retargeting the forecast caused a visible snap.");
                        phaseStarted = Time.unscaledTime; phase = 1;
                        break;
                    case 1:
                        if (Time.unscaledTime - phaseStarted < .10f) return;
                        Check(ClickStrength(2), "Retarget to key 2 failed.");
                        phaseStarted = Time.unscaledTime; phase = 2;
                        break;
                    case 2:
                        if (Time.unscaledTime - phaseStarted < .55f) return;
                        double expected = interaction.Model.Predict(2).BurstProbability;
                        Check(!gauge.IsPreviewAnimating && gauge.DisplayedProbabilityText == PressureGaugeDisplay.FormatProbability(expected) &&
                            gauge.ProbabilityText == PressureGaugeDisplay.FormatProbability(expected) && Math.Abs(gauge.DisplayedProbability - expected) < .000001 &&
                            Mathf.Abs(gauge.DisplayedPreviewAngle - 90) < .001f,
                            "Animated forecast did not settle on the exact rule result.");
                        CheckDisplayMatchesMeshes();
                        Check(ClickStrength(2) && !gauge.IsPreviewAnimating, "Clicking the same settled key restarted animation.");
                        Check(interaction.Model.Pressure == 8 && interaction.CommittedActionCount == 0 && match.PlayerTokens == 0 && match.BossTokens == 0,
                            "Thinking/forecast selection advanced gameplay.");
                        Report.AppendLine($"PASS: real 1 -> 3 -> 2 key input interpolates arrow/probability, retargets continuously, settles at {gauge.DisplayedProbabilityText}/90 degrees and does not consume a turn or reward.");
                        phase = 3;
                        break;
                    case 3:
                        if (Time.unscaledTime - started < 31.2f) return;
                        Check(boss.GetCurrentAnimatorStateInfo(0).IsName("Idle") && boss.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1,
                            "Boss Idle did not complete its automatic loop.");
                        Check(headMotion > 4 && headMotion < 15 && handTravel < .004f,
                            $"Boss Idle motion/support budget failed: head {headMotion:F3}deg, hands {handTravel:F5}m.");
                        Check(cameraTravel > .001f && cameraTravel < .012f && cameraTilt > .04f && cameraTilt < .4f,
                            $"Camera breathing motion budget failed: {cameraTravel:F5}m / {cameraTilt:F3}deg.");
                        Report.AppendLine($"PASS: actual automatic boss Idle completed a 30-second loop; head variation {headMotion:F3}deg, supported-hand travel {handTravel * 1000:F3}mm.");
                        Report.AppendLine($"PASS: Cinemachine output moved within {cameraTravel * 1000:F3}mm / {cameraTilt:F3}deg while its anchor and FOV stayed fixed.");
                        Check(interaction.SelectStrength(2) && interaction.ConfirmShake(), "Animated forecast could not confirm an actual shake.");
                        var random = new System.Random(123);
                        random.Next(interaction.settings.minimumBottleLimit, interaction.settings.maximumBottleLimit + 1);
                        Check(Math.Abs(interaction.LastResult.Roll - random.NextDouble()) < 1e-12,
                            "Animated previews consumed pressure RNG.");
                        Check(!gauge.HasPrediction && !gauge.IsPreviewAnimating && gauge.DisplayedProbabilityText == PressureGaugeDisplay.FormatProbability(interaction.LastResult.Prediction.BurstProbability),
                            "Confirmation did not finalize and clear the animated preview.");
                        phase = 4;
                        break;
                    case 4:
                        if (interaction.IsBusy) return;
                        Check(interaction.CommittedActionCount == 2 && match.PlayerTokens == 2 && match.IsPlayerTurn &&
                            !gauge.HasPrediction && gauge.DisplayedProbabilityText == "--%", "The player/boss turn flow or reset broke.");
                        Report.AppendLine("PASS: confirmation uses the exact target probability and original seeded roll; player and boss resolve once, reward correctly and clear the preview.");
                        breathing.enabled = false;
                        stopFrame = Time.frameCount; phase = 5;
                        break;
                    case 5:
                        if (Time.frameCount < stopFrame + 3) return;
                        Check(Vector3.Distance(Camera.main.transform.position, anchorPosition) < .0001f &&
                            Quaternion.Angle(Camera.main.transform.rotation, anchorRotation) < .03f,
                            "Disabling breathing left a residual camera offset.");
                        breathing.enabled = true;
                        match.RestartRun(123);
                        Check(!gauge.HasPrediction && !gauge.IsPreviewAnimating && gauge.DisplayedProbabilityText == "--%",
                            "Restart retained an in-flight forecast.");
                        Report.AppendLine("PASS: disabling breathing restores the original camera pose; restart clears forecast animation with no delayed callbacks.");
                        Report.AppendLine("Scope: live Play-mode Animator, Cinemachine output, mesh-readout, input and rule-state checks. No preview render, screen capture or Game-view visual confirmation.");
                        ProjectVerificationBridge.WriteReport("output/verification/latest/presentation-motion/Playmode-validation.txt", Report.ToString());
                        Debug.Log("COLA_PRESENTATION_MOTION_PLAYMODE_PASS\n" + Report);
                        SessionState.SetBool(Running + ".Complete", true);
                        EditorApplication.isPlaying = false;
                        phase = 6;
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ProjectVerificationBridge.WriteReport("output/verification/latest/presentation-motion/Playmode-error.txt", exception.ToString());
                SessionState.SetBool(Running, false);
                EditorApplication.update -= Tick;
                if (Application.isBatchMode) EditorApplication.Exit(2);
            }
        }

        private static void ObserveMotion()
        {
            Check(Vector3.Distance(boss.transform.position, bossPosition) < .00001f && Quaternion.Angle(boss.transform.rotation, bossRotation) < .03f,
                "Boss root moved.");
            Check(Vector3.Distance(anchor.position, anchorPosition) < .00001f && Quaternion.Angle(anchor.rotation, anchorRotation) < .03f,
                "Camera breathing moved its anchor cumulatively.");
            Check(Mathf.Abs(Camera.main.fieldOfView - 50) < .001f, "Breathing changed FOV.");
            if (phase >= 4) return;
            headMotion = Mathf.Max(headMotion, Quaternion.Angle(head.rotation, headRest));
            handTravel = Mathf.Max(handTravel, Mathf.Max(Vector3.Distance(leftHand.position, leftRest), Vector3.Distance(rightHand.position, rightRest)));
            cameraTravel = Mathf.Max(cameraTravel, Vector3.Distance(Camera.main.transform.position, anchorPosition));
            cameraTilt = Mathf.Max(cameraTilt, Quaternion.Angle(Camera.main.transform.rotation, anchorRotation));
        }

        private static void CheckDisplayMatchesMeshes()
        {
            Check(Quaternion.Angle(gauge.previewNeedlePivot.localRotation, Quaternion.Euler(0, 0, gauge.DisplayedPreviewAngle)) < .1f,
                "The physical preview arrow does not match the animated value.");
            string text = gauge.DisplayedProbabilityText;
            for (int i = 0; i < gauge.probabilityDigits.Length; i++)
            {
                var slot = gauge.probabilityDigits[i];
                Check(slot.GetComponent<Renderer>().enabled == (i < text.Length), "Probability glyph visibility is incorrect.");
                if (i < text.Length)
                    Check(slot.sharedMesh == gauge.glyphMeshes[PressureGaugeDisplay.GlyphCharacters.IndexOf(text[i])],
                        "The physical probability digit does not match the animated value.");
            }
        }

        private static void PlayChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayChanged;
            if (SessionState.GetBool(Running + ".Complete", false)) ProjectVerificationBridge.Schedule(MainSceneTools.Verify);
            else if (Application.isBatchMode) EditorApplication.Exit(2);
        }

        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
