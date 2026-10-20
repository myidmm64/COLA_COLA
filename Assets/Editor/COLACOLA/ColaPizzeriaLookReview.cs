using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ColaCola.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ColaCola.Editor
{
    /// <summary>Real gameplay and naturally scheduled staff, captured from the existing Game view.</summary>
    internal sealed class ColaPizzeriaLookReview
    {
        private static string Folder => ProjectVerificationBridge.Root + (ProjectVerificationBridge.RetroScreenOnly ? "/retro-screen" : "/pizzeria-look");
        private readonly StringBuilder report = new();
        private ColaDuelController match;
        private bool saved, originalW, originalE;

        internal IEnumerator Execute()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                object next = null; bool more;
                try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
                catch (Exception failure) { Restore(); ColaShakePlayReview.Complete(report.ToString(), failure); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (next is IEnumerator nested) stack.Push(nested); else yield return next;
            }
            Restore(); ColaShakePlayReview.Complete(report.ToString(), null);
        }

        private IEnumerator Run()
        {
            Check(Application.isPlaying, "Actual Play mode required.");
            match = UnityEngine.Object.FindAnyObjectByType<ColaDuelController>();
            Check(match && match.interaction, "Live duel missing.");
            originalW = match.GuaranteedBurst; originalE = match.ItemTestMenuEnabled; saved = true;
            if (originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            if (originalE) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu);
            report.AppendLine("Original MainScene in actual Unity Play; original player camera and maximized Game view.");
            report.AppendLine("No user keyboard/mouse, preview camera, Camera.Render or RenderTexture capture.");
            report.AppendLine($"Debug before: W={originalW}, E={originalE}, tokens={match.PlayerTokens}. W/E off during normal gameplay. Q unnecessary: no purchases.");
            yield return new WaitForSecondsRealtime(2);
            Check(match.interaction.SelectStrength(2) && match.interaction.ConfirmShake(), "Actual player shake rejected.");
            float deadline = Time.unscaledTime + 35;
            while ((match.interaction.IsBusy || !match.IsPlayerTurn) && Time.unscaledTime < deadline) yield return null;
            Check(!match.interaction.IsBusy && match.IsPlayerTurn, "Player/boss action did not return control.");
            Check(match.interaction.CommittedActionCount >= 1, "No real action resolved.");
            report.AppendLine($"Real strength-2 input resolved; action count={match.interaction.CommittedActionCount}, player HP={match.PlayerHealth}, boss HP={match.BossHealth}, pressure={match.interaction.Model.Pressure}.");
            yield return new WaitForSecondsRealtime(.8f);
            yield return Capture("Game-view.png");

            if (ProjectVerificationBridge.RetroScreenOnly)
            {
                yield return ReviewRetroScreen();
                yield break;
            }

            var staff = match.GetComponent<ColaPizzeriaStaff>();
            Check(staff, "Staff component missing.");
            Transform worker = null;
            foreach (var node in match.transform.root.GetComponentsInChildren<Transform>(true))
                if (node.name == "Pizzeria / Occasional Staff") { worker = node; break; }
            Check(worker, "Staff model missing.");
            deadline = Time.unscaledTime + 65;
            while ((!worker.gameObject.activeInHierarchy || Mathf.Abs(worker.localPosition.x) > (worker.localPosition.x < 0 ? 2.08f : 2.94f)) && Time.unscaledTime < deadline)
                yield return null;
            Check(worker.gameObject.activeInHierarchy, "Natural staff visit not observed on screen-side path.");
            Vector3 before = worker.position;
            yield return new WaitForSecondsRealtime(.15f);
            Check(Vector3.Distance(before, worker.position) > .01f, "Staff did not walk during the observation.");
            // Capture while walking toward the counter, before turning away to work.
            yield return Capture("Staff-game-view.png");
            bool cap = false, apron = false;
            foreach (var renderer in worker.GetComponentsInChildren<Renderer>(true))
            {
                Check(renderer.sharedMaterial && renderer.sharedMaterial.shader && renderer.sharedMaterial.shader.isSupported,
                    "Staff material/shader unsupported: " + renderer.name);
                if (renderer.name == "Fitted folded paper cap")
                {
                    cap = true;
                    Check(renderer.transform.parent.name == "spine.005", "Cap is not attached to the actual head joint.");
                    Check(renderer.bounds.size.magnitude < .7f && renderer.bounds.center.y > worker.position.y + 1.3f &&
                        renderer.bounds.center.y < worker.position.y + 1.9f, "Cap size/height does not fit the actual character.");
                }
                if (renderer.name == "Tailored bib and apron skirt")
                {
                    apron = true;
                    Check(renderer is SkinnedMeshRenderer cloth && cloth.bones.Length == 2, "Apron does not follow waist/chest bones.");
                    Check(renderer.bounds.size.magnitude < 1.3f && renderer.bounds.center.y > worker.position.y + .5f &&
                        renderer.bounds.center.y < worker.position.y + 1.4f, "Apron size/height does not fit the actual character.");
                }
                report.AppendLine($"Staff part {renderer.name}: parent={renderer.transform.parent.name}, world center={renderer.bounds.center:F3}, size={renderer.bounds.size:F3}.");
            }
            Check(cap && apron, "Fitted cap/apron missing.");
            report.AppendLine("PASS: naturally scheduled worker walked in Play; fitted cap/apron and supported room-light shader present.");
            report.AppendLine("Two ScreenCapture.CaptureScreenshot PNGs saved at actual Game-view end-of-frame. Visual inspection must be recorded separately after opening the images.");
        }

        private IEnumerator ReviewRetroScreen()
        {
            var signal = match.interaction.playerCamera.GetComponent<ColaRetroScreen>();
            Check(signal && signal.isActiveAndEnabled && signal.RenderedFrames > 20, "Retro pass is not rendering the live Game camera.");
            var volume = match.transform.root.GetComponentInChildren<Volume>();
            Check(volume && volume.profile.TryGet<ColorAdjustments>(out var color) && Mathf.Abs(color.postExposure.value - .4f) < .001f &&
                Mathf.Abs(color.contrast.value - 13) < .001f, "Previous color grade was not restored.");
            Check(volume.profile.TryGet<FilmGrain>(out var grain) && Mathf.Abs(grain.intensity.value - TabletopReadability.GrainIntensity) < .001f,
                "Restrained grain was not applied.");
            Check(signal.highlightSoftness <= .03f && signal.edgeSoftness <= .1f && signal.lensDistortion <= .025f &&
                signal.horizontalGlare <= .05f && signal.tensionVignette <= .1f, "Lens finish exceeds the restrained settings.");
            report.AppendLine($"Original lighting/color grade preserved: exposure=.40, contrast=13. Grain={grain.intensity.value:F3}; native-resolution lens pass frames={signal.RenderedFrames}, halation={signal.highlightSoftness:F3}, edge softness={signal.edgeSoftness:F2}, curvature={signal.lensDistortion:F3}, glare={signal.horizontalGlare:F3}, hold vignette={signal.tensionVignette:F3}.");
            float deadline = Time.unscaledTime + 45;
            while (signal.GlitchStrength < .075f && Time.unscaledTime < deadline) yield return null;
            Check(signal.GlitchStrength >= .075f && signal.GlitchStrength <= .101f, "Natural restrained signal glitch did not occur.");
            report.AppendLine($"Natural glitch strength at capture request={signal.GlitchStrength:F3}, pulses={signal.GlitchCount}.");
            yield return Capture("Glitch-game-view.png");
            yield return new WaitForSecondsRealtime(.35f);
            Check(signal.GlitchStrength == 0, "Glitch did not settle.");
            int before = signal.GlitchCount;
            match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            Check(match.GuaranteedBurst && match.interaction.SelectStrength(3) && match.interaction.ConfirmShake(), "W shared burst path rejected.");
            float peak = 0, activeSeconds = 0;
            deadline = Time.unscaledTime + 25;
            while (match.interaction.IsBusy && Time.unscaledTime < deadline)
            {
                peak = Mathf.Max(peak, signal.GlitchStrength);
                if (signal.GlitchStrength > .001f) activeSeconds += Time.unscaledDeltaTime;
                yield return null;
            }
            Check(!match.interaction.IsBusy && signal.GlitchCount > before && peak > .22f && peak <= .301f &&
                activeSeconds < .18f, "Restrained burst signal glitch was not observed/settled.");
            match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            report.AppendLine($"PASS: natural short tear and actual W-triggered burst glitch; burst peak={peak:F3}, sampled active duration={activeSeconds:F3}s. Gameplay returned control; original W/E restored on exit. No camera motion, time-scale freeze, RGB split or new overlay text added.");
        }

        private static IEnumerator Capture(string filename)
        {
            Directory.CreateDirectory(Folder);
            string path = Folder + "/" + filename;
            if (File.Exists(path)) File.Delete(path);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.unscaledTime + 6;
            while ((!File.Exists(path) || new FileInfo(path).Length < 100) && Time.unscaledTime < deadline) yield return null;
            Check(File.Exists(path) && new FileInfo(path).Length > 100, "Actual Game-view screenshot was not saved.");
            yield return null;
        }

        internal void Restore()
        {
            if (!saved || !match) return;
            if (match.GuaranteedBurst != originalW) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleGuaranteedBurst);
            if (match.ItemTestMenuEnabled != originalE) match.ApplyDebugCommand(ColaDuelController.DebugCommand.ToggleFreeItemMenu);
            saved = false;
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
