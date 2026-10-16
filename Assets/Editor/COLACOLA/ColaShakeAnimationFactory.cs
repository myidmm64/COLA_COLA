using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ColaCola.Presentation;

namespace ColaCola.Editor
{
    /// <summary>Bakes a seated one-hand reach, cylindrical grip, lift, shake and set-down for the two imported rigs.</summary>
    public static class ColaShakeAnimationFactory
    {
        public const string Folder = "Assets/_COLA_COLA/Animations/Shake";
        public const float Duration = 4.4f;
        public const float GripStart = ColaShakeAnimator.GripStartTime;
        public const float GripEnd = ColaShakeAnimator.GripEndTime;
        private const int Samples = 132;
        private const float GripHeight = .165f;
        private const float FingerRadius = .084f;

        private struct LocalPose
        {
            public Vector3 position, scale;
            public Quaternion rotation;
            public LocalPose(Transform node) { position = node.localPosition; rotation = node.localRotation; scale = node.localScale; }
            public void Apply(Transform node) { node.localPosition = position; node.localRotation = rotation; node.localScale = scale; }
        }

        private sealed class Arm
        {
            public Transform upper, elbow, wrist;
            public Vector3 position, pole;
            public Vector3 elbowRestLocal;
            public float upperLength, lowerLength;
            public Quaternion rotation;
            public Arm(Transform root, string side, bool boss)
            {
                upper = Bone(root, (boss ? "upper_arm." : "UpperArm.") + side);
                elbow = Bone(root, (boss ? "forearm." : "LowArm.") + side);
                wrist = Bone(root, (boss ? "hand." : "Wrist.") + side);
                position = wrist.position; rotation = wrist.rotation; pole = elbow.position;
                elbowRestLocal = elbow.localPosition;
                upperLength = Vector3.Distance(upper.position, elbow.position);
                lowerLength = Vector3.Distance(elbow.position, wrist.position);
            }
            public void FitFirstPersonReach(Vector3 target, float reach)
            {
                // The stock FPS model has a deliberately short upper arm. Keep its open shoulder
                // at the original offscreen position and extend that existing bone, leaving the
                // forearm, wrist, hand mesh and finger proportions unchanged. No geometry is added.
                float length = Mathf.Max(upperLength, Vector3.Distance(upper.position, target) - lowerLength * (1 - .2f * reach));
                elbow.localPosition = elbowRestLocal * (length / upperLength);
            }
            public void Solve(Vector3 target, Vector3 bend, Quaternion orientation, string context)
            {
                var start = upper.position;
                var a = Vector3.Distance(start, elbow.position); var b = Vector3.Distance(elbow.position, wrist.position);
                var distance = Vector3.Distance(start, target);
                if (distance > a + b - .00001f || distance < Mathf.Abs(a - b) + .00001f)
                    throw new InvalidOperationException($"Unreachable {context}: distance={distance:F5}, permitted={Mathf.Abs(a - b):F5}..{a + b:F5}, shoulder={start:F4}, wrist={target:F4}");
                var direction = (target - start).normalized;
                var perpendicular = Vector3.ProjectOnPlane(bend - start, direction).normalized;
                if (perpendicular.sqrMagnitude < .1f) perpendicular = Vector3.ProjectOnPlane(Vector3.right, direction).normalized;
                var along = (a * a - b * b + distance * distance) / (2 * distance);
                var desiredElbow = start + direction * along + perpendicular * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
                upper.rotation = Quaternion.FromToRotation(elbow.position - start, desiredElbow - start) * upper.rotation;
                elbow.rotation = Quaternion.FromToRotation(wrist.position - elbow.position, target - elbow.position) * elbow.rotation;
                wrist.rotation = orientation;
            }
        }

        public static void Build(GameObject sceneRoot)
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var bottle = Find(sceneRoot.transform, "HeroCola_ExistingModel");
            var bottleRest = new LocalPose(bottle);
            var report = new StringBuilder("Baked one-hand cola performances; 4.4 s, 30 samples/s, 3 strengths per actor.\n");
            try
            {
                BuildActor(Find(sceneRoot.transform, CharacterIdleFactory.ArmsName), bottle, false, report);
                BuildActor(Find(sceneRoot.transform, CharacterIdleFactory.BossName), bottle, true, report);
            }
            finally { bottleRest.Apply(bottle); }
            AssetDatabase.SaveAssets();
            File.WriteAllText("output/shake-animation/Animation-authoring-validation.txt", report.ToString());
        }

        public static string ActorClipPath(bool boss, int strength) => Folder + "/" + (boss ? "Boss" : "Player") + "_ColaShake_" + strength + ".anim";
        public static string BottleClipPath(bool boss, int strength) => Folder + "/" + (boss ? "Boss" : "Player") + "_BottleMotion_" + strength + ".anim";

        public static void BuildPlayerOnly(GameObject sceneRoot)
        {
            var report = new StringBuilder("Player-only right-side grip revision; boss clips and scene placement are not rebuilt.\n");
            BuildActor(Find(sceneRoot.transform, CharacterIdleFactory.ArmsName),
                Find(sceneRoot.transform, "HeroCola_ExistingModel"), false, report);
            AssetDatabase.SaveAssets();
            File.WriteAllText("output/shake-animation/Player-RightSide-authoring.txt", report.ToString());
        }

        private static void BuildActor(Transform actor, Transform bottle, bool boss, StringBuilder report)
        {
            var nodes = actor.GetComponentsInChildren<Transform>(true).Where(t => t != actor).ToArray();
            var paths = nodes.Select(t => AnimationUtility.CalculateTransformPath(t, actor)).ToArray();
            var rest = nodes.Select(t => new LocalPose(t)).ToArray();
            var animator = actor.GetComponent<Animator>();
            var enabled = animator.enabled; animator.enabled = false;
            var right = new Arm(actor, "R", boss); var left = new Arm(actor, "L", boss);
            var restBottle = new LocalPose(bottle);
            var basePosition = bottle.position; var baseRotation = bottle.rotation;
            var middle = Bone(actor, boss ? "f_middle.01.R" : "Middle2.R");
            var index = Bone(actor, boss ? "f_index.01.R" : "Index2.R");
            var little = Bone(actor, boss ? "f_pinky.01.R" : "Little2.R");
            var fingerForward = (middle.position - right.wrist.position).normalized;
            var backNormal = Vector3.Cross(fingerForward, index.position - little.position).normalized;
            if (Vector3.Dot(backNormal, Vector3.up) < 0) backNormal = -backNormal;
            var gripOrientation = Quaternion.LookRotation(boss ? Vector3.back : Vector3.forward, boss ? Vector3.left : Vector3.right)
                * Quaternion.Inverse(Quaternion.LookRotation(fingerForward, backNormal)) * right.rotation;
            var palmAdvance = Vector3.Distance(right.wrist.position, middle.position) * (boss ? .65f : .90f);
            var contactWrist = basePosition + (boss ? new Vector3(-.092f, GripHeight, palmAdvance) :
                new Vector3(.092f, GripHeight, -palmAdvance));
            var wristOffset = Quaternion.Inverse(baseRotation) * (contactWrist - basePosition);
            var wristRotationOffset = Quaternion.Inverse(baseRotation) * gripOrientation;
            try
            {
                for (var strength = 1; strength <= 3; strength++)
                {
                    var poses = new LocalPose[Samples + 1][];
                    var bottlePoses = new LocalPose[Samples + 1];
                    var maximumContactError = 0f; var minimumHandY = float.PositiveInfinity;
                    var minimumPlayerSide = float.PositiveInfinity;
                    for (var sample = 0; sample <= Samples; sample++)
                    {
                        for (var node = 0; node < nodes.Length; node++) rest[node].Apply(nodes[node]);
                        restBottle.Apply(bottle);
                        var time = sample * Duration / Samples;
                        BottlePose(time, strength, boss, basePosition, baseRotation, out var bottlePosition, out var bottleRotation);
                        bottle.SetPositionAndRotation(bottlePosition, bottleRotation);
                        var reach = Reach(time);
                        var hold = Hold(time);
                        var returnProgress = Ease((time - 3.6f) / .75f);
                        var grip = Ease((time - .95f) / .20f) * (1 - Ease((time - 3.35f) / .25f));
                        var target = bottlePosition + bottleRotation * wristOffset;
                        var handRotation = bottleRotation * wristRotationOffset;
                        if (time < .95f)
                        {
                            target = Vector3.Lerp(right.position, contactWrist, reach) + Vector3.up * ((boss ? .10f : .045f) * Mathf.Sin(Mathf.PI * reach));
                            handRotation = Quaternion.Slerp(right.rotation, gripOrientation, reach);
                        }
                        else if (time > 3.6f)
                        {
                            target = Vector3.Lerp(contactWrist, right.position, returnProgress) + Vector3.up * ((boss ? .10f : .045f) * Mathf.Sin(Mathf.PI * returnProgress));
                            handRotation = Quaternion.Slerp(gripOrientation, right.rotation, returnProgress);
                        }
                        var lean = reach * Mathf.Lerp(1, .28f, hold) * (1 - returnProgress);
                        if (boss)
                        {
                            var torso = Bone(actor, "spine.001");
                            torso.rotation = Quaternion.AngleAxis(-45 * lean, Vector3.right) * torso.rotation;
                            torso.position += new Vector3(0, .022f, -.13f) * lean;
                            var head = Bone(actor, "spine.004");
                            head.rotation = Quaternion.AngleAxis(29 * lean, Vector3.right) * head.rotation;
                            var supportPosition = left.position + Vector3.back * (.19f * lean);
                            var supportPole = Vector3.Lerp(left.pole, new Vector3(.65f, .74f, .62f), lean);
                            left.Solve(supportPosition, supportPole, left.rotation, "boss bracing left hand at " + time);
                        }
                        else
                        {
                            right.FitFirstPersonReach(target, reach * (1 - returnProgress));
                        }
                        var elbowPole = boss ? new Vector3(-.38f, .99f, .48f) :
                            Vector3.Lerp(new Vector3(.60f, .91f, -.62f), new Vector3(.59f, .98f, -.72f), hold);
                        elbowPole = Vector3.Lerp(right.pole, elbowPole, reach * (1 - returnProgress));
                        right.Solve(target, elbowPole, handRotation, (boss ? "boss" : "player") + " right hand at " + time);
                        WrapFingers(actor, bottlePosition, bottleRotation * Quaternion.Inverse(baseRotation), boss, grip);
                        if (time >= GripStart && time <= GripEnd)
                        {
                            maximumContactError = Mathf.Max(maximumContactError, Vector3.Distance(right.wrist.position, target));
                            if (!boss) minimumPlayerSide = Mathf.Min(minimumPlayerSide, right.wrist.position.x - bottle.position.x);
                        }
                        if (sample % 6 == 0)
                            minimumHandY = Mathf.Min(minimumHandY, CharacterIdleFactory.HandMinimumY(actor.gameObject, right.wrist));
                        poses[sample] = nodes.Select(t => new LocalPose(t)).ToArray();
                        bottlePoses[sample] = new LocalPose(bottle);
                    }
                    var actorClip = CreateClip((boss ? "Boss" : "Player") + "_ColaShake_" + strength);
                    for (var node = 0; node < nodes.Length; node++)
                    {
                        var indexOfNode = node;
                        WritePose(actorClip, paths[node], sample => poses[sample][indexOfNode]);
                    }
                    var bottleClip = CreateClip((boss ? "Boss" : "Player") + "_BottleMotion_" + strength);
                    WritePose(bottleClip, "", sample => bottlePoses[sample]);
                    Save(actorClip, ActorClipPath(boss, strength)); Save(bottleClip, BottleClipPath(boss, strength));
                    if (maximumContactError > .0001f) throw new InvalidOperationException("Baked wrist lost its grip: " + maximumContactError);
                    if (!boss && minimumPlayerSide < .06f) throw new InvalidOperationException("Player wrist crossed the bottle's right-side grip.");
                    report.AppendLine($"PASS: {(boss ? "Boss" : "Player")} strength {strength}, contact drift={maximumContactError * 1000:F4} mm, sampled hand minimumY={minimumHandY:F5}.");
                    if (!boss) report.AppendLine($"PASS: player wrist stays on the bottle's right side; minimum horizontal offset={minimumPlayerSide:F5}m.");
                }
                report.AppendLine($"{(boss ? "Boss" : "Player")} rest wrist={right.position:F4}; acquisition wrist={contactWrist:F4}; palm advance={palmAdvance:F4} m.");
            }
            finally
            {
                for (var node = 0; node < nodes.Length; node++) rest[node].Apply(nodes[node]);
                restBottle.Apply(bottle); animator.enabled = enabled;
            }
        }

        private static float Reach(float time) => Ease(time / .95f);
        private static float Hold(float time) => Ease((time - 1.15f) / .40f) * (1 - Ease((time - 2.8f) / .55f));

        private static void BottlePose(float time, int strength, bool boss, Vector3 rest, Quaternion rotation, out Vector3 position, out Quaternion orientation)
        {
            // Bring the shared bottle toward the owner and their right-hand side before shaking.
            var held = boss ? new Vector3(-.08f, .985f, .44f) : new Vector3(.12f, .960f, -.40f);
            var lift = Hold(time);
            position = Vector3.Lerp(rest, held, lift);
            orientation = Quaternion.Euler((boss ? -5 : 5) * lift, 0, (boss ? -3 : 3) * lift) * rotation;
            if (time < 1.55f || time > 2.65f) return;
            var phase = Mathf.InverseLerp(1.55f, 2.65f, time);
            var envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * phase), 2);
            var wave = Mathf.Sin(phase * Mathf.PI * 2 * (1.5f + .5f * strength)) * envelope;
            var amplitude = .012f + .008f * strength;
            position += new Vector3(.003f * wave, amplitude * wave, (boss ? -1 : 1) * amplitude * .65f * wave);
            orientation = Quaternion.Euler((boss ? -1 : 1) * wave * (6 + 3 * strength), 0, wave * 2) * orientation;
        }

        private static void WrapFingers(Transform actor, Vector3 bottlePosition, Quaternion motion, bool boss, float amount)
        {
            if (amount < .00001f) return;
            var prefixes = boss ? new[] { "f_index.", "f_middle.", "f_ring.", "f_pinky." } : new[] { "Index", "Middle", "Ring", "Little" };
            foreach (var prefix in prefixes)
                for (var segment = 0; segment < 3; segment++)
                {
                    var bone = Bone(actor, boss ? prefix + "0" + (segment + 1) + ".R" : prefix + (segment + 2) + ".R");
                    var hasChild = bone.childCount > 0;
                    var direction = hasChild ? bone.GetChild(0).position - bone.position : bone.position - bone.parent.position;
                    var length = hasChild ? direction.magnitude : .022f;
                    var local = Quaternion.Inverse(motion) * (bone.position - bottlePosition);
                    var radius = new Vector2(local.x, local.z).magnitude;
                    var cosine = Mathf.Clamp((radius * radius + FingerRadius * FingerRadius - length * length) / (2 * radius * FingerRadius), -1, 1);
                    var angle = Mathf.Atan2(local.z, local.x) + Mathf.Acos(cosine);
                    var target = bottlePosition + motion * new Vector3(Mathf.Cos(angle) * FingerRadius, local.y, Mathf.Sin(angle) * FingerRadius);
                    var desired = Quaternion.FromToRotation(direction, target - bone.position) * bone.rotation;
                    bone.rotation = Quaternion.Slerp(bone.rotation, desired, amount);
                }
            // The thumb opposes the fingers on the other side of the cylinder.
            foreach (var name in boss ? new[] { "thumb.02.R", "thumb.03.R" } : new[] { "Thumb2.R", "Thumb3.R" })
            {
                var bone = Bone(actor, name);
                var direction = bone.childCount > 0 ? bone.GetChild(0).position - bone.position : bone.position - bone.parent.position;
                var local = Quaternion.Inverse(motion) * (bone.position - bottlePosition);
                var target = bottlePosition + motion * new Vector3(boss ? -.035f : .035f, local.y, boss ? .065f : -.065f);
                var delta = Quaternion.FromToRotation(direction, target - bone.position);
                var limited = Quaternion.RotateTowards(Quaternion.identity, delta, 42);
                bone.rotation = Quaternion.Slerp(Quaternion.identity, limited, amount) * bone.rotation;
            }
        }

        private static AnimationClip CreateClip(string name)
        {
            var clip = new AnimationClip { name = name, frameRate = 30, legacy = false, wrapMode = WrapMode.Once };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false; settings.loopBlend = false;
            settings.keepOriginalPositionXZ = settings.keepOriginalPositionY = settings.keepOriginalOrientation = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        private static void WritePose(AnimationClip clip, string path, Func<int, LocalPose> get)
        {
            var quaternions = Enumerable.Range(0, Samples + 1).Select(s => get(s).rotation).ToArray();
            for (var sample = 1; sample <= Samples; sample++)
                if (Quaternion.Dot(quaternions[sample - 1], quaternions[sample]) < 0)
                { var q = quaternions[sample]; quaternions[sample] = new Quaternion(-q.x, -q.y, -q.z, -q.w); }
            for (var axis = 0; axis < 3; axis++)
            { var component = axis; Curve(clip, path, "m_LocalPosition." + "xyz"[axis], sample => get(sample).position[component]); }
            for (var axis = 0; axis < 4; axis++)
            { var component = axis; Curve(clip, path, "m_LocalRotation." + "xyzw"[axis], sample => quaternions[sample][component]); }
            clip.EnsureQuaternionContinuity();
        }

        private static void Curve(AnimationClip clip, string path, string property, Func<int, float> value)
        {
            var first = value(0); var animated = Enumerable.Range(1, Samples).Any(sample => Mathf.Abs(value(sample) - first) > .000001f);
            Keyframe[] keys;
            if (!animated) keys = new[] { new Keyframe(0, first), new Keyframe(Duration, first) };
            else
            {
                keys = new Keyframe[Samples + 1]; var dt = Duration / Samples;
                for (var sample = 0; sample <= Samples; sample++)
                {
                    var tangent = sample == 0 || sample == Samples ? 0 : (value(sample + 1) - value(sample - 1)) / (2 * dt);
                    keys[sample] = new Keyframe(sample * dt, value(sample), tangent, tangent);
                }
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), new AnimationCurve(keys));
        }

        private static void Save(AnimationClip clip, string path)
        {
            var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (saved) { EditorUtility.CopySerialized(clip, saved); UnityEngine.Object.DestroyImmediate(clip); EditorUtility.SetDirty(saved); }
            else AssetDatabase.CreateAsset(clip, path);
        }
        private static float Ease(float value) { var t = Mathf.Clamp01(value); return t * t * t * (10 + t * (-15 + 6 * t)); }
        private static Transform Bone(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        private static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
    }
}
