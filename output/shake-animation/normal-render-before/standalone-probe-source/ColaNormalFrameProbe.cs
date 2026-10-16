#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColaCola.Presentation
{
    /// <summary>Opt-in development-player verification against automatically rendered camera frames.</summary>
    [DefaultExecutionOrder(32000)]
    public sealed class ColaNormalFrameProbe : MonoBehaviour
    {
        private ColaShakeAnimator driver;
        private ShakeSelectionInteraction interaction;
        private Camera camera;
        private RenderTexture target;
        private Texture2D pixels;
        private string output;
        private readonly StringBuilder report = new StringBuilder();
        private float started, nextCapture, maxHandMotion;
        private Vector3 restHand;
        private int renderedFrames, capturedFrames, strength = 1;
        private bool running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "-cola-frame-probe");
            if (flag < 0 || flag + 1 >= args.Length) return;
            new GameObject("ColaNormalFrameProbe").AddComponent<ColaNormalFrameProbe>().Initialize(args[flag + 1]);
        }

        private void Initialize(string folder)
        {
            output = folder;
            Directory.CreateDirectory(output);
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            driver = FindAnyObjectByType<ColaShakeAnimator>();
            interaction = FindAnyObjectByType<ShakeSelectionInteraction>();
            camera = Camera.main;
            if (!driver || !interaction || !camera) { Fail("Missing saved player/hand/camera bindings."); return; }
            interaction.automaticBossTurns = false;
            target = new RenderTexture(563, 460, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            camera.aspect = 563f / 460;
            pixels = new Texture2D(563, 460, TextureFormat.RGB24, false);
            report.AppendLine("Standalone development player; normal URP camera rendering, no Camera.Render or editor pose sampling.");
            RenderPipelineManager.endCameraRendering += AfterNormalCamera;
            started = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(output)) return;
            if (Time.realtimeSinceStartup - started > 45) { Fail("Normal-frame verification timed out."); return; }
            if (!running && renderedFrames >= 3)
            {
                interaction.ResetSession(123);
                interaction.BeginBottle(999);
                restHand = driver.playerHand.position;
                maxHandMotion = 0;
                nextCapture = 0;
                capturedFrames = 0;
                Physics.SyncTransforms();
                var key = interaction.strengthButtons[strength - 1].GetComponent<Collider>();
                bool selected = interaction.HandlePointer(camera.WorldToScreenPoint(key.bounds.center));
                bool confirmed = interaction.HandlePointer(camera.WorldToScreenPoint(interaction.bottleCollider.bounds.center));
                if (!selected || !confirmed) { Fail("Actual strength/cola raycasts failed."); return; }
                running = true;
            }
            else if (running && !interaction.IsBusy && !driver.IsPlaying)
            {
                report.AppendLine($"Strength {strength}: {capturedFrames} normal rendered captures; hand travel={maxHandMotion:F5}m.");
                if (maxHandMotion < .1f || capturedFrames < 10) { Fail("The actual rendered-frame hand stayed in Idle."); return; }
                if (strength++ < 3) { running = false; return; }
                report.AppendLine("PASS: all three strengths moved the player's original hand through normal rendered frames.");
                File.WriteAllText(Path.Combine(output, "Normal-frame-validation.txt"), report.ToString());
                Debug.Log("COLA_NORMAL_RENDER_PASS\n" + report);
                Application.Quit(0);
                enabled = false;
            }
        }

        private void AfterNormalCamera(ScriptableRenderContext context, Camera renderedCamera)
        {
            if (renderedCamera != camera || !enabled) return;
            renderedFrames++;
            if (!running || !driver.IsPlaying || driver.PlayingBoss) return;
            maxHandMotion = Mathf.Max(maxHandMotion, Vector3.Distance(driver.playerHand.position, restHand));
            float time = driver.PlaybackTime;
            if (time < nextCapture) return;
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(output, $"Strength{strength}-Frame-{capturedFrames:D3}.png"), pixels.EncodeToPNG());
            report.AppendLine($"FRAME {strength}/{capturedFrames}: time={time:F4}, wrist={driver.playerHand.position:F4}, animatorEnabled={driver.playerAnimator.enabled}.");
            capturedFrames++;
            nextCapture = time + .12f;
        }

        private void Fail(string reason)
        {
            if (!string.IsNullOrEmpty(output)) File.WriteAllText(Path.Combine(output, "Normal-frame-error.txt"), reason + "\n" + report);
            Debug.LogError("COLA_NORMAL_RENDER_FAIL: " + reason);
            Application.Quit(2);
            enabled = false;
        }

        private void OnDestroy()
        {
            RenderPipelineManager.endCameraRendering -= AfterNormalCamera;
            if (camera && camera.targetTexture == target) camera.targetTexture = null;
            if (pixels) Destroy(pixels);
            if (target) { target.Release(); Destroy(target); }
        }
    }
}
#endif
