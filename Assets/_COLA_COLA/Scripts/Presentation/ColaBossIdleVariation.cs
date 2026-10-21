using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Small, irregular seated gestures layered on the imported boss's original idle.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class ColaBossIdleVariation : MonoBehaviour
    {
        private ShakeSelectionInteraction owner;
        private Animator actor;
        private Transform[] nodes;
        private Quaternion[] rotations;
        private bool overridden;
        private float started, duration, nextAt;
        private int gesture = -1, previous = -1;
        private float side;
        private const int GestureCount = 12;
        private SupportedArm rightSupport, leftSupport;
        private bool menuIdle;
        private float menuTime;
        private AnimatorUpdateMode previousUpdateMode;
        private AnimatorCullingMode previousCullingMode;
        private float IdleTime => menuIdle ? menuTime : ColaGameClock.Time;
        private static readonly int[] MenuGestures = { 0, 1, 2, 4, 5, 8, 9 };

        private sealed class SupportedArm
        {
            private readonly Transform upper, elbow, hand;
            private Vector3 target, pole;
            private Quaternion orientation;
            public bool Valid => upper && elbow && hand;
            public SupportedArm(Transform[] rig, string side)
            {
                upper = FindBone(rig, "upper_arm." + side);
                elbow = FindBone(rig, "forearm." + side);
                hand = FindBone(rig, "hand." + side);
            }
            public void Capture()
            {
                if (!Valid) return;
                target = hand.position; pole = elbow.position; orientation = hand.rotation;
            }
            public void Solve()
            {
                if (!Valid) return;
                Vector3 start = upper.position, offset = target - start;
                float a = Vector3.Distance(start, elbow.position), b = Vector3.Distance(elbow.position, hand.position);
                if (a < .0001f || b < .0001f || offset.sqrMagnitude < .000001f) return;
                Vector3 axis = offset.normalized;
                float distance = Mathf.Clamp(offset.magnitude, Mathf.Abs(a - b) + .00001f, a + b - .00001f);
                Vector3 bend = Vector3.ProjectOnPlane(pole - start, axis).normalized;
                if (bend.sqrMagnitude < .5f) bend = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
                if (bend.sqrMagnitude < .5f) bend = Vector3.ProjectOnPlane(Vector3.right, axis).normalized;
                float along = (a * a - b * b + distance * distance) / (2 * distance);
                Vector3 desiredElbow = start + axis * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
                upper.rotation = Quaternion.FromToRotation(elbow.position - start, desiredElbow - start) * upper.rotation;
                elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, start + axis * distance - elbow.position) * elbow.rotation;
                hand.rotation = orientation;
            }
        }
        // Presentation has its own RNG; gestures cannot change item or explosion outcomes.
        private readonly System.Random random = new System.Random();

        public static void EnsureFor(ShakeSelectionInteraction interaction)
        {
            if (!interaction || !Application.IsPlaying(interaction.gameObject)) return;
            var idle = interaction.GetComponent<ColaBossIdleVariation>();
            if (!idle) idle = interaction.gameObject.AddComponent<ColaBossIdleVariation>();
            idle.RestorePose();
            idle.owner = interaction;
            idle.actor = interaction.shakeAnimator ? interaction.shakeAnimator.bossAnimator : null;
            if (interaction.shakeAnimator) interaction.shakeAnimator.IdleVariation = idle;
            if (!idle.actor) return;
            string[] names = { "spine.004", "spine.005", "f_index.02.R", "f_middle.02.R",
                "f_index.02.L", "f_middle.02.L", "f_pinky.02.L", "f_ring.02.R", "f_pinky.02.R", "f_ring.02.L",
                "spine.003", "spine.002", "upper_arm.R", "forearm.R", "hand.R", "upper_arm.L", "forearm.L", "hand.L" };
            idle.nodes = new Transform[names.Length];
            idle.rotations = new Quaternion[names.Length];
            var rig = idle.actor.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < names.Length; i++)
                idle.nodes[i] = FindBone(rig, names[i]);
            idle.rightSupport = new SupportedArm(rig, "R");
            idle.leftSupport = new SupportedArm(rig, "L");
            idle.gesture = -1;
            idle.previous = -1;
            idle.nextAt = idle.IdleTime + 1.8f;
        }

        private static Transform FindBone(Transform[] rig, string name)
        {
            foreach (var bone in rig) if (bone.name == name) return bone;
            return null;
        }

        public void SetMenuIdle(bool value)
        {
            if (menuIdle == value) return;
            RestorePose();
            if (value)
            {
                if (!actor) return;
                previousUpdateMode = actor.updateMode;
                previousCullingMode = actor.cullingMode;
                actor.updateMode = AnimatorUpdateMode.UnscaledTime;
                actor.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                menuTime = 0;
            }
            else if (actor)
            {
                actor.updateMode = previousUpdateMode;
                actor.cullingMode = previousCullingMode;
            }
            menuIdle = value;
            gesture = -1;
            nextAt = IdleTime + 1.2f;
        }

        private void Update()
        {
            RestorePose();
            if (menuIdle) menuTime += Time.unscaledDeltaTime;
        }

        private void LateUpdate()
        {
            if (!Application.IsPlaying(gameObject) || !owner || !actor || !actor.enabled || nodes == null) return;
            bool occupied = !menuIdle && (!owner.InputEnabled || (owner.shakeAnimator && owner.shakeAnimator.IsPlaying) ||
                (owner.items && (owner.items.IsUsingItem || owner.items.IsReceivingItems() || owner.items.IsReceivingItems(true) ||
                    !owner.items.match || owner.items.match.State != ColaDuelController.DuelState.Playing)) ||
                (owner.burstPresentation && owner.burstPresentation.Stage != ColaBurstPresentation.BurstStage.Idle));
            if (occupied)
            {
                gesture = -1;
                nextAt = IdleTime + 1.0f;
                return;
            }
            float now = IdleTime;
            if (gesture < 0)
            {
                if (now < nextAt) return;
                // Context changes attention, never the hidden result or gameplay RNG.
                int candidate = menuIdle ? MenuGestures[random.Next(MenuGestures.Length)] : random.Next(GestureCount);
                if (!menuIdle && owner.IsBossTurn && random.NextDouble() < .45) candidate = 10;
                else if (!menuIdle && owner.Model != null && owner.Model.Pressure >= owner.Model.Limit * .65f && random.NextDouble() < .4) candidate = 11;
                if (candidate == previous)
                    candidate = menuIdle ? MenuGestures[(System.Array.IndexOf(MenuGestures, previous) + 1 + random.Next(MenuGestures.Length - 1)) % MenuGestures.Length] : (previous + 1 + random.Next(GestureCount - 1)) % GestureCount;
                gesture = candidate;
                previous = gesture;
                side = random.Next(2) == 0 ? -1 : 1;
                duration = (gesture == 9 ? 2.1f : 2.6f) + (float)random.NextDouble() * 1.5f;
                started = now;
            }
            float t = (now - started) / duration;
            if (t >= 1) { gesture = -1; nextAt = now + 1.8f + (float)random.NextDouble() * 2.7f; return; }
            for (int i = 0; i < nodes.Length; i++) if (nodes[i]) rotations[i] = nodes[i].localRotation;
            overridden = true;
            rightSupport?.Capture(); leftSupport?.Capture();
            bool movedBody = false;
            float envelope = Mathf.SmoothStep(0, 1, t / .24f) * (1 - Mathf.SmoothStep(0, 1, (t - .64f) / .36f));
            switch (gesture)
            {
                case 0: Turn(0, new Vector3(0, side * 9, side * 1.3f) * envelope); break; // glance sideways
                case 1: Turn(0, new Vector3(-6, side * 2, 0) * envelope); break; // inspect the cola
                case 2: Turn(0, new Vector3(0, side * 3, side * 4) * envelope); break; // attentive head tilt
                case 3:
                    Turn(0, new Vector3(-2.5f * Mathf.Sin(t * Mathf.PI * 2), 0, 0) * envelope);
                    Turn(1, new Vector3(-1, 0, 0) * envelope);
                    break;
                case 4:
                    Curl(2, 8 * Mathf.Sin(t * Mathf.PI * 4) * envelope);
                    Curl(3, 4 * Mathf.Sin(t * Mathf.PI * 4 - .5f) * envelope);
                    break;
                case 5:
                    Curl(4, -6 * envelope); Curl(5, -4 * envelope); Curl(6, -3 * envelope);
                    Turn(0, new Vector3(-2, -side * 4, 0) * envelope);
                    break;
                case 6: // Read the dial, then ease back without lifting either hand.
                    Turn(0, new Vector3(-4, AttentionYaw(owner.gauge ? owner.gauge.transform : null, side * 8), -side) * envelope);
                    movedBody = Chest(new Vector3(-.8f, 0, 0) * envelope);
                    break;
                case 7: // Check the items in front of the boss.
                    var inventory = owner.items ? owner.items.GetComponent<ColaItemPresentation>() : null;
                    Turn(0, new Vector3(-6, AttentionYaw(inventory ? inventory.StorageRoot(true) : null, -side * 8), side * 1.4f) * envelope);
                    Curl(4, -4 * envelope); Curl(9, -3 * envelope);
                    break;
                case 8: // A slow exhale and a small settling of the upper back.
                    movedBody = Chest(new Vector3(.9f, 0, side * .35f) * envelope);
                    Turn(0, new Vector3(-3, side * 1.5f, -side * 1.5f) * envelope);
                    Curl(2, -4 * envelope); Curl(3, -3 * envelope); Curl(7, -2 * envelope);
                    break;
                case 9: // Staggered fingertip lifts; palms remain supported.
                    Curl(2, -11 * Pulse(t, .18f, .18f) * envelope);
                    Curl(3, -9 * Pulse(t, .35f, .18f) * envelope);
                    Curl(7, -7 * Pulse(t, .52f, .18f) * envelope);
                    Turn(0, new Vector3(-1, side * 2, 0) * envelope);
                    break;
                case 10: // A thinking pause, followed by a deliberate look back at the cola.
                    float reconsider = Mathf.SmoothStep(0, 1, (t - .3f) / .4f);
                    Turn(0, new Vector3(-5 * reconsider, side * 5 * (1 - reconsider), side * 2) * envelope);
                    Turn(1, new Vector3(-1.5f, -side, 0) * envelope);
                    Curl(4, -3 * envelope); Curl(5, -2 * envelope);
                    break;
                case 11: // Public high pressure prompts a restrained, tense posture.
                    movedBody = Chest(new Vector3(-.65f, 0, 0) * envelope);
                    Turn(0, new Vector3(-3.5f, -side * 2, side) * envelope);
                    Curl(2, 3 * envelope); Curl(3, 2 * envelope);
                    Curl(4, 3 * envelope); Curl(5, 2 * envelope);
                    break;
            }
            if (movedBody) { rightSupport.Solve(); leftSupport.Solve(); }
        }

        private bool Chest(Vector3 angles)
        {
            if (!nodes[10] || rightSupport == null || leftSupport == null || !rightSupport.Valid || !leftSupport.Valid) return false;
            Turn(10, angles * .7f);
            Turn(11, angles * .3f);
            return true;
        }

        private float AttentionYaw(Transform target, float fallback)
        {
            if (!target || !owner.playerCamera || !nodes[0]) return fallback;
            Vector3 origin = nodes[0].position;
            Vector3 forward = Vector3.ProjectOnPlane(owner.playerCamera.transform.position - origin, Vector3.up);
            Vector3 direction = Vector3.ProjectOnPlane(target.position - origin, Vector3.up);
            if (forward.sqrMagnitude < .0001f || direction.sqrMagnitude < .0001f) return fallback;
            return Mathf.Clamp(Vector3.SignedAngle(forward, direction, Vector3.up), -12, 12);
        }

        private static float Pulse(float t, float start, float width)
        {
            float phase = Mathf.Clamp01((t - start) / width);
            return phase > 0 && phase < 1 ? Mathf.Sin(phase * Mathf.PI) * Mathf.Sin(phase * Mathf.PI) : 0;
        }

        private void Turn(int index, Vector3 angles)
        {
            if (nodes[index]) nodes[index].rotation = Quaternion.Euler(angles) * nodes[index].rotation;
        }
        private void Curl(int index, float degrees)
        {
            var bone = nodes[index];
            if (!bone || bone.childCount == 0) return;
            var axis = Vector3.Cross((bone.GetChild(0).position - bone.position).normalized, Vector3.down);
            if (axis.sqrMagnitude > .01f) bone.rotation = Quaternion.AngleAxis(degrees, axis.normalized) * bone.rotation;
        }
        public void RestorePose()
        {
            if (!overridden || nodes == null) return;
            for (int i = 0; i < nodes.Length; i++) if (nodes[i]) nodes[i].localRotation = rotations[i];
            overridden = false;
        }
        private void OnDisable() { SetMenuIdle(false); RestorePose(); }
        private void OnDestroy() { SetMenuIdle(false); RestorePose(); }
    }
}
