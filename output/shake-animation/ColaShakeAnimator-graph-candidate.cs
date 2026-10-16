using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ColaCola.Presentation
{
    /// <summary>Plays each actor's own rig and attaches the held bottle to that actor's hand.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(1000)]
    public sealed class ColaShakeAnimator : MonoBehaviour
    {
        public Animator playerAnimator, bossAnimator;
        public Transform playerHand, bossHand;
        public const float GripStartTime = 1.15f;
        public const float GripEndTime = 3.35f;
        public AnimationClip[] playerClips = new AnimationClip[3];
        public AnimationClip[] bossClips = new AnimationClip[3];
        public AnimationClip[] playerBottleClips = new AnimationClip[3];
        public AnimationClip[] bossBottleClips = new AnimationClip[3];
        public Transform bottle;
        [Min(0)] public float idleBlendSeconds = .2f;

        public bool IsPlaying => playback != null;
        public bool LastPlaybackCompleted { get; private set; }
        public bool PlayingBoss => playback != null && playback.boss;
        public float PlaybackTime => playback != null ? playback.elapsed : 0;
        public Transform ActiveHand => playback != null ? playback.hand : null;
        public bool BottleHeld => playback != null && playback.elapsed >= GripStartTime && playback.elapsed <= GripEndTime;
        public bool UsesAnimatorGraph => playback != null && playback.graph.IsValid();

        private Playback playback;
        private int playbackVersion;

        private struct LocalPose
        {
            public Vector3 position, scale;
            public Quaternion rotation;

            public LocalPose(Transform node)
            {
                position = node.localPosition;
                rotation = node.localRotation;
                scale = node.localScale;
            }

            public void Apply(Transform node)
            {
                if (!node) return;
                node.localPosition = position;
                node.localRotation = rotation;
                node.localScale = scale;
            }
        }

        private sealed class Playback
        {
            public bool boss, animatorEnabled;
            public Animator animator;
            public AnimationClip actorClip, bottleClip, idleClip;
            public Transform hand, bottleTransform, bottleParent, actorRootParent;
            public Vector3 bottlePositionInHand;
            public Quaternion bottleRotationInHand;
            public Transform[] bones;
            public LocalPose[] rest;
            public LocalPose actorRootRest, bottleRest;
            public float elapsed, duration, normalizedTime, idleSpeed;
            public int stateHash;
            public PlayableGraph graph;
            public AnimationClipPlayable actorPlayable, idlePlayable;
            public AnimationMixerPlayable mixer;
        }

        public bool CanPlay(bool boss, int strength)
        {
            if (strength < 1 || strength > 3 || !bottle) return false;
            var actor = boss ? bossAnimator : playerAnimator;
            var otherActor = boss ? playerAnimator : bossAnimator;
            var hand = boss ? bossHand : playerHand;
            var clips = boss ? bossClips : playerClips;
            var bottleClips = boss ? bossBottleClips : playerBottleClips;
            int index = strength - 1;
            return actor && actor != otherActor && hand && hand.IsChildOf(actor.transform) &&
                clips != null && clips.Length > index && clips[index] && clips[index].length > 0 &&
                bottleClips != null && bottleClips.Length > index && bottleClips[index] && bottleClips[index].length > 0;
        }

        /// <summary>The shared bottle is locked to one performance at a time. No pressure or turn rules are changed here.</summary>
        public IEnumerator PlayShake(bool boss, int strength)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || IsPlaying || !CanPlay(boss, strength)) yield break;
            int version = ++playbackVersion;
            LastPlaybackCompleted = false;
            try
            {
                BeginPlayback(boss, strength);
                while (playback != null && version == playbackVersion) yield return null;
            }
            finally
            {
                // Explicit cancellation is also called by the interaction before stopping its own coroutines.
                // Keeping the clock here on this component makes restoration independent of the waiting caller.
                if (version == playbackVersion && playback != null) CancelAndRestore();
            }
        }

        private void BeginPlayback(bool boss, int strength)
        {
            var animator = boss ? bossAnimator : playerAnimator;
            var state = animator.runtimeAnimatorController ? animator.GetCurrentAnimatorStateInfo(0) : default;
            var infos = animator.runtimeAnimatorController ? animator.GetCurrentAnimatorClipInfo(0) : Array.Empty<AnimatorClipInfo>();
            AnimationClip idleClip = null;
            float greatestWeight = -1;
            foreach (var info in infos)
                if (info.clip && info.weight > greatestWeight) { idleClip = info.clip; greatestWeight = info.weight; }
            if (!idleClip && animator.runtimeAnimatorController)
            {
                var clips = animator.runtimeAnimatorController.animationClips;
                if (clips.Length > 0) idleClip = clips[0];
            }
            var allNodes = animator.GetComponentsInChildren<Transform>(true);
            var bones = new Transform[Mathf.Max(0, allNodes.Length - 1)];
            int next = 0;
            foreach (var node in allNodes) if (node != animator.transform) bones[next++] = node;
            var pose = new Playback
            {
                boss = boss,
                animator = animator,
                hand = boss ? bossHand : playerHand,
                animatorEnabled = animator.enabled,
                actorClip = (boss ? bossClips : playerClips)[strength - 1],
                bottleClip = (boss ? bossBottleClips : playerBottleClips)[strength - 1],
                idleClip = idleClip,
                bottleTransform = bottle,
                bottleParent = bottle.parent,
                actorRootParent = animator.transform.parent,
                normalizedTime = state.normalizedTime,
                stateHash = state.fullPathHash,
                idleSpeed = animator.enabled ? animator.speed * state.speed * state.speedMultiplier : 0,
                bones = bones,
                rest = new LocalPose[bones.Length],
                actorRootRest = new LocalPose(animator.transform),
                bottleRest = new LocalPose(bottle)
            };
            pose.duration = Mathf.Max(pose.actorClip.length, pose.bottleClip.length);
            for (int i = 0; i < bones.Length; i++) pose.rest[i] = new LocalPose(bones[i]);
            animator.enabled = true;
            playback = pose;
            CreateActorGraph(pose);
            CaptureHandGrip(pose);
            SamplePlayback(pose);
        }

        private static void CaptureHandGrip(Playback pose)
        {
            // Capture against the authored contact pose rather than the current Idle phase.
            // The offset belongs to this rig; the other actor's bones never participate.
            EvaluateActor(pose, GripStartTime, 1);
            pose.bottleClip.SampleAnimation(pose.bottleTransform.gameObject, GripStartTime);
            pose.bottlePositionInHand = pose.hand.InverseTransformPoint(pose.bottleTransform.position);
            pose.bottleRotationInHand = Quaternion.Inverse(pose.hand.rotation) * pose.bottleTransform.rotation;
            EvaluateActor(pose, 0, 0);
            pose.bottleRest.Apply(pose.bottleTransform);
        }

        private static void CreateActorGraph(Playback pose)
        {
            pose.graph = PlayableGraph.Create("COLA_COLA_" + (pose.boss ? "Boss" : "Player") + "_HandPlayback");
            pose.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            pose.mixer = AnimationMixerPlayable.Create(pose.graph, 2);
            pose.actorPlayable = AnimationClipPlayable.Create(pose.graph, pose.actorClip);
            pose.actorPlayable.SetApplyFootIK(false);
            pose.actorPlayable.SetSpeed(0);
            pose.graph.Connect(pose.actorPlayable, 0, pose.mixer, 1);
            if (pose.idleClip)
            {
                pose.idlePlayable = AnimationClipPlayable.Create(pose.graph, pose.idleClip);
                pose.idlePlayable.SetApplyFootIK(false);
                pose.idlePlayable.SetSpeed(0);
                pose.graph.Connect(pose.idlePlayable, 0, pose.mixer, 0);
            }
            var output = AnimationPlayableOutput.Create(pose.graph, "Original character hand", pose.animator);
            output.SetSourcePlayable(pose.mixer);
            pose.graph.Play();
        }

        private static void EvaluateActor(Playback pose, float time, float blend)
        {
            if (pose.idlePlayable.IsValid())
            {
                float idleTime = pose.normalizedTime * pose.idleClip.length + time * pose.idleSpeed;
                pose.idlePlayable.SetTime(Mathf.Repeat(idleTime, pose.idleClip.length));
            }
            else blend = 1;
            pose.actorPlayable.SetTime(Mathf.Min(time, pose.actorClip.length));
            pose.mixer.SetInputWeight(0, 1 - blend);
            pose.mixer.SetInputWeight(1, blend);
            pose.graph.Evaluate(0);
            for (int i = 0; i < pose.bones.Length; i++)
                if (pose.bones[i]) pose.bones[i].localScale = pose.rest[i].scale;
            pose.actorRootRest.Apply(pose.animator.transform);
        }

        private void LateUpdate()
        {
            var pose = playback;
            if (pose == null) return;
            if (!pose.animator || !pose.hand || !pose.bottleTransform)
            {
                CancelAndRestore();
                return;
            }
            try
            {
                pose.elapsed = Mathf.Min(pose.duration, pose.elapsed + Time.unscaledDeltaTime);
                SamplePlayback(pose);
                if (pose.elapsed >= pose.duration)
                {
                    LastPlaybackCompleted = true;
                    RestorePlayback(pose);
                }
            }
            catch (Exception exception)
            {
                CancelAndRestore();
                Debug.LogException(exception, this);
            }
        }

        private void SamplePlayback(Playback pose)
        {
            float blend = idleBlendSeconds <= 0 ? 1 : Mathf.Min(
                Mathf.SmoothStep(0, 1, pose.elapsed / idleBlendSeconds),
                Mathf.SmoothStep(0, 1, (pose.duration - pose.elapsed) / idleBlendSeconds));
            EvaluateActor(pose, pose.elapsed, blend);
            pose.bottleClip.SampleAnimation(pose.bottleTransform.gameObject, Mathf.Min(pose.elapsed, pose.bottleClip.length));
            pose.bottleTransform.localScale = pose.bottleRest.scale;
            if (pose.elapsed >= GripStartTime && pose.elapsed <= GripEndTime)
                pose.bottleTransform.SetPositionAndRotation(pose.hand.TransformPoint(pose.bottlePositionInHand),
                    pose.hand.rotation * pose.bottleRotationInHand);
        }

        public void CancelAndRestore()
        {
            LastPlaybackCompleted = false;
            ++playbackVersion;
            var pose = playback;
            if (pose != null) RestorePlayback(pose);
        }

        private void RestorePlayback(Playback pose)
        {
            // Clear first: callers can cancel twice, or reset the interaction from a completion callback.
            if (playback == pose) playback = null;
            if (pose.graph.IsValid()) pose.graph.Destroy();
            if (pose.bottleTransform && pose.bottleTransform.parent != pose.bottleParent)
                pose.bottleTransform.SetParent(pose.bottleParent, false);
            pose.bottleRest.Apply(pose.bottleTransform);
            if (!pose.animator) return;
            if (pose.animator.transform.parent != pose.actorRootParent)
                pose.animator.transform.SetParent(pose.actorRootParent, false);
            for (int i = 0; i < pose.bones.Length; i++) pose.rest[i].Apply(pose.bones[i]);
            pose.actorRootRest.Apply(pose.animator.transform);
            pose.animator.enabled = pose.animatorEnabled;
            if (pose.animatorEnabled && pose.animator.runtimeAnimatorController && pose.stateHash != 0)
            {
                pose.animator.Rebind();
                float normalized = pose.normalizedTime;
                if (pose.idleClip) normalized += pose.elapsed * pose.idleSpeed / pose.idleClip.length;
                pose.animator.Play(pose.stateHash, 0, normalized);
            }
            if (pose.animatorEnabled && pose.animator.gameObject.activeInHierarchy)
                pose.animator.Update(0);
            pose.actorRootRest.Apply(pose.animator.transform);
        }

        [ContextMenu("Preview Player Shake (Play Mode)")]
        public void PreviewPlayerShake() => Preview(false);

        [ContextMenu("Preview Boss Shake (Play Mode)")]
        public void PreviewBossShake() => Preview(true);

        private void Preview(bool boss)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || IsPlaying || !CanPlay(boss, 2)) return;
            var interaction = FindFirstObjectByType<ShakeSelectionInteraction>();
            if (interaction && interaction.IsBusy) return;
            StartCoroutine(PlayShake(boss, 2));
        }

        private void OnDisable() => CancelAndRestore();
        private void OnDestroy() => CancelAndRestore();
    }
}
