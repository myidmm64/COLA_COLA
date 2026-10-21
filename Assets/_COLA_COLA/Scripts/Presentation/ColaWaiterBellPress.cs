using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Original FPS right index finger reaches, presses and returns without stretching the wrist.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(1250)]
    public sealed class ColaWaiterBellPress : MonoBehaviour
    {
        private ShakeSelectionInteraction owner;
        private ColaWaiterBell bell;
        private Animator actor;
        private Transform upper, lower, wrist, tip, middle, little;
        private Transform[] bones;
        private Vector3[] positions;
        private Quaternion[] rotations;
        private SkinnedMeshRenderer[] skins;
        private bool[] skinUpdates;
        private bool overridden, ownsSkins, touched;
        private Vector3 fromPosition, fromShoulder;
        private Quaternion fromRotation;
        private float started, roll;
        private const float PlaybackSpeed = 1.5f;
        public bool IsPlaying { get; private set; }
        public bool IsAnimating { get; private set; }

        public static ColaWaiterBellPress EnsureFor(ShakeSelectionInteraction interaction)
        {
            if (!interaction || !Application.IsPlaying(interaction.gameObject)) return null;
            var press = interaction.GetComponent<ColaWaiterBellPress>();
            if (!press) press = interaction.gameObject.AddComponent<ColaWaiterBellPress>();
            press.owner = interaction;
            return press;
        }

        private bool Bind()
        {
            var next = owner && owner.shakeAnimator ? owner.shakeAnimator.playerAnimator : null;
            if (!next) return false;
            if (actor == next && upper && lower && wrist && tip && bones != null) return true;
            Cancel(); actor = next;
            upper = Find("UpperArm.R"); lower = Find("LowArm.R"); wrist = Find("Wrist.R");
            tip = Find("Index4.R_end"); if (!tip) tip = Find("Index4.R");
            middle = Find("Middle2.R"); little = Find("Little2.R");
            if (!upper || !lower || !wrist || !tip || !lower.IsChildOf(upper) || !wrist.IsChildOf(lower)) return false;
            bones = upper.GetComponentsInChildren<Transform>(true);
            positions = new Vector3[bones.Length]; rotations = new Quaternion[bones.Length];
            skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true); skinUpdates = new bool[skins.Length];
            return true;
        }

        public bool Press(ColaWaiterBell target)
        {
            if (!isActiveAndEnabled || IsPlaying || !target || !Bind()) return false;
            bell = target; touched = false; IsPlaying = true;
            // During a player shake the request waits for the right hand to be free.
            // Payment, sound and delivery happen only at the later fingertip contact.
            return true;
        }

        private void Update() => RestorePose();

        private void LateUpdate()
        {
            if (!IsPlaying) return;
            if (!owner || !owner.isActiveAndEnabled || !owner.InputEnabled || !bell || !bell.isActiveAndEnabled ||
                !owner.items || !owner.items.match || owner.items.match.State != ColaDuelController.DuelState.Playing)
            { Cancel(); return; }
            bool playerShaking = owner.shakeAnimator && owner.shakeAnimator.IsPlaying && !owner.shakeAnimator.PlayingBoss;
            bool bursting = owner.burstPresentation && owner.burstPresentation.Stage != ColaBurstPresentation.BurstStage.Idle;
            if (!IsAnimating)
            {
                var leftPress = owner.GetComponent<ColaPressureButtonPress>();
                if (playerShaking || bursting || owner.items.IsUsingItem || !actor.enabled ||
                    (leftPress && leftPress.IsPlaying) ||
                    (owner.items.match.Progression?.BlocksGameplay ?? false)) return;
                if (!owner.items.CanOrderWaiter()) { Cancel(); return; }
                fromPosition = wrist.position; fromRotation = wrist.rotation; fromShoulder = upper.position;
                started = ColaGameClock.Time; IsAnimating = true;
                for (int i = 0; i < skins.Length; i++) if (skins[i])
                { skinUpdates[i] = skins[i].updateWhenOffscreen; skins[i].updateWhenOffscreen = true; }
                ownsSkins = true;
            }
            if (playerShaking || bursting || !actor.enabled) { Cancel(); return; }
            var sheet = owner.items.GetComponent<ColaOrderSheet>();
            if (sheet && sheet.BlocksGameplay) { Cancel(); return; }
            float time = (ColaGameClock.Time - started) * PlaybackSpeed;
            if (time >= .92f) { Cancel(); return; }
            // Restore in Update, snapshot the freshly evaluated idle, then override only this arm.
            for (int i = 0; i < bones.Length; i++)
            { positions[i] = bones[i].localPosition; rotations[i] = bones[i].localRotation; }
            overridden = true;
            Vector3 restPosition = wrist.position, restShoulder = upper.position;
            Quaternion restRotation = wrist.rotation;
            Quaternion wristRelative = Quaternion.Inverse(lower.rotation) * wrist.rotation;
            float reach = Ease(time / .32f), retreat = Ease((time - .47f) / .45f);
            float curl = reach * (1 - retreat);
            foreach (var bone in bones)
            {
                string name = bone.name;
                if (name == "Index2.R" || name == "Index3.R" || name == "Index4.R")
                    bone.localRotation = Quaternion.Slerp(bone.localRotation, Quaternion.identity, curl * .85f);
                else if (bone.childCount > 0 && (name.StartsWith("Middle") || name.StartsWith("Ring") || name.StartsWith("Little")))
                {
                    var axis = Vector3.Cross((bone.GetChild(0).position - bone.position).normalized, Vector3.down);
                    if (axis.sqrMagnitude > .01f) bone.rotation = Quaternion.AngleAxis(18 * curl, axis.normalized) * bone.rotation;
                }
            }
            Vector3 forward = owner.playerCamera ? Vector3.ProjectOnPlane(owner.playerCamera.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 fingers = (tip.position - wrist.position).normalized;
            Vector3 across = middle && little ? (middle.position - little.position).normalized : wrist.right;
            Vector3 back = Vector3.Cross(fingers, across).normalized;
            if (back.sqrMagnitude < .1f) back = wrist.up;
            if (Vector3.Dot(back, Vector3.up) < 0) back = -back;
            Quaternion palm = Quaternion.Inverse(wrist.rotation) * Quaternion.LookRotation(fingers, back);
            Quaternion pressing = Quaternion.LookRotation((forward - Vector3.up * .12f).normalized, Vector3.up) * Quaternion.Inverse(palm);
            Vector3 offset = Quaternion.Inverse(wrist.rotation) * (tip.position - wrist.position);
            if (!touched && time >= .34f)
            {
                touched = true;
                bell.CompleteHandPress();
            }
            Vector3 contact = bell.PressSurface - pressing * offset;
            Vector3 target;
            Quaternion rotation;
            if (time < .32f)
            {
                target = Bezier(fromPosition, fromPosition + Vector3.up * .065f,
                    contact + Vector3.up * .065f - forward * .015f, contact + Vector3.up * .008f, reach);
                rotation = Quaternion.Slerp(fromRotation, pressing, reach);
            }
            else if (time < .47f)
            {
                float landing = 1 - Ease((time - .32f) / .02f);
                target = contact + Vector3.up * (.008f * landing);
                rotation = pressing;
            }
            else
            {
                target = Bezier(contact, contact + Vector3.up * .065f,
                    restPosition + Vector3.up * .035f, restPosition, retreat);
                rotation = Quaternion.Slerp(pressing, restRotation, retreat);
            }
            float length = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, wrist.position);
            // Move the shoulder anchor instead of crushing/stretched segments at the wrist.
            Vector3 direction = (forward - right * .12f + Vector3.up * .08f).normalized;
            Vector3 anchor = target - direction * length;
            upper.position = Vector3.Lerp(Vector3.Lerp(fromShoulder, anchor, reach), restShoulder, retreat);
            ColaArmPose.Straighten(upper, lower, wrist, target, rotation, wristRelative, ref roll);
        }

        private void RestorePose()
        {
            if (!overridden || bones == null) return;
            for (int i = 0; i < bones.Length; i++) if (bones[i])
            { bones[i].localPosition = positions[i]; bones[i].localRotation = rotations[i]; }
            overridden = false;
        }
        public void Cancel()
        {
            RestorePose();
            if (ownsSkins) for (int i = 0; i < skins.Length; i++) if (skins[i]) skins[i].updateWhenOffscreen = skinUpdates[i];
            ownsSkins = false; IsPlaying = IsAnimating = false; bell = null; roll = 0;
        }
        private Transform Find(string name)
        { foreach (var node in actor.GetComponentsInChildren<Transform>(true)) if (node.name == name) return node; return null; }
        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();
        private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * t * (10 + t * (-15 + 6 * t)); }
        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        { float s = 1 - t; return s * s * s * a + 3 * s * s * t * b + 3 * s * t * t * c + t * t * t * d; }
    }
}
