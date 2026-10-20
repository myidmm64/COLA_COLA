using Unity.Cinemachine;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Compatibility for saved breathing components. Idle camera motion is disabled.</summary>
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
            if (!breathing) breathing = rig.gameObject.AddComponent<ColaCameraBreathing>();
            breathing.enabled = false;
            return breathing;
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            // Retain saved scene references, but idle breathing no longer contributes camera motion.
            CurrentLocalOffset = CurrentEulerOffset = Vector3.zero;
        }
    }
}
