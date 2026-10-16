using Unity.Cinemachine;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Adds a slow, bounded breathing offset through Cinemachine without moving its anchor.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaCameraBreathing : CinemachineExtension
    {
        [Min(2)] public float cycleSeconds = 4.8f;
        [Range(0, .015f)] public float liftMetres = .004f;
        [Range(0, 1)] public float pitchDegrees = .16f;
        public Vector3 CurrentLocalOffset { get; private set; }
        public Vector3 CurrentEulerOffset { get; private set; }

        public static ColaCameraBreathing EnsureFor(Transform root)
        {
            var rig = root ? root.Find("07_Cameras/CM_SeatedDuel") : null;
            if (!rig) return null;
            var breathing = rig.GetComponent<ColaCameraBreathing>();
            return breathing ? breathing : rig.gameObject.AddComponent<ColaCameraBreathing>();
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Noise || !Application.IsPlaying(gameObject)) return;
            float phase = Time.unscaledTime * (2 * Mathf.PI / Mathf.Max(2, cycleSeconds));
            float breath = (1 - Mathf.Cos(phase)) * .5f;
            CurrentLocalOffset = new Vector3(Mathf.Sin(phase) * liftMetres * .18f,
                breath * liftMetres, -breath * liftMetres * .4f);
            CurrentEulerOffset = new Vector3(-breath * pitchDegrees,
                Mathf.Sin(phase) * pitchDegrees * .18f, Mathf.Sin(phase) * pitchDegrees * .25f);
            state.PositionCorrection += state.GetCorrectedOrientation() * CurrentLocalOffset;
            state.OrientationCorrection *= Quaternion.Euler(CurrentEulerOffset);
        }
    }
}
