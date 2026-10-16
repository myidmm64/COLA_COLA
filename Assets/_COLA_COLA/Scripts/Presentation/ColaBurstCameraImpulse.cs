using Unity.Cinemachine;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Transient, bounded shock in Cinemachine's correction channel; never moves the camera anchor.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaBurstCameraImpulse : CinemachineExtension
    {
        private float started = -100, strength;
        public Vector3 CurrentOffset { get; private set; }
        public Vector3 CurrentEuler { get; private set; }
        public bool IsActive => Time.unscaledTime - started < .85f;

        public void Fire(bool boss)
        {
            started = Time.unscaledTime;
            strength = boss ? .42f : 1;
        }

        public void Clear()
        {
            started = -100;
            CurrentOffset = CurrentEuler = Vector3.zero;
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Noise || !Application.IsPlaying(gameObject)) return;
            float t = Time.unscaledTime - started;
            CurrentOffset = CurrentEuler = Vector3.zero;
            if (t < 0 || t >= .85f) return;
            float decay = Mathf.Exp(-t * 7) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.6f, .85f, t)));
            float kick = (1 - Mathf.Exp(-t * 100)) * decay * strength;
            CurrentOffset = new Vector3(Mathf.Sin(t * 83) * .008f, -.011f, -.028f) * kick;
            CurrentEuler = new Vector3(-2.2f + Mathf.Sin(t * 62) * .65f,
                Mathf.Sin(t * 83) * .6f, Mathf.Sin(t * 49 + .4f) * 1.1f) * kick;
            state.PositionCorrection += state.GetCorrectedOrientation() * CurrentOffset;
            state.OrientationCorrection *= Quaternion.Euler(CurrentEuler);
        }
    }
}
