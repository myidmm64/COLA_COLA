using Unity.Cinemachine;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Small action-led camera gestures in Cinemachine's correction channel, independent of outcome RNG.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaInteractionCamera : CinemachineExtension
    {
        [Range(0, 1.5f)] public float reactionAmount = 1;
        public Vector3 CurrentLocalOffset { get; private set; }
        public Vector3 CurrentEulerOffset { get; private set; }
        private ShakeSelectionInteraction owner;
        // One live envelope per action family. Rapid actions never evict or restart an active envelope.
        private readonly Gesture[] gestures = new Gesture[6];
        private Vector3 filteredPosition, filteredEuler, positionVelocity, eulerVelocity;
        private int filteredFrame = -1;
        private float lastCueAt = -10;
        private string lastCue;

        private struct Gesture
        {
            public float started, duration;
            public Vector3 position, euler;
        }

        public static ColaInteractionCamera EnsureFor(ShakeSelectionInteraction interaction)
        {
            if (!interaction || !Application.IsPlaying(interaction.gameObject)) return null;
            var rig = interaction.transform.root.Find("07_Cameras/CM_SeatedDuel");
            if (!rig) return null;
            var motion = rig.GetComponent<ColaInteractionCamera>();
            if (!motion) motion = rig.gameObject.AddComponent<ColaInteractionCamera>();
            motion.owner = interaction;
            return motion;
        }

        public static void Cue(ShakeSelectionInteraction interaction, string cue, Vector3 point, bool bossAction = false)
        {
            if (bossAction || cue == "Turn") return;
            var motion = EnsureFor(interaction);
            if (motion && motion.isActiveAndEnabled) motion.React(cue, point);
        }

        public static void ResetFor(ShakeSelectionInteraction interaction)
        {
            if (!interaction) return;
            var rig = interaction.transform.root.Find("07_Cameras/CM_SeatedDuel");
            var motion = rig ? rig.GetComponent<ColaInteractionCamera>() : null;
            if (motion) motion.Clear();
        }

        private void React(string cue, Vector3 point)
        {
            float now = Time.unscaledTime;
            if (cue == lastCue && now - lastCueAt < (cue == "Button" ? .065f : .18f)) return;
            Vector3 local = owner.playerCamera ? owner.playerCamera.transform.InverseTransformPoint(point) : Vector3.forward;
            float side = Mathf.Clamp(local.x / Mathf.Max(.3f, Mathf.Abs(local.z)), -1, 1);
            Vector3 position = Vector3.zero, euler = Vector3.zero;
            float duration;
            switch (cue)
            {
                case "Button":
                    int strength = owner.Model != null ? Mathf.Clamp(owner.Model.SelectedStrength, 1, 3) : 1;
                    duration = .48f;
                    position = new Vector3(0, -.00045f, .0011f + strength * .00025f);
                    euler = new Vector3(.045f + strength * .015f, 0, 0); break;
                case "Bell":
                    duration = .55f; position = new Vector3(side * .001f, -.001f, .004f);
                    euler = new Vector3(.12f, side * .25f, 0); break;
                case "ItemReach":
                    duration = .80f; position = new Vector3(side * .001f, 0, .004f);
                    euler = new Vector3(0, side * .30f, 0); break;
                case "Grip":
                    duration = .65f; position = new Vector3(0, .001f, .005f);
                    euler = new Vector3(-.12f, 0, 0); break;
                case "SetDown":
                    duration = .50f; position = new Vector3(0, -.0015f, -.002f);
                    euler = new Vector3(.14f, 0, 0); break;
                case "ShakeStart":
                    duration = .72f; position = new Vector3(0, 0, .003f); break;
                case "Safe":
                    duration = .70f; position = new Vector3(0, -.001f, -.003f);
                    euler = new Vector3(.08f, 0, 0); break;
                case "DeliverySlide":
                    duration = .55f; position = new Vector3(0, 0, .0015f);
                    euler = new Vector3(0, side * .10f, 0); break;
                case "DeliveryLand":
                case "DeliveryLandGlass":
                    duration = .35f; position = new Vector3(0, -.0008f, 0); break;
                case "NewBottle":
                    duration = .70f; position = new Vector3(0, 0, .003f);
                    euler = new Vector3(.08f, 0, 0); break;
                case "MenuOpen":
                case "MenuClose":
                    duration = .50f; position = new Vector3(0, 0, cue == "MenuOpen" ? -.002f : .002f); break;
                case "Cart":
                case "Blocked":
                    return;
                default:
                    if (!cue.StartsWith("Item_", System.StringComparison.Ordinal)) return;
                    duration = .40f; position = new Vector3(0, -.0008f, -.001f); break;
            }
            lastCue = cue; lastCueAt = now;
            int slot = Family(cue);
            var previous = gestures[slot];
            if (previous.duration > 0 && now < previous.started + previous.duration) return;
            gestures[slot] = new Gesture { started = now, duration = duration, position = position, euler = euler };
        }

        private static int Family(string cue)
        {
            switch (cue)
            {
                case "Button": return 0;
                case "Grip": case "SetDown": case "ShakeStart": case "Safe": case "NewBottle": return 1;
                case "Bell": return 2;
                case "DeliverySlide": case "DeliveryLand": case "DeliveryLandGlass": return 3;
                case "MenuOpen": case "MenuClose": return 4;
                default: return 5;
            }
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Noise || !Application.IsPlaying(gameObject) || !owner) return;
            Vector3 position = Vector3.zero, euler = Vector3.zero;
            float shakeWeight = 0;
            float now = Time.unscaledTime;
            for (int i = 0; i < gestures.Length; i++)
            {
                var gesture = gestures[i];
                if (gesture.duration <= 0) continue;
                float t = (now - gesture.started) / gesture.duration;
                if (t < 0 || t >= 1) continue;
                // SmoothDamp already eases acceleration. A second eased clock here made the start look frozen.
                float envelope = Mathf.Sin(Mathf.PI * t);
                position += gesture.position * envelope;
                euler += gesture.euler * envelope;
            }
            var animator = owner.shakeAnimator;
            if (animator && animator.IsPlaying && !animator.PlayingBoss)
            {
                float clock = animator.PlaybackTime;
                float held = Mathf.SmoothStep(0, 1, (clock - ColaShakeAnimator.GripStartTime) / .36f) *
                    (1 - Mathf.SmoothStep(0, 1, (clock - ColaShakeAnimator.GripEndTime) / .30f));
                position.z += held * .004f;
                float strength = Mathf.Clamp(owner.LastResult.Prediction.Strength, 1, 3);
                shakeWeight = Mathf.SmoothStep(0, 1, (clock - ColaShakeAnimator.ShakeStartTime) / .10f) *
                    (1 - Mathf.SmoothStep(0, 1, (clock - (ColaShakeAnimator.ShakeEndTime - .10f)) / .10f));
                // Use the hand's actual signed stroke, so stronger movement stays synchronized rather than vibrating randomly.
                float stroke = animator.ShakeStroke * shakeWeight;
                position.y += stroke * (.0018f + strength * .0015f);
                position.z -= stroke * (.001f + strength * .0015f);
                euler.x -= stroke * (.10f + strength * .08f);
            }
            // Ordinary gestures yield to the authored explosion and blackout, then return without stored drift.
            float gain = reactionAmount * 1.30f;
            var burst = owner.burstPresentation;
            if (burst)
            {
                if (burst.Stage == ColaBurstPresentation.BurstStage.Strain) gain *= .2f;
                else if (burst.Stage == ColaBurstPresentation.BurstStage.FadeIn) gain *= 1 - burst.BlackoutOpacity;
                else if (burst.Stage != ColaBurstPresentation.BurstStage.Idle) gain = 0;
            }
            float positionLimit = Mathf.Lerp(.009f, .016f, shakeWeight);
            float pitchLimit = Mathf.Lerp(.27f, .50f, shakeWeight);
            Vector3 targetPosition = Vector3.ClampMagnitude(position * gain, positionLimit);
            Vector3 targetEuler = new Vector3(Mathf.Clamp(euler.x * gain, -pitchLimit, pitchLimit),
                Mathf.Clamp(euler.y * gain, -.40f, .40f), 0);
            // Carry both displacement and velocity across events, including the end of a shake.
            // Cinemachine can evaluate more than once per frame; integrate this filter only once.
            if (filteredFrame != Time.frameCount)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
                filteredPosition = Vector3.SmoothDamp(filteredPosition, targetPosition, ref positionVelocity, .055f, .10f, dt);
                filteredEuler = Vector3.SmoothDamp(filteredEuler, targetEuler, ref eulerVelocity, .055f, 4f, dt);
                filteredFrame = Time.frameCount;
            }
            CurrentLocalOffset = filteredPosition;
            CurrentEulerOffset = filteredEuler;
            state.PositionCorrection += state.GetCorrectedOrientation() * CurrentLocalOffset;
            state.OrientationCorrection *= Quaternion.Euler(CurrentEulerOffset);
        }

        private void Clear()
        {
            System.Array.Clear(gestures, 0, gestures.Length);
            lastCue = null; lastCueAt = -10;
            filteredPosition = filteredEuler = positionVelocity = eulerVelocity = Vector3.zero;
            filteredFrame = -1;
            CurrentLocalOffset = CurrentEulerOffset = Vector3.zero;
        }
    }
}
