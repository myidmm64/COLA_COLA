using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
        public static void WriteLiveSnapshot()
        {
            var text = new StringBuilder("BGM state before request-triggered asset refresh.\n");
            if (!EditorApplication.isPlaying) text.AppendLine("NOT TAKEN: Editor was not in Play mode.");
            else
            {
                text.AppendLine($"Actual Play frame: {Time.frameCount}; listener volume {AudioListener.volume}; listener paused {AudioListener.pause}.");
                var music = UnityEngine.Object.FindAnyObjectByType<ColaDuelMusic>();
                if (!music) text.AppendLine("No live ColaDuelMusic component.");
                else foreach (var source in new[] { music.Ambience })
                {
                    if (!source) { text.AppendLine("Missing live music source."); continue; }
                    text.AppendLine($"Source={source.name}; playing={source.isPlaying}; mute={source.mute}; volume={source.volume:F4}; sample={source.timeSamples}");
                    if (source.clip) MusicClipEvidence.Compare(source.clip, AssetDatabase.GetAssetPath(source.clip), text);
                }
            }
            ProjectVerificationBridge.WriteReport(ProjectVerificationBridge.Root + "/music/Before-refresh.txt", text.ToString());
        }
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
            if (ProjectVerificationBridge.MusicOnly) return;
            if (!SessionState.GetBool(Key + ".Passed", false)) { if (Application.isBatchMode) EditorApplication.Exit(2); return; }
            if (ProjectVerificationBridge.FullRun) ProjectVerificationBridge.Schedule(ColaOrderSheetVerification.Begin);
            else ProjectVerificationBridge.Schedule(MainSceneTools.Verify);
        }
    }

    public sealed class ColaMusicPlayProbe : MonoBehaviour
    {
        private readonly StringBuilder report = new();
        private ColaDuelController match;
        private bool savedDebug, originalGuaranteed, originalMenu;
        public void Begin() => StartCoroutine(Guarded(Run()));
        private IEnumerator Guarded(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                bool more; object current = null;
                try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
                catch (Exception exception) { RestoreDebug(); ColaMusicVerification.Complete(report.ToString(), exception); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested); else yield return current;
            }
            RestoreDebug();
            ColaMusicVerification.Complete(report.ToString(), null);
        }
        private void RestoreDebug()
        {
            if (!savedDebug || !match || !Application.isPlaying) return;
            if (match.GuaranteedBurst != originalGuaranteed) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            if (match.ItemTestMenuEnabled != originalMenu) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu);
            savedDebug = false;
        }
        private IEnumerator Run()
        {
            Check(Application.isPlaying, "Music must be verified in actual Play mode.");
            match = FindAnyObjectByType<ColaDuelController>();
            Check(match, "No playable duel found.");
            originalGuaranteed = match.GuaranteedBurst; originalMenu = match.ItemTestMenuEnabled; savedDebug = true;
            if (originalGuaranteed) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            var interaction = match.interaction;
            var music = match.GetComponent<ColaDuelMusic>();
            Check(music && music.Ambience, "Music was not automatically installed.");
            yield return new WaitForSecondsRealtime(.6f);
            Check(!AudioListener.pause && AudioListener.volume > 0, "Unity AudioListener is paused or muted.");
            Check(!music.Ambience.mute, "The BGM source is muted.");
            Check(music.Ambience.isPlaying && music.Ambience.loop && music.Ambience.spatialBlend == 0,
                "The single 2D music loop must be playing.");
            Check(music.Ambience.clip == Resources.Load<AudioClip>(ColaDuelMusic.MusicResource), "Expected Bleak Terminal.");
            foreach (var clip in new[] { music.Ambience.clip })
            {
                Check(clip.length > 1, "The music clip has no duration.");
                Check(MusicClipEvidence.Compare(clip, AssetDatabase.GetAssetPath(clip), report), "Loaded BGM does not reference the current asset.");
                report.AppendLine($"PASS: {clip.name}, {clip.length:F2}s, {clip.channels} channels. Streaming MP3; no PCM/seam comparison claimed.");
            }
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                report.AppendLine($"Listener {listener.name}: enabled={listener.isActiveAndEnabled}");
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            foreach (var typeName in new[] { "UnityEditor.AudioUtil", "UnityEditorInternal.InternalEditorUtility", "UnityEditor.EditorUtility" })
            {
                var type = typeof(EditorApplication).Assembly.GetType(typeName);
                if (type == null) continue;
                foreach (var property in type.GetProperties(flags))
                    if (property.Name.IndexOf("mute", StringComparison.OrdinalIgnoreCase) >= 0 && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
                        report.AppendLine($"Editor {typeName}.{property.Name}={property.GetValue(null)}");
            }
            var output = new float[1024];
            float outputPeak = 0, mixPeak = 0;
            int initialSample = music.Ambience.timeSamples;
            double initialDsp = AudioSettings.dspTime;
            float outputDeadline = Time.realtimeSinceStartup + .6f;
            while (Time.realtimeSinceStartup < outputDeadline)
            {
                yield return null;
                music.Ambience.GetOutputData(output, 0);
                foreach (float value in output) outputPeak = Mathf.Max(outputPeak, Mathf.Abs(value));
                AudioListener.GetOutputData(output, 0);
                foreach (float value in output) mixPeak = Mathf.Max(mixPeak, Mathf.Abs(value));
            }
            report.AppendLine($"Actual base source output peak={outputPeak:F6}; mix peak={mixPeak:F6}; listener volume={AudioListener.volume}; paused={AudioListener.pause}; source volume={music.Ambience.volume}; virtual={music.Ambience.isVirtual}; sample advance={music.Ambience.timeSamples-initialSample}; DSP advance={AudioSettings.dspTime-initialDsp:F3}.");
            Check(outputPeak > .00001f, "BGM source has no digital output; check Unity audio/device mute.");
            interaction.automaticBossTurns = false; interaction.BeginBottle(30, 0);
            yield return new WaitForSecondsRealtime(1.4f);
            float low = music.Intensity;
            interaction.BeginBottle(30, 27);
            yield return new WaitForSecondsRealtime(1.5f);
            Check(low < .05f && music.Intensity > .95f, "Pressure state did not update.");
            var baseSource = music.Ambience;
            match.RestartRun(123); interaction.automaticBossTurns = false;
            Check(music.Ambience == baseSource && FindObjectsByType<ColaDuelMusic>(FindObjectsSortMode.None).Length == 1,
                "Restart duplicated or replaced the ongoing score.");
            interaction.BeginBottle(30);
            Check(match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst) && match.GuaranteedBurst, "W debug path failed to enable guaranteed burst.");
            Check(interaction.SelectStrength(1) && interaction.ConfirmShake(), "Music burst probe could not start.");
            bool ducked = false, faded = false;
            float deadline = Time.unscaledTime + 16;
            while (interaction.IsBusy && Time.unscaledTime < deadline)
            {
                if (interaction.burstPresentation.Stage == ColaBurstPresentation.BurstStage.Recovery && music.Duck < .25f) ducked = true;
                if (interaction.burstPresentation.Stage == ColaBurstPresentation.BurstStage.FadeIn) faded = true;
                yield return null;
            }
            Check(!interaction.IsBusy && ducked && faded && music.Ambience.isPlaying,
                "Explosion must duck the score and preserve playback through blackout/reveal.");
            yield return new WaitForSecondsRealtime(1.4f);
            Check(music.Duck > .95f && music.Ambience.volume > .2f, "Music did not recover after fade-in.");
            int nearEnd = music.Ambience.clip.samples - 2205;
            music.Ambience.timeSamples = nearEnd;
            yield return new WaitForSecondsRealtime(.4f);
            Check(music.Ambience.isPlaying && music.Ambience.timeSamples < music.Ambience.clip.frequency,
                "Actual loop wrap stopped playback.");
            report.AppendLine("PASS: single Bleak Terminal source and real sample-clock wrap; rupture ducks music and fade-in restores it; restart retains the ongoing music source.");
            report.AppendLine("Scope: actual Unity Play-mode asset/source/sample state, digital output and rupture/fade behavior. Speaker listening and the MP3 seam are not verified.");
            report.AppendLine("W used for burst and debug state restored. Q/E unnecessary for audio-only checks.");
        }
        private void OnDestroy() => RestoreDebug();
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }

    internal static class MusicClipEvidence
    {
        internal static bool Compare(AudioClip clip, string path, StringBuilder report)
        {
            if (!clip || !File.Exists(path)) { report.AppendLine("Missing source clip/file: " + path); return false; }
            byte[] file = File.ReadAllBytes(path);
            if (!string.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase))
            {
                string sourceHash; using (var sha = SHA256.Create()) sourceHash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
                report.AppendLine($"Streaming asset={path}; source SHA256={sourceHash}. Asset identity only; compressed PCM comparison not performed.");
                return clip == AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            int channels = 0, rate = 0, bits = 0, format = 0, offset = 0, bytes = 0;
            for (int i = 12; i + 8 <= file.Length;)
            {
                string tag = Encoding.ASCII.GetString(file, i, 4);
                int length = BitConverter.ToInt32(file, i + 4), start = i + 8;
                if (length < 0 || (long)start + length > file.Length) return false;
                if (tag == "fmt " && length >= 16)
                {
                    format = BitConverter.ToUInt16(file, start); channels = BitConverter.ToUInt16(file, start + 2);
                    rate = BitConverter.ToInt32(file, start + 4); bits = BitConverter.ToUInt16(file, start + 14);
                }
                if (tag == "data") { offset = start; bytes = length; }
                i = start + length + (length & 1);
            }
            if (format != 1 || bits != 16 || offset == 0 || channels != clip.channels || rate != clip.frequency || bytes / 2 != clip.samples * clip.channels)
            { report.AppendLine($"Reference format/sample mismatch: {path}; WAV {rate}Hz/{channels}ch/{bytes / 2} values; Play {clip.frequency}Hz/{clip.channels}ch/{clip.samples * clip.channels} values."); return false; }
            var pcm = new float[bytes / 2];
            if (!clip.GetData(pcm, 0)) return false;
            double error = 0, energy = 0;
            for (int i = 0; i < pcm.Length; i++)
            {
                double expected = BitConverter.ToInt16(file, offset + i * 2) / 32768d;
                double delta = pcm[i] - expected; error += delta * delta; energy += expected * expected;
            }
            double relativeError = Math.Sqrt(error / Math.Max(energy, 1e-12));
            string hash; using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
            report.AppendLine($"Clip={clip.name}; asset={path}; source SHA256={hash}; Play-vs-WAV normalized PCM error={relativeError:F6} (allows import compression). ");
            return relativeError < .08;
        }
    }
}
