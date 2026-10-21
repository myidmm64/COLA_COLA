using System;
using System.Collections;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>One continuous, hand-led performance on the original rigs. No shake clips or layered corrections.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(1000)]
    public sealed class ColaShakeAnimator : MonoBehaviour
    {
        public Animator playerAnimator, bossAnimator;
        public Transform playerHand, bossHand, bottle;
        public const float GripStartTime = .72f;
        public const float ShakeStartTime = 1.12f;
        public const float ShakeEndTime = 2.44f;
        public const float ResultCheckTime = 2.60f;
        public const float GripEndTime = 3.30f;
        public const float Duration = 3.95f;
        private const float ReachEnd = .62f, LiftEnd = 1.08f, ReleaseEnd = 3.46f;
        public static float ResultPauseSeconds(float pressureFraction) => Mathf.Lerp(.45f, .75f, Mathf.Clamp01(pressureFraction));

        public bool previewPlayerOnPlay;
        [Range(1, 3)] public int playPreviewStrength = 2;

        private Performance current;
        private int generation;
        private Coroutine previewLoop;
        public bool IsPlaying => current != null;
        public bool LastPlaybackCompleted { get; private set; }
        public bool PlayingBoss => current != null && current.boss;
        internal ColaBossIdleVariation IdleVariation { get; set; }
        public float PlaybackTime => current != null ? current.time : 0;
        public Transform ActiveHand => current != null ? current.right.hand : null;
        public bool BottleHeld => current != null && current.time >= GripStartTime && current.time <= GripEndTime;
        public bool UsesOriginalRig => current != null && current.actor && current.right.hand && current.right.hand.IsChildOf(current.actor.transform);
        public bool IsAwaitingResult => current != null && current.waiting;
        public float ShakeStrokeWeight => current != null ? current.strokeWeight : 0;
        public float ShakeStroke => current != null ? current.stroke : 0;

        private struct Pose
        {
            public Vector3 position, scale;
            public Quaternion rotation;
            public Pose(Transform node) { position = node.localPosition; rotation = node.localRotation; scale = node.localScale; }
            public void Apply(Transform node)
            {
                if (!node) return;
                node.localPosition = position; node.localRotation = rotation; node.localScale = scale;
            }
        }

        private sealed class Arm
        {
            public Transform shoulder, elbow, hand;
            public Vector3 startHand, pole, startPole;
            public Quaternion startRotation;
            private readonly Quaternion wristRelativeRotation;
            private readonly bool preserveFpsWrist;
            private float continuousRoll;
            public float upperLength, lowerLength;
            public Arm(Animator actor, Transform wrist, bool boss, string side)
            {
                shoulder = Find(actor.transform, (boss ? "upper_arm." : "UpperArm.") + side);
                elbow = Find(actor.transform, (boss ? "forearm." : "LowArm.") + side);
                hand = wrist;
                startHand = hand.position; startRotation = hand.rotation; pole = startPole = elbow.position;
                upperLength = Vector3.Distance(shoulder.position, elbow.position);
                lowerLength = Vector3.Distance(elbow.position, hand.position);
                wristRelativeRotation = Quaternion.Inverse(elbow.rotation) * hand.rotation;
                preserveFpsWrist = !boss;
            }
            public void PositionFpsShoulder(Vector3 target, Camera camera, float tableHeight, float amount)
            {
                // Keep the imported forearm length; reach by repositioning the shoulder, not the wrist joint.
                float seatedForearm = lowerLength;
                Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
                float down = Mathf.Min(seatedForearm * .35f, Mathf.Max(0, target.y - (tableHeight + .075f)));
                float side = Mathf.Min(.12f, seatedForearm * .28f);
                float back = Mathf.Sqrt(Mathf.Max(0, seatedForearm * seatedForearm - down * down - side * side));
                Vector3 elbowTarget = target - forward * back - Vector3.up * down + camera.transform.right * side;
                Vector3 armDirection = (target - elbowTarget).normalized;
                Vector3 anchor = target - armDirection * (upperLength + lowerLength);
                shoulder.position = Vector3.Lerp(shoulder.position, anchor, amount);
            }
            public void Move(Vector3 target, Quaternion orientation, Vector3 bendPoint)
            {
                if (preserveFpsWrist)
                {
                    ColaArmPose.Straighten(shoulder, elbow, hand, target, orientation, wristRelativeRotation, ref continuousRoll);
                    return;
                }
                Vector3 start = shoulder.position;
                float a = Vector3.Distance(start, elbow.position), b = Vector3.Distance(elbow.position, hand.position);
                if (a < .0001f || b < .0001f) return;
                Vector3 offset = target - start;
                Vector3 axis = offset.sqrMagnitude > .000001f ? offset.normalized : (hand.position - start).normalized;
                float distance = Mathf.Clamp(offset.magnitude, Mathf.Abs(a - b) + .00001f, a + b - .00001f);
                Vector3 bend = Vector3.ProjectOnPlane(bendPoint - start, axis).normalized;
                if (bend.sqrMagnitude < .5f) bend = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
                if (bend.sqrMagnitude < .5f) bend = Vector3.ProjectOnPlane(Vector3.right, axis).normalized;
                float along = (a * a - b * b + distance * distance) / (2 * distance);
                Vector3 desiredElbow = start + axis * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
                shoulder.rotation = Quaternion.FromToRotation(elbow.position - start, desiredElbow - start) * shoulder.rotation;
                elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, start + axis * distance - elbow.position) * elbow.rotation;
                hand.rotation = orientation;
            }
        }

        private sealed class Finger
        {
            public Transform bone;
            public float curl;
            public bool thumb;
        }

        private sealed class Performance
        {
            public Animator actor;
            public bool boss, animatorEnabled, paused, waiting, revealed, gripBound;
            public float time, pressure, recoil, stroke, strokeWeight, startedAt, idleNormalized, idleRate, holdElapsed;
            public int strength, idleHash;
            public Transform[] nodes;
            public Pose[] rest;
            public Pose actorRest, bottleRest;
            public Transform actorParent, bottleParent, torso, head;
            public Vector3 tableForward, actorRight, touch, contact, held, bottlePosition, pickupPosition, bottleScale;
            public Camera camera;
            public Vector3 localPalmBack, localFingerForward, gripOffset;
            public Quaternion bottleRotation, gripRotation, gripRelativeRotation;
            public Arm right, left;
            public Finger[] fingers;
            public SkinnedMeshRenderer[] skins;
            public bool[] skinUpdates;
        }

        public bool CanPlay(bool boss, int strength)
        {
            if (strength < 1 || strength > 3 || !bottle) return false;
            var actor = boss ? bossAnimator : playerAnimator;
            var other = boss ? playerAnimator : bossAnimator;
            var hand = boss ? bossHand : playerHand;
            if (!actor || actor == other || !hand || !hand.IsChildOf(actor.transform)) return false;
            var upper = Find(actor.transform, boss ? "upper_arm.R" : "UpperArm.R");
            var elbow = Find(actor.transform, boss ? "forearm.R" : "LowArm.R");
            return upper && elbow && elbow.IsChildOf(upper) && hand.IsChildOf(elbow);
        }

        public IEnumerator PlayShake(bool boss, int strength, Func<IEnumerator> burstPerformance = null,
            float pressureFraction = 0, Action safeResult = null)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || IsPlaying || !CanPlay(boss, strength)) yield break;
            int version = ++generation;
            LastPlaybackCompleted = false;
            try
            {
                Begin(boss, strength, pressureFraction);
                while (current != null && version == generation)
                {
                    if (!current.revealed && current.time >= ResultCheckTime)
                    {
                        current.paused = current.waiting = true;
                        float began = ColaGameClock.Time;
                        float duration = ResultPauseSeconds(current.pressure);
                        while (current != null && version == generation && ColaGameClock.Time - began < duration) yield return null;
                        if (current == null || version != generation) yield break;
                        current.waiting = false;
                        current.revealed = true;
                        if (burstPerformance != null) yield return burstPerformance();
                        else safeResult?.Invoke();
                        if (current == null || version != generation) yield break;
                        current.recoil = 0;
                        current.paused = false;
                        if (burstPerformance != null) current.time = GripEndTime;
                    }
                    yield return null;
                }
            }
            finally
            {
                if (current != null && version == generation) CancelAndRestore();
            }
        }

        private void Begin(bool boss, int strength, float pressure)
        {
            // A procedural idle offset must never become the shake's persistent rest pose.
            if (boss && IdleVariation) IdleVariation.RestorePose();
            var actor = boss ? bossAnimator : playerAnimator;
            if (actor.enabled && !actor.isInitialized) actor.Update(0);
            var idle = actor.runtimeAnimatorController ? actor.GetCurrentAnimatorStateInfo(0) : default;
            var p = new Performance
            {
                actor = actor, boss = boss, strength = strength, pressure = Mathf.Clamp01(pressure),
                animatorEnabled = actor.enabled, startedAt = ColaGameClock.Time,
                idleHash = idle.fullPathHash, idleNormalized = idle.normalizedTime,
                idleRate = actor.speed * idle.speed * idle.speedMultiplier / Mathf.Max(.01f, idle.length),
                actorRest = new Pose(actor.transform), actorParent = actor.transform.parent,
                bottleRest = new Pose(bottle), bottleParent = bottle.parent,
                bottlePosition = bottle.position, bottleRotation = bottle.rotation, bottleScale = bottle.localScale,
                nodes = actor.GetComponentsInChildren<Transform>(true),
                skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            };
            p.rest = new Pose[p.nodes.Length];
            for (int i = 0; i < p.nodes.Length; i++) p.rest[i] = new Pose(p.nodes[i]);
            p.skinUpdates = new bool[p.skins.Length];
            for (int i = 0; i < p.skins.Length; i++) p.skinUpdates[i] = p.skins[i].updateWhenOffscreen;
            p.right = new Arm(actor, boss ? bossHand : playerHand, boss, "R");
            var leftHand = Find(actor.transform, boss ? "hand.L" : "Wrist.L");
            if (boss && leftHand && Find(actor.transform, "upper_arm.L") && Find(actor.transform, "forearm.L"))
                p.left = new Arm(actor, leftHand, true, "L");
            p.torso = boss ? Find(actor.transform, "spine.001") : null;
            p.head = boss ? Find(actor.transform, "spine.004") : null;
            var camera = Camera.main;
            p.camera = camera;
            // The imported FPS root/pivot is not the seated player's position or facing direction.
            Vector3 facing = !boss && camera ? camera.transform.forward : bottle.position - actor.transform.position;
            p.tableForward = Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
            if (p.tableForward.sqrMagnitude < .5f) p.tableForward = boss ? Vector3.back : Vector3.forward;
            p.actorRight = Vector3.Cross(Vector3.up, p.tableForward).normalized;
            var middle = Find(actor.transform, boss ? "f_middle.01.R" : "Middle2.R");
            var index = Find(actor.transform, boss ? "f_index.01.R" : "Index2.R");
            var little = Find(actor.transform, boss ? "f_pinky.01.R" : "Little2.R");
            Vector3 forward = middle ? (middle.position - p.right.startHand).normalized : p.right.hand.forward;
            Vector3 across = index && little ? (index.position - little.position).normalized : p.right.hand.right;
            Vector3 back = Vector3.Cross(forward, across).normalized;
            if (back.sqrMagnitude < .5f) back = p.right.hand.up;
            if (Vector3.Dot(back, Vector3.up) < 0) back = -back;
            p.localPalmBack = Quaternion.Inverse(p.right.startRotation) * back;
            p.localFingerForward = Quaternion.Inverse(p.right.startRotation) * forward;
            p.gripRotation = Quaternion.LookRotation(p.tableForward, p.actorRight) *
                Quaternion.Inverse(Quaternion.LookRotation(p.localFingerForward, p.localPalmBack));
            float palm = middle ? Vector3.Distance(middle.position, p.right.startHand) * .78f : .06f;
            p.pickupPosition = p.bottlePosition - p.tableForward * (boss ? 0 : .08f);
            float gripHeight = boss ? .185f : .165f;
            p.touch = p.bottlePosition + Vector3.up * gripHeight + p.actorRight * .092f - p.tableForward * palm;
            p.contact = p.pickupPosition + Vector3.up * gripHeight + p.actorRight * .092f - p.tableForward * palm;
            p.held = p.contact - p.tableForward * (boss ? .18f : .29f) + Vector3.up * (boss ? .13f : .155f) + p.actorRight * .025f;
            p.fingers = Fingers(actor.transform, boss);
            current = p;
            actor.enabled = false; // Exactly one writer owns this rig during the complete action.
            foreach (var skin in p.skins) skin.updateWhenOffscreen = true;
            Apply(p, GripStartTime);
            p.gripOffset = p.right.hand.InverseTransformPoint(bottle.position);
            p.gripRelativeRotation = Quaternion.Inverse(p.right.hand.rotation) * p.bottleRotation;
            p.gripBound = true;
            Apply(p, 0);
        }

        private void LateUpdate()
        {
            var p = current;
            if (p == null) return;
            if (!p.actor || !p.right.hand || !bottle) { CancelAndRestore(); return; }
            try
            {
                if (p.waiting) p.holdElapsed += ColaGameClock.Delta;
                if (!p.paused)
                {
                    float next = Mathf.Min(Duration, p.time + ColaGameClock.Delta);
                    p.time = !p.revealed ? Mathf.Min(next, ResultCheckTime) : next;
                }
                Apply(p, p.time);
                if (p.time >= Duration)
                {
                    LastPlaybackCompleted = true;
                    Restore(p);
                }
            }
            catch (Exception error) { CancelAndRestore(); Debug.LogException(error, this); }
        }

        private void Apply(Performance p, float time)
        {
            for (int i = 0; i < p.nodes.Length; i++) p.rest[i].Apply(p.nodes[i]);
            float reach = Ease(time / ReachEnd);
            float returning = Ease((time - ReleaseEnd) / (Duration - ReleaseEnd));
            float lifting = Ease((time - GripStartTime) / (LiftEnd - GripStartTime));
            float lowering = Ease((time - ResultCheckTime) / (GripEndTime - ResultCheckTime));
            float lift = lifting * (1 - lowering);
            float drag = Ease((time - ReachEnd) / (GripStartTime - ReachEnd));
            Vector3 target;
            Quaternion orientation;
            if (time < ReachEnd)
            {
                target = Bezier(p.right.startHand, p.right.startHand + Vector3.up * .065f,
                    p.touch - p.tableForward * .045f + Vector3.up * .040f, p.touch, reach);
                orientation = Quaternion.Slerp(p.right.startRotation, p.gripRotation, reach);
            }
            else if (time < GripStartTime)
            {
                target = Vector3.Lerp(p.touch, p.contact, drag);
                orientation = p.gripRotation;
            }
            else if (time >= ReleaseEnd)
            {
                target = Bezier(p.touch, p.touch - p.tableForward * .045f + Vector3.up * .045f,
                    p.right.startHand + Vector3.up * .045f, p.right.startHand, returning);
                orientation = Quaternion.Slerp(p.gripRotation, p.right.startRotation, returning);
            }
            else
            {
                target = time < ResultCheckTime ? Vector3.Lerp(p.contact, p.held, lifting) : Vector3.Lerp(p.held, p.touch, lowering);
                target += Vector3.up * (Mathf.Sin(lift * Mathf.PI) * .015f);
                orientation = p.gripRotation;
            }
            // Relax the upright grip into a slight diagonal once the bottle is lifted.
            // Blend back to upright before setting it down so the hand never changes grip.
            Quaternion carriedTilt = Quaternion.AngleAxis(-14f * lift, p.tableForward) *
                Quaternion.AngleAxis(-6f * lift, p.actorRight);
            orientation = carriedTilt * orientation;
            target += p.actorRight * (.018f * lift);
            p.strokeWeight = 0;
            p.stroke = 0;
            if (time >= ShakeStartTime && time <= ShakeEndTime)
            {
                float phase = Mathf.InverseLerp(ShakeStartTime, ShakeEndTime, time);
                float stroke = Stroke(phase, p.strength + 1, out float speed);
                p.stroke = stroke;
                float vertical = p.strength == 1 ? .035f : p.strength == 2 ? .055f : .075f;
                float depth = p.strength == 1 ? .018f : p.strength == 2 ? .032f : .045f;
                float tilt = p.boss ? (p.strength == 1 ? 10 : p.strength == 2 ? 18 : 26) :
                    (p.strength == 1 ? 8 : p.strength == 2 ? 13 : 18);
                float ownerScale = p.boss ? 1.06f : 1;
                // Shake along the tilted bottle's diagonal instead of a world-vertical rail.
                Vector3 strokeDirection = carriedTilt * (Vector3.up * vertical - p.tableForward * depth);
                target += strokeDirection * (stroke * ownerScale);
                orientation = Quaternion.AngleAxis(-stroke * tilt * ownerScale,
                    carriedTilt * p.actorRight) * orientation;
                p.strokeWeight = .30f + .70f * speed;
            }
            // A result hold is a living pose, not a frozen animation frame. The independent
            // clock keeps moving while the choreography waits, then fades into set-down.
            float holdWeight = Ease((time - ShakeEndTime) / (ResultCheckTime - ShakeEndTime)) *
                (1 - Ease((time - ResultCheckTime) / .18f));
            float holdClock = Mathf.Max(0, time - ShakeEndTime) + p.holdElapsed;
            if (holdWeight > 0)
            {
                float settle = Mathf.Sin(holdClock * 18) * Mathf.Exp(-holdClock * 7) * .004f;
                target += (p.actorRight * (Mathf.Sin(holdClock * 4.4f) * .0045f) +
                    Vector3.up * (Mathf.Sin(holdClock * 6.1f) * .003f + settle)) * holdWeight;
                orientation = Quaternion.AngleAxis(Mathf.Sin(holdClock * 5.1f) * 1.6f * holdWeight,
                    carriedTilt * p.actorRight) * orientation;
                orientation = Quaternion.AngleAxis(Mathf.Sin(holdClock * 3.7f) * .9f * holdWeight,
                    p.tableForward) * orientation;
            }
            if (p.recoil > 0)
            {
                target -= p.tableForward * (p.recoil * .065f);
                target += Vector3.up * (p.recoil * .024f);
                orientation = Quaternion.AngleAxis(p.recoil * 16, p.actorRight) * orientation;
            }
            float leaning = reach * (1 - returning) * Mathf.Lerp(1, .48f, lift);
            if (p.torso)
            {
                p.torso.rotation = Quaternion.AngleAxis(38 * leaning + Mathf.Sin(holdClock * 4.4f) * .28f * holdWeight,
                    p.actorRight) * p.torso.rotation;
                p.torso.position += (p.tableForward * .11f + Vector3.up * .015f) * leaning;
                if (p.head) p.head.rotation = Quaternion.AngleAxis(-22 * leaning, p.actorRight) * p.head.rotation;
                if (p.recoil > 0)
                {
                    p.torso.position -= p.tableForward * (.045f * p.recoil);
                    p.torso.rotation = Quaternion.AngleAxis(-9 * p.recoil, p.actorRight) * p.torso.rotation;
                    if (p.head) p.head.rotation = Quaternion.AngleAxis(5 * p.recoil, p.actorRight) * p.head.rotation;
                }
                float shortage = Vector3.Distance(p.right.shoulder.position, target) - (p.right.upperLength + p.right.lowerLength - .008f);
                if (shortage > 0)
                    p.torso.position += Vector3.ClampMagnitude(target - p.right.shoulder.position, Mathf.Min(shortage, .15f)) * reach * (1 - returning);
            }
            if (!p.boss && p.camera) p.right.PositionFpsShoulder(target, p.camera, p.bottlePosition.y, reach * (1 - returning));
            p.right.Move(target, orientation, p.right.pole + p.actorRight * (.045f * leaning));
            if (p.left != null)
                p.left.Move(p.left.startHand + p.tableForward * (.07f * leaning), p.left.startRotation, p.left.pole);
            bottle.localScale = p.bottleScale;
            if (p.gripBound && time >= GripStartTime && time <= GripEndTime)
                bottle.SetPositionAndRotation(p.right.hand.TransformPoint(p.gripOffset), p.right.hand.rotation * p.gripRelativeRotation);
            else bottle.SetPositionAndRotation(time <= GripStartTime ? Vector3.Lerp(p.bottlePosition, p.pickupPosition, drag) : p.bottlePosition, p.bottleRotation);
            float close = Ease((time - .60f) / (GripStartTime - .60f)) * (1 - Ease((time - GripEndTime) / (ReleaseEnd - GripEndTime)));
            CurlFingers(p, close);
        }

        private static float Stroke(float phase, int count, out float speed)
        {
            float from, to, progress;
            if (phase < .10f) { from = 0; to = 1; progress = phase / .10f; }
            else if (phase > .90f) { from = 1; to = 0; progress = (phase - .90f) / .10f; }
            else
            {
                float segment = (phase - .10f) / .80f * (count * 2);
                int index = Mathf.Min(count * 2 - 1, Mathf.FloorToInt(segment));
                progress = segment - index;
                from = index % 2 == 0 ? 1 : -1;
                to = -from;
            }
            progress = Mathf.Clamp01(progress);
            speed = 16 * progress * progress * (1 - progress) * (1 - progress);
            return Mathf.Lerp(from, to, Ease(progress));
        }

        private static Finger[] Fingers(Transform root, bool boss)
        {
            var joints = new System.Collections.Generic.List<Finger>();
            var names = boss ? new[] { "f_index.", "f_middle.", "f_ring.", "f_pinky." } : new[] { "Index", "Middle", "Ring", "Little" };
            foreach (string name in names)
                for (int i = 0; i < 3; i++)
                {
                    var bone = Find(root, boss ? name + "0" + (i + 1) + ".R" : name + (i + 2) + ".R");
                    if (bone) joints.Add(new Finger { bone = bone, curl = i == 0 ? 34 : i == 1 ? 48 : 32 });
                }
            foreach (string name in boss ? new[] { "thumb.02.R", "thumb.03.R" } : new[] { "Thumb2.R", "Thumb3.R" })
            {
                var bone = Find(root, name);
                if (bone) joints.Add(new Finger { bone = bone, curl = 26, thumb = true });
            }
            return joints.ToArray();
        }

        private void CurlFingers(Performance p, float amount)
        {
            if (amount <= 0) return;
            Vector3 palmBack = p.right.hand.rotation * p.localPalmBack;
            foreach (var finger in p.fingers)
            {
                if (!finger.bone) continue;
                Vector3 forward = finger.bone.childCount > 0 ? finger.bone.GetChild(0).position - finger.bone.position :
                    p.right.hand.rotation * p.localFingerForward;
                if (finger.thumb)
                {
                    Vector3 contact = bottle.position + bottle.up * .185f + p.actorRight * .03f - p.tableForward * .04f;
                    var delta = Quaternion.FromToRotation(forward, contact - finger.bone.position);
                    finger.bone.rotation = Quaternion.Slerp(Quaternion.identity,
                        Quaternion.RotateTowards(Quaternion.identity, delta, finger.curl), amount) * finger.bone.rotation;
                    continue;
                }
                Vector3 axis = Vector3.Cross(forward.normalized, -palmBack).normalized;
                finger.bone.rotation = Quaternion.AngleAxis(finger.curl * amount, axis) * finger.bone.rotation;
            }
        }

        private static float Ease(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * t * (10 + t * (-15 + 6 * t));
        }
        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float s = 1 - t;
            return s * s * s * a + 3 * s * s * t * b + 3 * s * t * t * c + t * t * t * d;
        }
        private static Transform Find(Transform root, string name)
        {
            foreach (var node in root.GetComponentsInChildren<Transform>(true)) if (node.name == name) return node;
            return null;
        }

        public void SetBurstRecoil(float amount) { if (current != null) current.recoil = Mathf.Clamp01(amount); }
        public void CancelAndRestore()
        {
            LastPlaybackCompleted = false;
            generation++;
            if (current != null) Restore(current);
        }
        private void Restore(Performance p)
        {
            if (current == p) current = null;
            if (bottle)
            {
                if (bottle.parent != p.bottleParent) bottle.SetParent(p.bottleParent, false);
                p.bottleRest.Apply(bottle);
            }
            for (int i = 0; i < p.nodes.Length; i++) p.rest[i].Apply(p.nodes[i]);
            for (int i = 0; i < p.skins.Length; i++) if (p.skins[i]) p.skins[i].updateWhenOffscreen = p.skinUpdates[i];
            if (!p.actor) return;
            if (p.actor.transform.parent != p.actorParent) p.actor.transform.SetParent(p.actorParent, false);
            p.actorRest.Apply(p.actor.transform);
            p.actor.enabled = p.animatorEnabled;
            if (p.animatorEnabled && p.actor.runtimeAnimatorController && p.idleHash != 0)
            {
                p.actor.Play(p.idleHash, 0, p.idleNormalized + (ColaGameClock.Time - p.startedAt) * p.idleRate);
                p.actor.Update(0);
            }
            p.actorRest.Apply(p.actor.transform);
        }

        [ContextMenu("Preview Player Shake (Play Mode)")]
        public void PreviewPlayerShake() => Preview(false);
        [ContextMenu("Preview Boss Shake (Play Mode)")]
        public void PreviewBossShake() => Preview(true);
        private void Preview(bool boss)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || IsPlaying || !CanPlay(boss, 2)) return;
            var interaction = FindAnyObjectByType<ShakeSelectionInteraction>();
            if (interaction && interaction.IsBusy) return;
            StartCoroutine(PlayShake(boss, 2));
        }
        public void BeginPlayerPlayPreview()
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || !previewPlayerOnPlay) return;
            StopPlayerPlayPreview(); previewLoop = StartCoroutine(RepeatPreview());
        }
        private IEnumerator RepeatPreview()
        {
            yield return new ColaPauseDelay(.75f);
            while (previewPlayerOnPlay && isActiveAndEnabled)
            {
                var interaction = FindAnyObjectByType<ShakeSelectionInteraction>();
                if (!interaction || !interaction.IsBusy) yield return PlayShake(false, Mathf.Clamp(playPreviewStrength, 1, 3));
                yield return new ColaPauseDelay(1);
            }
            previewLoop = null;
        }
        [ContextMenu("Stop Player Play Preview")]
        public void StopPlayerPlayPreview()
        {
            if (previewLoop != null) StopCoroutine(previewLoop);
            previewLoop = null; CancelAndRestore();
        }
        private void OnDisable() { StopPlayerPlayPreview(); CancelAndRestore(); }
        private void OnDestroy() => CancelAndRestore();
    }
}
