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
        public bool IsActive => Time.unscaledTime - started < .78f;

        public void Fire(bool boss)
        {
            started = Time.unscaledTime;
            strength = boss ? .62f : 1;
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
            if (t < 0 || t >= .78f) return;
            float decay = Mathf.Exp(-t * 8.4f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.44f, .78f, t)));
            float kick = (1 - Mathf.Exp(-t * 120)) * decay * strength;
            // One short recoil preserves impact without rapid yaw, roll or lateral vibration.
            CurrentOffset = new Vector3(0, -.013f, -.062f) * kick;
            CurrentEuler = new Vector3(-2.9f, 0, 0) * kick;
            state.PositionCorrection += state.GetCorrectedOrientation() * CurrentOffset;
            state.OrientationCorrection *= Quaternion.Euler(CurrentEuler);
        }
    }
}
