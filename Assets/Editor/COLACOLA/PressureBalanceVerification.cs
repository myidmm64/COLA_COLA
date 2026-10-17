using System;
using System.IO;
using System.Text;
using ColaCola.Core;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColaCola.Editor
{
    /// <summary>Checks the saved risk balance through actual Play-mode input and resolutions.</summary>
    [InitializeOnLoad]
    public static class PressureBalanceVerification
    {
        private const string Key = "COLA_COLA.PressureBalanceVerification";
        private static ColaDuelController match;
        private static ShakeSelectionInteraction interaction;
        private static PressureGaugeDisplay gauge;
        private static int phase, selected, resolved, bottleBefore;
        private static float phaseStarted;
        private static bool replacementChecked;
        private static readonly StringBuilder Report = new StringBuilder();

        static PressureBalanceVerification() { if (SessionState.GetBool(Key, false)) Hook(); }

        [MenuItem("COLA_COLA/Verify Pressure Choice Balance")]
        public static void Begin()
        {
            Directory.CreateDirectory("output/verification/latest/pressure-balance");
            EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".Ready", false);
            SessionState.SetBool(Key + ".Complete", false);
            SessionState.SetString(Key + ".Started", DateTime.UtcNow.ToString("O"));
            Hook();
            EditorApplication.isPlaying = true;
        }

        private static void Hook()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= PlayChanged; EditorApplication.playModeStateChanged += PlayChanged;
        }

        private static void Initialize()
        {
            match = UnityEngine.Object.FindAnyObjectByType<ColaDuelController>();
            Check(match, "Playable match missing.");
            match.RestartRun(123);
            interaction = match.interaction; gauge = interaction.gauge;
            Check(interaction.settings.minimumBottleLimit == 20 && interaction.settings.maximumBottleLimit == 30 &&
                interaction.settings.probabilityExponent == 1.5f, "Saved settings do not use limits 20..30 and exponent 1.5.");
            Check(interaction.Model.Limit >= 20 && interaction.Model.Limit <= 30, "The real starting bottle ignored the revised limits.");
            Report.Clear();
            for (int limit = 20; limit <= 30; limit++)
            {
                var model = new BottlePressureModel(limit, interaction.settings.probabilityExponent);
                var p1 = model.Predict(1).BurstProbability; var p2 = model.Predict(2).BurstProbability; var p3 = model.Predict(3).BurstProbability;
                Check(p1 < p2 && p2 < p3 && p3 - p1 > Math.Pow(3d / limit, 2) - Math.Pow(1d / limit, 2),
                    "Fresh-bottle choices did not increase separation compared with exponent 2.");
                Report.AppendLine($"PASS: new bottle limit {limit}: strength 1/2/3 = {p1:P2} / {p2:P2} / {p3:P2}.");
                for (int pressure = 0; pressure < limit; pressure++)
                {
                    model.BeginBottle(limit, pressure);
                    double previous = 0;
                    for (int strength = 1; strength <= 3; strength++)
                    {
                        var prediction = model.Predict(strength);
                        double ratio = Math.Min(1d, (double)prediction.PressureAfter / limit);
                        Check(prediction.BurstProbability >= previous && prediction.BurstProbability <= 1 &&
                            Math.Abs(prediction.BurstProbability - Math.Pow(ratio, 1.5)) < 1e-12 &&
                            (ratio == 1 || prediction.BurstProbability > ratio * ratio) &&
                            (prediction.PressureAfter < limit || prediction.BurstProbability == 1), "Probability ordering/limit boundary failed.");
                        previous = prediction.BurstProbability;
                    }
                }
            }
            resolved = 0; replacementChecked = false;
            interaction.ShakeResolved -= OnResolved; interaction.ShakeResolved += OnResolved;
            interaction.BeginBottle(25);
            selected = 1;
            Check(ClickKey(selected), "First physical strength selection failed.");
            phaseStarted = Time.unscaledTime; phase = 0;
            SessionState.SetBool(Key + ".Ready", true);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            try
            {
                Check(DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + ".Started", ""), null,
                    System.Globalization.DateTimeStyles.RoundtripKind) < TimeSpan.FromSeconds(75), "Balance Play verification timed out.");
                if (!EditorApplication.isPlaying || Time.frameCount < 3) return;
                if (!SessionState.GetBool(Key + ".Ready", false)) { Initialize(); return; }
                switch (phase)
                {
                    case 0:
                        if (Time.unscaledTime - phaseStarted < gauge.forecastTransitionSeconds + .15f) return;
                        var prediction = interaction.Model.Predict(selected);
                        Check(!gauge.IsPreviewAnimating && gauge.DisplayedProbabilityText == PressureGaugeDisplay.FormatProbability(prediction.BurstProbability) &&
                            Math.Abs(gauge.DisplayedProbability - prediction.BurstProbability) < .000001 && gauge.PredictedPressure == selected &&
                            interaction.Model.Pressure == 0 && interaction.CommittedActionCount == 0 && resolved == 0,
                            "The real animated gauge did not display the revised risk without advancing play.");
                        if (selected < 3)
                        {
                            selected++;
                            Check(ClickKey(selected), "Physical strength reselection failed.");
                            phaseStarted = Time.unscaledTime;
                            return;
                        }
                        Report.AppendLine("PASS: actual 1/2/3 collider input settles at 0.8% / 2.3% / 4.2% on a limit-25 bottle without spending pressure, tokens or turns.");
                        Check(ClickCola(), "Physical cola confirmation failed.");
                        var random = new System.Random(123); random.Next(20, 31);
                        Check(Math.Abs(interaction.LastResult.Roll - random.NextDouble()) < 1e-12 &&
                            Math.Abs(interaction.LastResult.Prediction.BurstProbability - Math.Pow(3d / 25, 1.5)) < 1e-12,
                            "The actual roll differs from the forecast or preview consumed randomness.");
                        phase = 1;
                        break;
                    case 1:
                        if (interaction.IsBusy) return;
                        Check(resolved == 2 && interaction.CommittedActionCount == 2 && match.IsPlayerTurn && match.PlayerTokens == 3,
                            "The revised balance broke player/boss alternation or safe token rewards.");
                        interaction.BeginBottle(25, 5);
                        Check(Math.Abs(interaction.Model.Predict(1).BurstProbability - Math.Pow(6d / 25, 1.5)) < 1e-12 &&
                            Math.Abs(interaction.Model.Predict(2).BurstProbability - Math.Pow(7d / 25, 1.5)) < 1e-12 &&
                            Math.Abs(interaction.Model.Predict(3).BurstProbability - Math.Pow(8d / 25, 1.5)) < 1e-12,
                            "Mid-bottle choices do not use the expected increased risk.");
                        Report.AppendLine("PASS: at pressure 5 / limit 25, strengths 1/2/3 predict 11.8% / 14.8% / 18.1%; actual safe player/boss actions still resolve and reward normally.");
                        interaction.BeginBottle(20, 19); bottleBefore = interaction.BottleNumber;
                        Check(ClickKey(1) && ClickCola() && interaction.LastResult.Burst && interaction.LastResult.Prediction.BurstProbability == 1,
                            "Reaching the public limit must still guarantee a burst.");
                        phase = 2;
                        break;
                    case 2:
                        if (interaction.IsBusy) return;
                        Check(replacementChecked && resolved == 4 && match.PlayerHealth == 2 && match.PlayerTokens == 8,
                            "Burst damage, exactly-five-token reward or next-bottle replacement failed.");
                        Report.AppendLine("PASS: limit reached -> self damage 1 -> exactly 5 total tokens -> replacement in range 20..30 after burst presentation -> boss turn -> player input.");
                        Report.AppendLine("Scope: live Play-mode rules, physics input, animated readout, turn and reward checks. No preview renders or claim of subjective playtest balance.");
                        ProjectVerificationBridge.WriteReport("output/verification/latest/pressure-balance/Playmode-validation.txt", Report.ToString());
                        Debug.Log("COLA_PRESSURE_BALANCE_PLAYMODE_PASS\n" + Report);
                        interaction.ShakeResolved -= OnResolved;
                        SessionState.SetBool(Key + ".Complete", true);
                        EditorApplication.isPlaying = false;
                        phase = 3;
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ProjectVerificationBridge.WriteReport("output/verification/latest/pressure-balance/Playmode-error.txt", exception.ToString());
                SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
                if (Application.isBatchMode) EditorApplication.Exit(2);
            }
        }

        private static void OnResolved(ShakeResult result)
        {
            resolved++;
            if (phase == 2 && !interaction.LastActionWasBoss)
            {
                Check(result.Burst && interaction.BottleNumber == bottleBefore + 1 && interaction.Model.Pressure == 0 &&
                    interaction.Model.Limit >= 20 && interaction.Model.Limit <= 30, "Generated replacement did not use the new limits.");
                replacementChecked = true;
            }
        }

        private static bool ClickKey(int strength)
        {
            Physics.SyncTransforms();
            return interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(interaction.strengthButtons[strength - 1].GetComponent<Collider>().bounds.center));
        }
        private static bool ClickCola() => interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(interaction.bottleCollider.bounds.center));

        private static void PlayChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayChanged;
            if (SessionState.GetBool(Key + ".Complete", false)) ProjectVerificationBridge.Schedule(MainSceneTools.Verify);
            else if (Application.isBatchMode) EditorApplication.Exit(2);
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
