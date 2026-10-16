using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ColaCola.Editor
{
    /// <summary>Bakes supported seated animation into ordinary clips; no runtime IK dependency.</summary>
    public static class CharacterIdleFactory
    {
        public const string Folder = "Assets/_COLA_COLA/Animations/Idle";
        public const string BossName = "Boss_ExistingRig_SeatedBlockPose";
        public const string ArmsName = "PlayerArms_ExistingRig_TablePose";
        public const float Duration = 30f;
        private const int Samples = 360;
        private const float TableHeight = .83f;

        [Serializable] private class LocalPose
        {
            public string path;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }
        [Serializable] private class ReferencePose { public int revision; public LocalPose[] nodes; }

        private sealed class ArmSupport
        {
            public Transform upper, elbow, wrist;
            public Vector3 target, pole;
            public Quaternion rotation;
            public ArmSupport(GameObject root, string side, bool boss)
            {
                upper = Bone(root, (boss ? "upper_arm." : "UpperArm.") + side);
                elbow = Bone(root, (boss ? "forearm." : "LowArm.") + side);
                wrist = Bone(root, (boss ? "hand." : "Wrist.") + side);
                target = wrist.position;
                rotation = wrist.rotation;
                pole = elbow.position;
            }
            public void Solve() { SolveArm(upper, elbow, wrist, target, pole, rotation); }
        }

        public static void Apply(GameObject sceneRoot)
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var report = new StringBuilder("Natural seated Idle: 30 seconds, six unequal breaths, supported hands, occasional attention/posture changes.\n");
            Build(sceneRoot.GetComponentsInChildren<Transform>(true).Single(t => t.name == BossName).gameObject, "Boss_SeatedIdle", true, report);
            Build(sceneRoot.GetComponentsInChildren<Transform>(true).Single(t => t.name == ArmsName).gameObject, "PlayerArms_TableIdle", false, report);
            AssetDatabase.SaveAssets();
            File.WriteAllText(MainSceneTools.Output + "/NaturalIdle-authoring-validation.txt", report.ToString());
        }

        public static void RebuildBossOnly(GameObject sceneRoot)
        {
            var report = new StringBuilder("More active seated boss Idle with supported hands.\n");
            Build(sceneRoot.GetComponentsInChildren<Transform>(true).Single(t => t.name == BossName).gameObject,
                "Boss_SeatedIdle", true, report);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("output/presentation-motion");
            File.WriteAllText("output/presentation-motion/Boss-authoring.txt", report.ToString());
        }

        private static Transform Bone(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        }

        private static ReferencePose Snapshot(GameObject root)
        {
            return new ReferencePose { revision = 2, nodes = root.GetComponentsInChildren<Transform>(true).Where(t => t != root.transform)
                .Select(t => new LocalPose { path = AnimationUtility.CalculateTransformPath(t, root.transform),
                    position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray() };
        }

        private static void Restore(Transform root, ReferencePose pose)
        {
            foreach (var entry in pose.nodes)
            {
                var bone = root.Find(entry.path);
                if (!bone) throw new InvalidOperationException("Reference-pose path no longer exists: " + entry.path);
                bone.localPosition = entry.position;
                bone.localRotation = entry.rotation;
                bone.localScale = entry.scale;
            }
        }

        private static void PrepareSupportedRest(GameObject character, string name, bool boss)
        {
            // Calibrated pose prevents cumulative finger curling when the tool is applied again.
            string referencePath = Folder + "/" + name + "_SupportedReference.json";
            if (File.Exists(referencePath))
            {
                var previous = JsonUtility.FromJson<ReferencePose>(File.ReadAllText(referencePath));
                Restore(character.transform, previous);
                if (previous.revision < 2 && boss)
                {
                    foreach (string side in new[] { "L", "R" })
                    {
                        RelaxThumb(character, side);
                        var support = new ArmSupport(character, side, true);
                        support.target += Vector3.up * (TableHeight + .003f - HandMinimumY(character, support.wrist));
                        support.Solve();
                    }
                    File.WriteAllText(referencePath, JsonUtility.ToJson(Snapshot(character), true));
                }
                return;
            }
            foreach (string side in new[] { "L", "R" })
            {
                var support = new ArmSupport(character, side, boss);
                if (boss)
                {
                    support.target = side == "L" ? new Vector3(.34f, .88f, .62f) : new Vector3(-.16f, .88f, .67f);
                    support.pole = new Vector3(support.upper.position.x + (side == "L" ? .025f : -.025f), .875f, 1.015f);
                    support.Solve();
                }
                FlattenHand(character, support.wrist, side, boss);
                if (boss)
                {
                    RelaxThumb(character, side);
                    string[] fingers = { "f_index", "f_middle", "f_ring", "f_pinky" };
                    for (int finger = 0; finger < fingers.Length; finger++)
                    {
                        Curl(Bone(character, fingers[finger] + ".01." + side), 5 + finger * 1.3f);
                        Curl(Bone(character, fingers[finger] + ".02." + side), 9 + finger * 1.5f);
                        Curl(Bone(character, fingers[finger] + ".03." + side), 4 + finger);
                    }
                    Curl(Bone(character, "thumb.02." + side), 5);
                }
                else
                {
                    // *1 bones include the palm fan; bend the actual knuckles (*2/*3).
                    string[] fingers = { "Index", "Middle", "Ring", "Little" };
                    for (int finger = 0; finger < fingers.Length; finger++)
                    {
                        Curl(Bone(character, fingers[finger] + "2." + side), 4 + finger * 1.2f);
                        Curl(Bone(character, fingers[finger] + "3." + side), 6 + finger * 1.1f);
                        Curl(Bone(character, fingers[finger] + "4." + side), 2.5f);
                    }
                    Curl(Bone(character, "Thumb2." + side), 3);
                }
                // Seat the actual lowest hand surface 3 mm above the table, using skinned vertices.
                support.rotation = support.wrist.rotation;
                support.target = support.wrist.position;
                float handMinimum = HandMinimumY(character, support.wrist);
                float adjustment = TableHeight + .003f - handMinimum;
                Debug.Log($"COLA_HAND_CALIBRATION {character.name}/{side}: wrist={support.wrist.position:F5}, handMinY={handMinimum:F5}, adjustment={adjustment:F5}");
                if (Mathf.Abs(adjustment) > .08f)
                {
                    MainSceneTools.Capture(Camera.main, MainSceneTools.Output + "/NaturalIdle-calibration-probe.png");
                    throw new InvalidOperationException($"Hand contact calibration exceeded 8 cm: {character.name}/{side}; wrist={support.wrist.position:F5}, minimum={handMinimum:F5}, adjustment={adjustment:F5}");
                }
                support.target += Vector3.up * adjustment;
                support.Solve();
            }
            File.WriteAllText(referencePath, JsonUtility.ToJson(Snapshot(character), true));
            foreach (var bone in character.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
            AssetDatabase.Refresh();
        }

        private static void FlattenHand(GameObject character, Transform wrist, string side, bool boss)
        {
            var index = Bone(character, (boss ? "f_index.01." : "Index2.") + side);
            var little = Bone(character, (boss ? "f_pinky.01." : "Little2.") + side);
            var middle = Bone(character, (boss ? "f_middle.01." : "Middle2.") + side);
            var normal = Vector3.Cross(middle.position - wrist.position, index.position - little.position).normalized;
            if (Vector3.Dot(normal, Vector3.up) < 0) normal = -normal;
            wrist.rotation = Quaternion.FromToRotation(normal, Vector3.up) * wrist.rotation;
        }

        private static void Curl(Transform bone, float degrees)
        {
            if (bone.childCount == 0) return;
            Vector3 axis = Vector3.Cross(bone.GetChild(0).position - bone.position, Vector3.down).normalized;
            if (axis.sqrMagnitude > .1f) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }

        private static void RelaxThumb(GameObject character, string side)
        {
            // The imported rest thumb points down out of the palm. Bring it alongside
            // the fingers so the whole hand can rest, rather than balancing on the thumb tip.
            foreach (string segment in new[] { "thumb.01.", "thumb.02." })
            {
                var bone = Bone(character, segment + side);
                var direction = (bone.GetChild(0).position - bone.position).normalized;
                var relaxed = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                relaxed.y = -.08f;
                bone.rotation = Quaternion.FromToRotation(direction, relaxed.normalized) * bone.rotation;
            }
        }

        internal static float HandMinimumY(GameObject character, Transform wrist)
        {
            float minimum = float.PositiveInfinity;
            var handBones = new HashSet<Transform>(wrist.GetComponentsInChildren<Transform>(true));
            foreach (var renderer in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var weights = renderer.sharedMesh.boneWeights;
                if (weights.Length == 0) continue;
                var indices = new HashSet<int>(Enumerable.Range(0, renderer.bones.Length).Where(i => handBones.Contains(renderer.bones[i])));
                if (indices.Count == 0) continue;
                // Evaluate skinning in world space. Imported FBXs have 100x renderer
                // scale; BakeMesh(false) + TransformPoint would apply that scale twice.
                var vertices = renderer.sharedMesh.vertices;
                var bindposes = renderer.sharedMesh.bindposes;
                var skinMatrices = renderer.bones.Select((bone, i) => bone.localToWorldMatrix * bindposes[i]).ToArray();
                for (int i = 0; i < vertices.Length; i++)
                {
                    var w = weights[i];
                    float influence = (indices.Contains(w.boneIndex0) ? w.weight0 : 0) + (indices.Contains(w.boneIndex1) ? w.weight1 : 0) +
                                      (indices.Contains(w.boneIndex2) ? w.weight2 : 0) + (indices.Contains(w.boneIndex3) ? w.weight3 : 0);
                    if (influence > .5f)
                    {
                        Vector3 point = skinMatrices[w.boneIndex0].MultiplyPoint3x4(vertices[i]) * w.weight0 +
                                        skinMatrices[w.boneIndex1].MultiplyPoint3x4(vertices[i]) * w.weight1 +
                                        skinMatrices[w.boneIndex2].MultiplyPoint3x4(vertices[i]) * w.weight2 +
                                        skinMatrices[w.boneIndex3].MultiplyPoint3x4(vertices[i]) * w.weight3;
                        minimum = Mathf.Min(minimum, point.y);
                    }
                }
            }
            if (float.IsInfinity(minimum)) throw new InvalidOperationException("No skinned hand vertices: " + wrist.name);
            return minimum;
        }

        private static void Build(GameObject character, string name, bool boss, StringBuilder report)
        {
            PrepareSupportedRest(character, name, boss);
            var rest = Snapshot(character);
            var bones = rest.nodes.Select(n => character.transform.Find(n.path)).ToArray();
            var supports = new[] { new ArmSupport(character, "L", boss), new ArmSupport(character, "R", boss) };
            var frames = new LocalPose[Samples + 1][];
            float maxHandError = 0, maxHandRotationError = 0;
            for (int sample = 0; sample <= Samples; sample++)
            {
                Restore(character.transform, rest);
                float time = sample == Samples ? 0 : sample * Duration / Samples;
                AnimateBody(character, time, boss);
                foreach (var arm in supports)
                {
                    arm.Solve();
                    maxHandError = Mathf.Max(maxHandError, Vector3.Distance(arm.wrist.position, arm.target));
                    maxHandRotationError = Mathf.Max(maxHandRotationError, Quaternion.Angle(arm.wrist.rotation, arm.rotation));
                }
                AnimateFingerAdjustment(character, time, boss);
                frames[sample] = bones.Select(t => new LocalPose { position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
            }
            Restore(character.transform, rest);
            if (maxHandError > .0001f || maxHandRotationError > .06f) throw new InvalidOperationException("Table hand support was lost: " + character.name);
            var clip = new AnimationClip { name = name, frameRate = 30, legacy = false, wrapMode = WrapMode.Loop };
            for (int bone = 0; bone < bones.Length; bone++)
            {
                int index = bone;
                for (int axis = 0; axis < 3; axis++)
                {
                    int component = axis;
                    SetCurve(clip, rest.nodes[bone].path, "m_LocalPosition." + "xyz"[axis], s => frames[s][index].position[component]);
                    SetCurve(clip, rest.nodes[bone].path, "m_LocalScale." + "xyz"[axis], s => frames[s][index].scale[component]);
                }
                Quaternion previous = frames[0][bone].rotation;
                for (int s = 1; s <= Samples; s++)
                {
                    Quaternion q = frames[s][bone].rotation;
                    if (Quaternion.Dot(previous, q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    frames[s][bone].rotation = previous = q;
                }
                for (int axis = 0; axis < 4; axis++)
                {
                    int component = axis;
                    SetCurve(clip, rest.nodes[bone].path, "m_LocalRotation." + "xyzw"[axis], s => frames[s][index].rotation[component]);
                }
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = false;
            settings.keepOriginalPositionXZ = settings.keepOriginalPositionY = settings.keepOriginalOrientation = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.EnsureQuaternionContinuity();
            SaveClipAndController(character, name, clip);
            report.AppendLine($"PASS: {name}: {Duration:F0}s, {Samples + 1} baked supported poses; hand drift {maxHandError * 1000:F4} mm, orientation drift {maxHandRotationError:F4} degrees.");
            report.AppendLine("Hands minimum world Y: " + string.Join(", ", supports.Select(s => HandMinimumY(character, s.wrist).ToString("F5"))) + "; tabletop Y=.83000.");
        }

        private static void AnimateBody(GameObject character, float time, bool boss)
        {
            float breath = Breath(time, boss);
            float shift = Pulse(time, 7.2f, 9.3f, 12.6f, 16.0f) - .65f * Pulse(time, 21f, 23.2f, 25f, 28.8f);
            if (boss)
            {
                RotateWorld(Bone(character, "spine.001"), new Vector3(.30f * breath, .90f * shift, -1.35f * shift));
                RotateWorld(Bone(character, "spine.002"), new Vector3(.65f * breath, .40f * shift, -.65f * shift));
                RotateWorld(Bone(character, "spine.003"), new Vector3(.75f * breath, 0, -.45f * shift));
                Bone(character, "spine.003").position += Vector3.up * (.0035f * breath);
                float down = 4.4f * Pulse(time, 1.6f, 3.0f, 4.1f, 6.2f) + 2.2f * Pulse(time, 23.8f, 25.1f, 26.2f, 28.1f);
                float glance = 5.2f * Pulse(time, 10.8f, 12.1f, 13.3f, 15.7f) - 2.5f * Pulse(time, 18f, 19.2f, 20.2f, 22f);
                RotateWorld(Bone(character, "spine.004"), new Vector3(-down - .65f * breath, glance - .50f * shift, 1.35f * shift));
                RotateWorld(Bone(character, "spine.005"), new Vector3(-.30f * down, .22f * glance, .45f * shift));
            }
            else
            {
                Bone(character, "UpperArm.L").position += new Vector3(.0010f * shift, .0018f * breath, -.0015f * breath);
                Bone(character, "UpperArm.R").position += new Vector3(.0006f * shift, .0014f * breath, -.0012f * breath);
            }
        }

        private static void AnimateFingerAdjustment(GameObject character, float time, bool boss)
        {
            if (boss) Curl(Bone(character, "f_index.02.R"), -2.8f * Pulse(time, 17.3f, 18.2f, 19.0f, 20.7f));
            else
            {
                Curl(Bone(character, "Index3.R"), -2.8f * Pulse(time, 11.0f, 12.1f, 13.2f, 15.5f));
                Curl(Bone(character, "Thumb2.L"), -2.3f * Pulse(time, 20.0f, 21.5f, 23.2f, 25.9f));
            }
        }

        private static float Breath(float time, bool boss)
        {
            float[] marks = boss ? new[] { 0f, 4.7f, 9.9f, 15.6f, 20.4f, 25.5f, Duration } : new[] { 0f, 5.4f, 10.1f, 14.7f, 20.3f, 25.0f, Duration };
            float[] depth = { .87f, 1.0f, .80f, .94f, .85f, .91f };
            int i = 0;
            while (i < marks.Length - 2 && time >= marks[i + 1]) i++;
            float phase = Mathf.InverseLerp(marks[i], marks[i + 1], time);
            return depth[i] * (phase < .36f ? Ease(phase / .36f) : 1 - Ease((phase - .36f) / .55f));
        }

        private static float Ease(float value)
        {
            float v = Mathf.Clamp01(value);
            return v * v * v * (10 + v * (-15 + 6 * v));
        }

        private static float Pulse(float time, float begin, float arrive, float leave, float end)
        {
            if (time <= begin || time >= end) return 0;
            if (time < arrive) return Ease((time - begin) / (arrive - begin));
            return time < leave ? 1 : 1 - Ease((time - leave) / (end - leave));
        }

        private static void RotateWorld(Transform bone, Vector3 degrees)
        {
            bone.rotation = Quaternion.Euler(degrees) * bone.rotation;
        }

        private static void SolveArm(Transform upper, Transform elbow, Transform wrist, Vector3 target, Vector3 pole, Quaternion handRotation)
        {
            Vector3 start = upper.position;
            float a = Vector3.Distance(start, elbow.position), b = Vector3.Distance(elbow.position, wrist.position);
            Vector3 direction = (target - start).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(start, target), Mathf.Abs(a - b) + .00001f, a + b - .00001f);
            Vector3 bend = Vector3.ProjectOnPlane(pole - start, direction).normalized;
            if (bend.sqrMagnitude < .1f) bend = Vector3.ProjectOnPlane(Vector3.down, direction).normalized;
            float along = (a * a - b * b + distance * distance) / (2 * distance);
            Vector3 desiredElbow = start + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(elbow.position - start, desiredElbow - start) * upper.rotation;
            elbow.rotation = Quaternion.FromToRotation(wrist.position - elbow.position, target - elbow.position) * elbow.rotation;
            wrist.rotation = handRotation;
        }

        private static void SetCurve(AnimationClip clip, string path, string property, Func<int, float> value)
        {
            float first = value(0), dt = Duration / Samples;
            bool animated = Enumerable.Range(1, Samples).Any(s => Mathf.Abs(value(s) - first) > .000001f);
            if (!animated)
            {
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), new AnimationCurve(new Keyframe(0, first), new Keyframe(Duration, first)));
                return;
            }
            var keys = new Keyframe[Samples + 1];
            for (int s = 0; s <= Samples; s++)
            {
                int before = s == 0 || s == Samples ? Samples - 1 : s - 1;
                int after = s == 0 || s == Samples ? 1 : s + 1;
                float tangent = (value(after) - value(before)) / (2 * dt);
                keys[s] = new Keyframe(s * dt, s == Samples ? first : value(s), tangent, tangent);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), new AnimationCurve(keys));
        }

        private static void SaveClipAndController(GameObject character, string name, AnimationClip clip)
        {
            string path = Folder + "/" + name + ".anim";
            var savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (savedClip)
            {
                EditorUtility.CopySerialized(clip, savedClip);
                UnityEngine.Object.DestroyImmediate(clip);
                EditorUtility.SetDirty(savedClip);
            }
            else { AssetDatabase.CreateAsset(clip, path); savedClip = clip; }
            string controllerPath = Folder + "/" + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.FirstOrDefault(s => s.state.name == "Idle").state;
            if (!state) state = machine.AddState("Idle", new Vector3(280, 80));
            state.motion = savedClip;
            state.speed = 1;
            state.writeDefaultValues = false;
            machine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(controller);
            var animator = character.GetComponent<Animator>();
            if (!animator) animator = character.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.enabled = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        }
    }
}
