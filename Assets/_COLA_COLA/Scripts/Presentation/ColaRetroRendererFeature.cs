using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ColaCola.Presentation
{
    /// <summary>One color-only RenderGraph pass on the actual duel camera; UI text is composited afterwards.</summary>
    public sealed class ColaRetroRendererFeature : ScriptableRendererFeature
    {
        private Material material;
        private SignalPass pass;
        public override void Create()
        {
            CoreUtils.Destroy(material);
            var shader = Resources.Load<Shader>("COLA_COLA_RetroScreen");
            material = shader ? CoreUtils.CreateEngineMaterial(shader) : null;
            pass = new SignalPass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            if (!Application.isPlaying || camera.cameraType != CameraType.Game || !material ||
                !camera.TryGetComponent<ColaRetroScreen>(out var signal) || !signal.isActiveAndEnabled) return;
            material.SetFloat("_SignalTime", signal.SignalTime);
            material.SetFloat("_GlitchStrength", signal.GlitchStrength);
            material.SetVector("_SignalStyle", new Vector4(0, signal.highlightSoftness, signal.edgeSoftness, 0));
            material.SetVector("_LensStyle", new Vector4(signal.lensDistortion, signal.horizontalGlare, signal.tensionVignette, signal.Tension));
            material.SetVector("_NativePixel", new Vector4(1f / Mathf.Max(1, camera.pixelWidth), 1f / Mathf.Max(1, camera.pixelHeight), 0, 0));
            pass.Setup(material, signal);
            renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); material = null; }

        private sealed class SignalPass : ScriptableRenderPass
        {
            private Material material;
            private ColaRetroScreen signal;
            public void Setup(Material value, ColaRetroScreen controller)
            { material = value; signal = controller; requiresIntermediateTexture = true; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var descriptor = graph.GetTextureDesc(source);
                descriptor.name = "COLA_COLA Lens Finish"; descriptor.clearBuffer = false;
                var destination = graph.CreateTexture(descriptor);
                var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
                graph.AddBlitPass(parameters, passName: "COLA_COLA Lens Finish and Signal Tear");
                resources.cameraColor = destination;
                signal.MarkRendered();
            }
        }
    }
}
