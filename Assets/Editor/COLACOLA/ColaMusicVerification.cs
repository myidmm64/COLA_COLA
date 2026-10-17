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
    public static class ColaMusicVerification
    {
        private const string Key = "COLA_COLA.MusicVerification";
        static ColaMusicVerification() { if (SessionState.GetBool(Key, false)) Hook(); }
        public static void Begin()
        {
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Started", false); SessionState.SetBool(Key + ".Passed", false);
            Hook(); EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        private static void Hook()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || Time.frameCount < 4 || SessionState.GetBool(Key + ".Started", false)) return;
            SessionState.SetBool(Key + ".Started", true);
            new GameObject("MusicPlayProbe").AddComponent<ColaMusicPlayProbe>().Begin();
        }
        internal static void Complete(string report, Exception error)
        {
            Directory.CreateDirectory(ProjectVerificationBridge.Root + "/music");
            ProjectVerificationBridge.WriteReport(ProjectVerificationBridge.Root + "/music/Playmode-validation.txt", report + (error == null ? "" : "\nFAIL: " + error));
            SessionState.SetBool(Key + ".Passed", error == null);
            if (error == null) Debug.Log("COLA_MUSIC_PLAYMODE_PASS\n" + report); else Debug.LogException(error);
            EditorApplication.isPlaying = false;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Changed;
            if (!SessionState.GetBool(Key + ".Passed", false)) { if (Application.isBatchMode) EditorApplication.Exit(2); return; }
            if (ProjectVerificationBridge.FullRun) ProjectVerificationBridge.Schedule(ColaOrderSheetVerification.Begin);
            else ProjectVerificationBridge.Schedule(MainSceneTools.Verify);
        }
    }

    public sealed class ColaMusicPlayProbe : MonoBehaviour
    {
        private readonly StringBuilder report = new();
        public void Begin() => StartCoroutine(Guarded(Run()));
        private IEnumerator Guarded(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                bool more; object current = null;
                try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
                catch (Exception exception) { ColaMusicVerification.Complete(report.ToString(), exception); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested); else yield return current;
            }
            ColaMusicVerification.Complete(report.ToString(), null);
        }
        private IEnumerator Run()
        {
            Check(Application.isPlaying, "Music must be verified in actual Play mode.");
            var match = FindAnyObjectByType<ColaDuelController>();
            var interaction = match.interaction;
            var music = match.GetComponent<ColaDuelMusic>();
            Check(music && music.Ambience && music.Tension, "Music was not automatically installed.");
            yield return new WaitForSecondsRealtime(.6f);
            Check(music.Ambience.isPlaying && music.Tension.isPlaying && music.Ambience.loop && music.Tension.loop &&
                music.Ambience.spatialBlend == 0 && music.Tension.spatialBlend == 0, "Both scheduled 2D loop stems must be playing.");
            int phaseDifference = Math.Abs(music.Ambience.timeSamples - music.Tension.timeSamples);
            Check(phaseDifference < 1024, "Music stems are not synchronized: " + phaseDifference);
            foreach (var clip in new[] { music.Ambience.clip, music.Tension.clip })
            {
                Check(clip.channels == 2 && Math.Abs(clip.length - 48) < .01f, "Original stereo cue length changed.");
                var pcm = new float[clip.samples * clip.channels];
                Check(clip.GetData(pcm, 0), "Music PCM could not be read.");
                double energy = 0; float peak = 0;
                foreach (float value in pcm) { energy += value * value; peak = Mathf.Max(peak, Mathf.Abs(value)); }
                double rms = Math.Sqrt(energy / pcm.Length);
                Check(peak > .2f && peak < .9f && rms > .03 && rms < .3, "Music is silent or clipped.");
                Check(Mathf.Abs(pcm[0] - pcm[pcm.Length - 2]) < .05f && Mathf.Abs(pcm[1] - pcm[pcm.Length - 1]) < .05f,
                    "Music loop has a discontinuity at its seam.");
                report.AppendLine($"PASS: {clip.name}, 48s stereo, peak {peak:F3}, RMS {rms:F3}; bounded level and continuous loop seam.");
            }
            interaction.automaticBossTurns = false; interaction.BeginBottle(30, 0);
            yield return new WaitForSecondsRealtime(1.4f);
            float low = music.Intensity;
            interaction.BeginBottle(30, 27);
            yield return new WaitForSecondsRealtime(1.5f);
            Check(low < .05f && music.Intensity > .95f && music.Tension.volume > .15f, "Pressure did not drive the danger mix.");
            var baseSource = music.Ambience; var tensionSource = music.Tension;
            match.RestartRun(123); interaction.automaticBossTurns = false;
            Check(music.Ambience == baseSource && music.Tension == tensionSource && FindObjectsByType<ColaDuelMusic>(FindObjectsSortMode.None).Length == 1,
                "Restart duplicated or replaced the ongoing score.");
            interaction.BeginBottle(1);
            Check(interaction.SelectStrength(1) && interaction.ConfirmShake(), "Music burst probe could not start.");
            bool ducked = false, faded = false;
            float deadline = Time.unscaledTime + 16;
            while (interaction.IsBusy && Time.unscaledTime < deadline)
            {
                if (interaction.burstPresentation.Stage == ColaBurstPresentation.BurstStage.Recovery && music.Duck < .25f) ducked = true;
                if (interaction.burstPresentation.Stage == ColaBurstPresentation.BurstStage.FadeIn) faded = true;
                yield return null;
            }
            Check(!interaction.IsBusy && ducked && faded && music.Ambience.isPlaying && music.Tension.isPlaying,
                "Explosion must duck the score and preserve playback through blackout/reveal.");
            yield return new WaitForSecondsRealtime(1.4f);
            Check(music.Duck > .95f && music.Ambience.volume > .2f, "Music did not recover after fade-in.");
            int nearEnd = music.Ambience.clip.samples - 2205;
            music.Ambience.timeSamples = music.Tension.timeSamples = nearEnd;
            yield return new WaitForSecondsRealtime(.4f);
            Check(music.Ambience.isPlaying && music.Tension.isPlaying && music.Ambience.timeSamples < 22050 &&
                Math.Abs(music.Ambience.timeSamples - music.Tension.timeSamples) < 1024, "Actual loop wrap stopped or desynchronized playback.");
            report.AppendLine("PASS: synchronized scheduled playback and real sample-clock wrap; pressure raises the tension stem; rupture ducks music and fade-in restores it; restart retains one score and two original sources.");
            report.AppendLine("Scope: actual Unity Play-mode source/sample state, PCM levels, loop seam, pressure mix and rupture/fade behavior. Speaker listening is not verified.");
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
