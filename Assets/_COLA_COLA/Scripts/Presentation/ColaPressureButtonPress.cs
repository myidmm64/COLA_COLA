using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Original FPS left arm points at and taps a pressure key after the idle/other arm animation.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(1200)]
    public sealed class ColaPressureButtonPress : MonoBehaviour
    {
        private ShakeSelectionInteraction owner;
        private Animator actor;
        private Transform upper, lower, wrist, tip, middle, little, button;
        private Transform[] bones;
        private Vector3[] baselinePositions;
        private Quaternion[] baselineRotations;
        private SkinnedMeshRenderer[] skins;
        private bool[] skinUpdates;
        private bool overridden, skinsOwned, returning, retargeting, hasDisplayedPose;
        private float started, fromCurl, currentCurl;
        private Vector3 fromPosition, fromShoulder;
        private Quaternion fromRotation;
        private Vector3 displayedPosition, displayedShoulder;
        private Quaternion displayedRotation;
        private float continuousRoll;
        public bool IsPlaying { get; private set; }
        public bool AnimatesButton => IsPlaying && !returning;

        public static ColaPressureButtonPress EnsureFor(ShakeSelectionInteraction interaction)
        {
            if (!interaction || !Application.IsPlaying(interaction.gameObject)) return null;
            var press = interaction.GetComponent<ColaPressureButtonPress>();
            if (!press) press = interaction.gameObject.AddComponent<ColaPressureButtonPress>();
            press.owner = interaction;
            return press;
        }

        public void PrepareForPlay()
        {
            if (Application.IsPlaying(gameObject)) Bind();
        }

        private bool Bind()
        {
            var next = owner && owner.shakeAnimator ? owner.shakeAnimator.playerAnimator : null;
            if (!next) return false;
            if (actor == next && upper && lower && wrist && tip && bones != null) return true;
            Cancel(); actor = next;
            upper = Find(actor.transform, "UpperArm.L"); lower = Find(actor.transform, "LowArm.L");
            wrist = Find(actor.transform, "Wrist.L");
            tip = Find(actor.transform, "Index4.L_end");
            if (!tip) tip = Find(actor.transform, "Index4.L");
            middle = Find(actor.transform, "Middle2.L"); little = Find(actor.transform, "Little2.L");
            if (!upper || !lower || !wrist || !tip || !lower.IsChildOf(upper) || !wrist.IsChildOf(lower)) return false;
            bones = upper.GetComponentsInChildren<Transform>(true);
            baselinePositions = new Vector3[bones.Length]; baselineRotations = new Quaternion[bones.Length];
            skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true); skinUpdates = new bool[skins.Length];
            return true;
        }

        public void Press(Transform target)
        {
            if (!isActiveAndEnabled || !target || !Bind()) return;
            if (IsPlaying && !returning && button == target) return;
            // Retarget from the currently visible hand, restoring the base rig before any other action captures it.
            retargeting = IsPlaying;
            CaptureFromPose();
            RestoreOverrides();
            if (!skinsOwned)
            {
                for (int i = 0; i < skins.Length; i++)
                    if (skins[i]) { skinUpdates[i] = skins[i].updateWhenOffscreen; skins[i].updateWhenOffscreen = true; }
                skinsOwned = true;
            }
            button = target; started = Time.unscaledTime; returning = false; IsPlaying = true;
            // Selection remains immediate; only the key's visible travel waits for the fingertip.
            owner.SetButtonPressPose(button, 0);
        }

        public void ReturnForAction()
        {
            if (!IsPlaying || !wrist || returning) return;
            CaptureFromPose();
            RestoreOverrides(); ReleaseSkins();
            returning = true; started = Time.unscaledTime;
        }

        private void CaptureFromPose()
        {
            // Update order must not decide whether a rapid click starts from the visible hand or restored idle.
            bool displayed = IsPlaying && hasDisplayedPose;
            fromPosition = displayed ? displayedPosition : wrist.position;
            fromRotation = displayed ? displayedRotation : wrist.rotation;
            fromShoulder = displayed ? displayedShoulder : upper.position;
            fromCurl = currentCurl;
        }

        private void Update() => RestoreOverrides();

        private void LateUpdate()
        {
            if (!IsPlaying) return;
            if (!Application.IsPlaying(gameObject) || !owner || !owner.isActiveAndEnabled || !actor || !wrist || !tip || !button)
            { Cancel(); return; }
            float time = Time.unscaledTime - started;
            if (time >= (returning ? .26f : .82f)) { Cancel(); return; }
            // Animator stays enabled. Only the left subtree is overridden, then restored before the next evaluation.
            for (int i = 0; i < bones.Length; i++)
            { baselinePositions[i] = bones[i].localPosition; baselineRotations[i] = bones[i].localRotation; }
            overridden = true;
            Vector3 restPosition = wrist.position;
            Quaternion restRotation = wrist.rotation;
            Quaternion restingWristRelative = Quaternion.Inverse(lower.rotation) * wrist.rotation;
            var camera = owner.playerCamera;
            Vector3 forward = camera ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float reach = Ease(time / .30f), retreat = Ease((time - .48f) / .34f);
            float amount = returning ? fromCurl * (1 - Ease(time / .26f)) : Mathf.Lerp(fromCurl, 1, reach) * (1 - retreat);
            currentCurl = amount;
            PointFingers(amount);
            Vector3 target;
            Quaternion rotation;
            if (returning)
            {
                float t = Ease(time / .26f);
                target = Vector3.Lerp(fromPosition, restPosition, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .025f);
                rotation = Quaternion.Slerp(fromRotation, restRotation, t);
            }
            else
            {
                Vector3 fingerForward = (tip.position - wrist.position).normalized;
                Vector3 across = middle && little ? (middle.position - little.position).normalized : wrist.right;
                Vector3 back = Vector3.Cross(fingerForward, across).normalized;
                if (back.sqrMagnitude < .1f) back = wrist.up;
                if (Vector3.Dot(back, Vector3.up) < 0) back = -back;
                Quaternion localPalm = Quaternion.Inverse(wrist.rotation) * Quaternion.LookRotation(fingerForward, back);
                Quaternion pressing = Quaternion.LookRotation((forward - Vector3.up * .15f).normalized, Vector3.up) * Quaternion.Inverse(localPalm);
                Vector3 offset = Quaternion.Inverse(wrist.rotation) * (tip.position - wrist.position);
                var hit = button.GetComponent<Collider>();
                var box = hit as BoxCollider;
                Vector3 point = box ? button.TransformPoint(box.center + Vector3.up * (box.size.y * .5f)) + Vector3.up * .0025f :
                    hit ? new Vector3(hit.bounds.center.x, hit.bounds.max.y + .0025f, hit.bounds.center.z) : button.position + Vector3.up * .03f;
                // Always target the released surface: reading the moving key directly would
                // add its depression again every frame and make the fingertip sink through it.
                point += owner.ReleasedButtonOffset(button);
                Vector3 travel = button.parent ? button.parent.TransformVector(Vector3.up * ShakeSelectionInteraction.ButtonPressDistance) :
                    Vector3.up * ShakeSelectionInteraction.ButtonPressDistance;
                Vector3 contact = point - pressing * offset;
                float depression = Ease((time - .335f) / .070f);
                owner.SetButtonPressPose(button, depression);
                if (time < .30f)
                {
                    float t = Ease(time / .30f);
                    Vector3 landing = contact + Vector3.up * .012f;
                    // A button change goes directly to the new contact, never adding another upward arch.
                    target = retargeting ? Vector3.Lerp(fromPosition, landing, t) :
                        Bezier(fromPosition, fromPosition + Vector3.up * .10f,
                            contact - forward * .035f + Vector3.up * .075f, landing, t);
                    rotation = Quaternion.Slerp(fromRotation, pressing, t);
                }
                else if (time < .48f)
                {
                    float touch = Ease((time - .30f) / .035f);
                    float lift = Ease((time - .405f) / .075f);
                    target = contact - travel * depression + Vector3.up * (.012f * (1 - touch + lift));
                    rotation = pressing;
                }
                else
                {
                    float t = Ease((time - .48f) / .34f);
                    target = Bezier(contact - travel + Vector3.up * .012f, contact + Vector3.up * .09f,
                        restPosition + Vector3.up * .055f, restPosition, t);
                    rotation = Quaternion.Slerp(pressing, restRotation, t);
                }
            }
            float upperLength = Vector3.Distance(upper.position, lower.position);
            float lowerLength = Vector3.Distance(lower.position, wrist.position);
            float forearm = lowerLength;
            Vector3 restShoulder = upper.position;
            float table = owner.bottle ? owner.bottle.position.y : target.y - .1f;
            float side = Mathf.Min(.12f, forearm * .28f);
            float elbowDrop = Mathf.Clamp(target.y - table - .055f, 0, forearm * .25f);
            float elbowBack = Mathf.Sqrt(Mathf.Max(0, forearm * forearm - side * side - elbowDrop * elbowDrop));
            Vector3 elbow = target - forward * elbowBack - right * side - Vector3.up * elbowDrop;
            Vector3 armDirection = (target - elbow).normalized;
            Vector3 anchor = target - armDirection * (upperLength + lowerLength);
            anchor.y = Mathf.Max(anchor.y, table + .035f);
            float returnBlend = returning ? Ease(time / .26f) : retreat;
            upper.position = Vector3.Lerp(returning ? fromShoulder : Vector3.Lerp(fromShoulder, anchor, reach), restShoulder, returnBlend);
            ColaArmPose.Straighten(upper, lower, wrist, target, rotation, restingWristRelative, ref continuousRoll);
            displayedPosition = wrist.position; displayedRotation = wrist.rotation;
            displayedShoulder = upper.position;
            hasDisplayedPose = true;
        }

        private void PointFingers(float amount)
        {
            foreach (var bone in bones)
            {
                string name = bone.name;
                if (name == "Index2.L" || name == "Index3.L" || name == "Index4.L")
                    bone.localRotation = Quaternion.Slerp(bone.localRotation, Quaternion.identity, amount * .85f);
                else if (bone.childCount > 0 && (name.StartsWith("Middle") || name.StartsWith("Ring") || name.StartsWith("Little")))
                {
                    Vector3 direction = bone.GetChild(0).position - bone.position;
                    Vector3 axis = Vector3.Cross(direction.normalized, -Vector3.up).normalized;
                    if (axis.sqrMagnitude > .1f) bone.rotation = Quaternion.AngleAxis(22 * amount, axis) * bone.rotation;
                }
            }
        }

        private void RestoreOverrides()
        {
            if (!overridden || bones == null) return;
            for (int i = 0; i < bones.Length; i++) if (bones[i])
            { bones[i].localPosition = baselinePositions[i]; bones[i].localRotation = baselineRotations[i]; }
            overridden = false;
        }
        private void ReleaseSkins()
        {
            if (!skinsOwned) return;
            for (int i = 0; i < skins.Length; i++) if (skins[i]) skins[i].updateWhenOffscreen = skinUpdates[i];
            skinsOwned = false;
        }
        public void Cancel()
        {
            RestoreOverrides(); ReleaseSkins(); IsPlaying = false; currentCurl = 0; button = null;
            hasDisplayedPose = retargeting = returning = false;
            continuousRoll = 0;
            if (owner) owner.RestoreSelectedButtonPose();
        }
        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();
        private static Transform Find(Transform root, string name)
        { foreach (var node in root.GetComponentsInChildren<Transform>(true)) if (node.name == name) return node; return null; }
        private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * t * (10 + t * (-15 + 6 * t)); }
        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        { float s = 1 - t; return s * s * s * a + 3 * s * s * t * b + 3 * s * t * t * c + t * t * t * d; }
    }
}
