using System.Collections.Generic;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Extends only the imported sleeve's shoulder end, leaving elbow/wrist bones and hand geometry intact.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaFpsSleeveExtension : MonoBehaviour
    {
        private readonly List<SkinnedMeshRenderer> skins = new();
        private readonly List<Mesh> originals = new(), generated = new();
        public static void EnsureFor(Animator actor)
        {
            if (!actor || !Application.IsPlaying(actor.gameObject)) return;
            var extension = actor.GetComponent<ColaFpsSleeveExtension>();
            if (!extension) extension = actor.gameObject.AddComponent<ColaFpsSleeveExtension>();
            extension.Apply();
        }
        private void Apply()
        {
            if (generated.Count > 0) return;
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var source = skin.sharedMesh;
                if (!source || !source.isReadable) continue;
                var vertices = source.vertices;
                var weights = source.boneWeights;
                var bind = source.bindposes;
                if (weights.Length != vertices.Length) continue;
                bool changed = false;
                for (int boneIndex = 0; boneIndex < skin.bones.Length && boneIndex < bind.Length; boneIndex++)
                {
                    var bone = skin.bones[boneIndex];
                    if (!bone || (bone.name != "UpperArm.R" && bone.name != "UpperArm.L")) continue;
                    Transform elbow = null;
                    foreach (var child in bone.GetComponentsInChildren<Transform>())
                        if (child.name == (bone.name.EndsWith("R") ? "LowArm.R" : "LowArm.L")) { elbow = child; break; }
                    if (!elbow) continue;
                    float unit = bone.TransformVector(Vector3.up).magnitude;
                    float upperLength = Vector3.Distance(bone.position, elbow.position) / Mathf.Max(.0001f, unit);
                    // Extend the existing shoulder vertices, rather than adding a visible separate tube or stretching the wrist.
                    Vector3 extension = bind[boneIndex].inverse.MultiplyVector(Vector3.down * (.42f / Mathf.Max(.0001f, unit)));
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var w = weights[i];
                        float weight = (w.boneIndex0 == boneIndex ? w.weight0 : 0) + (w.boneIndex1 == boneIndex ? w.weight1 : 0) +
                            (w.boneIndex2 == boneIndex ? w.weight2 : 0) + (w.boneIndex3 == boneIndex ? w.weight3 : 0);
                        if (weight < .5f) continue;
                        float y = bind[boneIndex].MultiplyPoint3x4(vertices[i]).y;
                        float amount = Mathf.SmoothStep(0, 1, 1 - y / Mathf.Max(.0001f, upperLength * .55f));
                        if (amount <= 0) continue;
                        vertices[i] += extension * amount; changed = true;
                    }
                }
                if (!changed) continue;
                var mesh = Instantiate(source);
                mesh.name = source.name + " / seated sleeve"; mesh.hideFlags = HideFlags.DontSave;
                mesh.vertices = vertices; mesh.RecalculateBounds(); mesh.RecalculateNormals();
                skins.Add(skin); originals.Add(source); generated.Add(mesh);
                skin.sharedMesh = mesh;
                var bounds = skin.localBounds; bounds.Encapsulate(mesh.bounds); skin.localBounds = bounds;
            }
        }
        private void OnDestroy()
        {
            for (int i = 0; i < generated.Count; i++)
            {
                if (skins[i] && skins[i].sharedMesh == generated[i]) skins[i].sharedMesh = originals[i];
                if (generated[i]) Destroy(generated[i]);
            }
        }
    }
}
