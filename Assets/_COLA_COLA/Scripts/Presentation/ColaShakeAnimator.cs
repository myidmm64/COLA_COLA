using System;
using System.Collections;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Plays a baked hand-and-bottle performance without replacing either seated Idle controller.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(1000)]
    public sealed class ColaShakeAnimator : MonoBehaviour
    {
        public Animator playerAnimator, bossAnimator;
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
            public Transform bottleTransform, bottleParent, actorRootParent;
            public Transform[] bones;
            public LocalPose[] rest, idle;
            public LocalPose actorRootRest, bottleRest;
            public float elapsed, duration, normalizedTime, idleSpeed;
            public int stateHash;
        }

        public bool CanPlay(bool boss, int strength)
        {
            if (strength < 1 || strength > 3 || !bottle) return false;
            var actor = boss ? bossAnimator : playerAnimator;
            var clips = boss ? bossClips : playerClips;
            var bottleClips = boss ? bossBottleClips : playerBottleClips;
            int index = strength - 1;
            return actor && clips != null && clips.Length > index && clips[index] && clips[index].length > 0 &&
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
                idle = new LocalPose[bones.Length],
                actorRootRest = new LocalPose(animator.transform),
                bottleRest = new LocalPose(bottle)
            };
            pose.duration = Mathf.Max(pose.actorClip.length, pose.bottleClip.length);
            for (int i = 0; i < bones.Length; i++) pose.rest[i] = new LocalPose(bones[i]);
            animator.enabled = false;
            playback = pose;
            SamplePlayback(pose);
        }

        private void LateUpdate()
        {
            var pose = playback;
            if (pose == null) return;
            if (!pose.animator || !pose.bottleTransform)
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
            SampleIdle(pose);
            for (int i = 0; i < pose.bones.Length; i++)
                if (pose.bones[i]) pose.idle[i] = new LocalPose(pose.bones[i]);
            pose.actorClip.SampleAnimation(pose.animator.gameObject, Mathf.Min(pose.elapsed, pose.actorClip.length));
            float blend = idleBlendSeconds <= 0 ? 1 : Mathf.Min(
                Mathf.SmoothStep(0, 1, pose.elapsed / idleBlendSeconds),
                Mathf.SmoothStep(0, 1, (pose.duration - pose.elapsed) / idleBlendSeconds));
            for (int i = 0; i < pose.bones.Length; i++)
            {
                var node = pose.bones[i];
                if (!node) continue;
                node.localPosition = Vector3.LerpUnclamped(pose.idle[i].position, node.localPosition, blend);
                node.localRotation = Quaternion.SlerpUnclamped(pose.idle[i].rotation, node.localRotation, blend);
                node.localScale = Vector3.LerpUnclamped(pose.idle[i].scale, node.localScale, blend);
            }
            // Even a mistakenly authored actor-root curve must not move the seated rig or change its imported scale.
            pose.actorRootRest.Apply(pose.animator.transform);
            pose.bottleClip.SampleAnimation(pose.bottleTransform.gameObject, Mathf.Min(pose.elapsed, pose.bottleClip.length));
            pose.bottleTransform.localScale = pose.bottleRest.scale;
        }

        private static void SampleIdle(Playback pose)
        {
            for (int i = 0; i < pose.bones.Length; i++) pose.rest[i].Apply(pose.bones[i]);
            if (pose.animator && pose.animatorEnabled && pose.idleClip)
            {
                float time = pose.normalizedTime * pose.idleClip.length + pose.elapsed * pose.idleSpeed;
                pose.idleClip.SampleAnimation(pose.animator.gameObject, Mathf.Repeat(time, pose.idleClip.length));
            }
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
            if (pose.bottleTransform && pose.bottleTransform.parent != pose.bottleParent)
                pose.bottleTransform.SetParent(pose.bottleParent, false);
            pose.bottleRest.Apply(pose.bottleTransform);
            if (!pose.animator) return;
            if (pose.animator.transform.parent != pose.actorRootParent)
                pose.animator.transform.SetParent(pose.actorRootParent, false);
            SampleIdle(pose);
            pose.actorRootRest.Apply(pose.animator.transform);
            if (pose.animator.runtimeAnimatorController && pose.stateHash != 0)
            {
                float normalized = pose.normalizedTime;
                if (pose.idleClip) normalized += pose.elapsed * pose.idleSpeed / pose.idleClip.length;
                pose.animator.Play(pose.stateHash, 0, normalized);
            }
            pose.animator.enabled = pose.animatorEnabled;
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
