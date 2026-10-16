using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColaCola.Presentation;
using UnityEditor;
using UnityEngine;

namespace ColaCola.Editor
{
    /// <summary>Finds the stock right-arm shoulder rim without changing the imported mesh or its read/write setting.</summary>
    public static class PlayerSleeveContinuationFactory
    {
        private sealed class Edge
        {
            public int a, b, sourceA, sourceB, count;
        }

        private sealed class SourceData
        {
            public Vector3[] vertices;
            public BoneWeight[] weights;
            public Matrix4x4[] bind;
            public int[] triangles;
        }
        private static readonly Dictionary<Mesh, SourceData> sourceData = new Dictionary<Mesh, SourceData>();

        private static SourceData Read(Mesh mesh)
        {
            if (!sourceData.TryGetValue(mesh, out var data))
            {
                data = new SourceData { vertices = mesh.vertices, weights = mesh.boneWeights, bind = mesh.bindposes, triangles = mesh.triangles };
                sourceData.Add(mesh, data);
            }
            return data;
        }

        public static PlayerSleeveContinuation Apply(GameObject sceneRoot)
        {
            sourceData.Clear();
            var actor = sceneRoot.GetComponentsInChildren<Transform>(true).Single(t => t.name == CharacterIdleFactory.ArmsName);
            var upper = actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == "UpperArm.R");
            var candidates = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => Array.IndexOf(r.bones, upper) >= 0).ToArray();
            if (candidates.Length == 0) throw new InvalidOperationException("No skinned right-arm mesh was found.");
            SkinnedMeshRenderer selected = null;
            int[] chosen = null;
            float best = float.NegativeInfinity;
            bool usedBoundary = false;
            foreach (var renderer in candidates)
            {
                var mesh = renderer.sharedMesh;
                int upperIndex = Array.IndexOf(renderer.bones, upper);
                foreach (var ring in BoundaryRings(mesh))
                {
                    var weights = Read(mesh).weights;
                    float influence = ring.Average(i => Influence(weights[i], upperIndex));
                    var center = ring.Select(i => Skin(renderer, mesh, i)).Aggregate(Vector3.zero, (a, b) => a + b) / ring.Length;
                    float score = influence * 10 - Vector3.Distance(center, upper.position);
                    if (influence < .55f || score <= best) continue;
                    best = score; selected = renderer; chosen = ring; usedBoundary = true;
                }
            }
            if (!selected)
            {
                // Some versions of the stock model cap this same shoulder plane. Continue its outer rim instead.
                foreach (var renderer in candidates)
                {
                    var mesh = renderer.sharedMesh;
                    int upperIndex = Array.IndexOf(renderer.bones, upper);
                    var axis = (upper.GetChild(0).position - upper.position).normalized;
                    var weights = Read(mesh).weights;
                    var points = Enumerable.Range(0, mesh.vertexCount).Where(i => Influence(weights[i], upperIndex) > .8f)
                        .Select(i => new { index = i, point = Skin(renderer, mesh, i) }).ToArray();
                    if (points.Length < 3) continue;
                    float proximal = points.Min(p => Vector3.Dot(p.point - upper.position, axis));
                    var rim = points.Where(p => Vector3.Dot(p.point - upper.position, axis) < proximal + .004f)
                        .GroupBy(p => Quantize(p.point)).Select(g => g.First()).ToArray();
                    if (rim.Length < 3) continue;
                    var center = rim.Aggregate(Vector3.zero, (sum, p) => sum + p.point) / rim.Length;
                    var outer = rim.Where(p => Vector3.ProjectOnPlane(p.point - center, axis).magnitude > .01f).Select(p => p.index).ToArray();
                    if (outer.Length < 3) continue;
                    selected = renderer; chosen = outer; break;
                }
            }
            if (!selected || chosen == null) throw new InvalidOperationException("The stock right sleeve has no usable proximal rim.");
            var selectedMesh = selected.sharedMesh;
            var restPoints = chosen.Select(i => Skin(selected, selectedMesh, i)).ToArray();
            var restCenter = restPoints.Aggregate(Vector3.zero, (sum, p) => sum + p) / restPoints.Length;
            var direction = (upper.GetChild(0).position - upper.position).normalized;
            var basisX = Vector3.ProjectOnPlane(Vector3.right, direction).normalized;
            if (basisX.sqrMagnitude < .1f) basisX = Vector3.ProjectOnPlane(Vector3.up, direction).normalized;
            var basisY = Vector3.Cross(direction, basisX);
            chosen = chosen.OrderBy(i =>
            {
                var offset = Skin(selected, selectedMesh, i) - restCenter;
                return Mathf.Atan2(Vector3.Dot(offset, basisY), Vector3.Dot(offset, basisX));
            }).ToArray();
            var group = sceneRoot.transform.Find("08_Devices");
            var node = group.Find("PlayerSleeveContinuation");
            if (!node) { node = new GameObject("PlayerSleeveContinuation").transform; node.SetParent(group, false); }
            var continuation = node.GetComponent<PlayerSleeveContinuation>();
            if (!continuation) continuation = node.gameObject.AddComponent<PlayerSleeveContinuation>();
            var positions = selectedMesh.vertices;
            var skinWeights = selectedMesh.boneWeights;
            var uvs = selectedMesh.uv;
            continuation.source = selected;
            continuation.anchorFrame = actor;
            continuation.bindPoses = selectedMesh.bindposes;
            continuation.rim = chosen.Select(i =>
            {
                var weight = skinWeights[i];
                return new PlayerSleeveContinuation.RimVertex
                {
                    sourceIndex = i, bindPosition = positions[i], uv = uvs.Length > i ? uvs[i] : Vector2.zero,
                    boneIndices = new Vector4(weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3),
                    weights = new Vector4(weight.weight0, weight.weight1, weight.weight2, weight.weight3)
                };
            }).ToArray();
            continuation.restCenterLocal = actor.InverseTransformPoint(restCenter);
            continuation.hiddenAnchorLocal = actor.InverseTransformPoint(upper.position + new Vector3(0, -.025f, -.42f));
            float radius = chosen.Max(i => Vector3.Distance(Skin(selected, selectedMesh, i), restCenter));
            continuation.edgeGuideLocal = actor.InverseTransformPoint(new Vector3(.43f, Mathf.Max(.97f, .83f + radius + .13f), -1.04f));
            continuation.anchorOffsetsLocal = chosen.Select(i => actor.InverseTransformVector(Skin(selected, selectedMesh, i) - restCenter)).ToArray();
            var targetRenderer = node.GetComponent<MeshRenderer>();
            targetRenderer.sharedMaterial = selected.sharedMaterials[0];
            targetRenderer.shadowCastingMode = selected.shadowCastingMode;
            targetRenderer.receiveShadows = selected.receiveShadows;
            targetRenderer.lightProbeUsage = selected.lightProbeUsage;
            targetRenderer.reflectionProbeUsage = selected.reflectionProbeUsage;
            continuation.RefreshGeometry();
            EditorUtility.SetDirty(continuation);
            Directory.CreateDirectory("output/shake-animation");
            File.WriteAllText("output/shake-animation/Sleeve-continuation-authoring.txt",
                $"Source: {selected.name}; {(usedBoundary ? "open welded boundary" : "capped proximal plane")}; rim vertices: {chosen.Length}; radius: {radius:F5}m.\n" +
                $"Rest center: {restCenter:F5}; hidden anchor: {actor.TransformPoint(continuation.hiddenAnchorLocal):F5}; initial seam error: {continuation.MaximumRimError:F8}m.\n" +
                "Original FBX, importer readability, materials, actor hierarchy and Idle assets were not modified.\n");
            return continuation;
        }

        private static IEnumerable<int[]> BoundaryRings(Mesh mesh)
        {
            var vertices = Read(mesh).vertices;
            var welded = new Dictionary<Vector3Int, int>();
            var ids = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var key = Quantize(vertices[i]);
                if (!welded.TryGetValue(key, out int id)) { id = welded.Count; welded.Add(key, id); }
                ids[i] = id;
            }
            var edges = new Dictionary<(int, int), Edge>();
            var triangles = Read(mesh).triangles;
            for (int i = 0; i < triangles.Length; i += 3)
                for (int side = 0; side < 3; side++)
                {
                    int a = triangles[i + side], b = triangles[i + (side + 1) % 3];
                    int wa = ids[a], wb = ids[b];
                    if (wa == wb) continue;
                    var key = wa < wb ? (wa, wb) : (wb, wa);
                    if (!edges.TryGetValue(key, out var edge))
                    { edge = new Edge { a = wa, b = wb, sourceA = a, sourceB = b }; edges.Add(key, edge); }
                    edge.count++;
                }
            var boundary = edges.Values.Where(e => e.count == 1).ToArray();
            var neighbors = new Dictionary<int, HashSet<int>>();
            var sourceIndices = new Dictionary<int, int>();
            foreach (var edge in boundary)
            {
                if (!neighbors.ContainsKey(edge.a)) neighbors[edge.a] = new HashSet<int>();
                if (!neighbors.ContainsKey(edge.b)) neighbors[edge.b] = new HashSet<int>();
                neighbors[edge.a].Add(edge.b); neighbors[edge.b].Add(edge.a);
                sourceIndices[edge.a] = edge.sourceA; sourceIndices[edge.b] = edge.sourceB;
            }
            var visited = new HashSet<int>();
            foreach (var start in neighbors.Keys)
            {
                if (visited.Contains(start)) continue;
                var pending = new Stack<int>(); var component = new List<int>(); pending.Push(start);
                bool closed = true;
                while (pending.Count > 0)
                {
                    int current = pending.Pop();
                    if (!visited.Add(current)) continue;
                    component.Add(current); closed &= neighbors[current].Count == 2;
                    foreach (int next in neighbors[current]) if (!visited.Contains(next)) pending.Push(next);
                }
                if (closed && component.Count >= 3 && component.Count <= 64)
                    yield return component.Select(id => sourceIndices[id]).ToArray();
            }
        }

        private static Vector3Int Quantize(Vector3 point) => Vector3Int.RoundToInt(point * 1000000);
        private static float Influence(BoneWeight weight, int bone) =>
            (weight.boneIndex0 == bone ? weight.weight0 : 0) + (weight.boneIndex1 == bone ? weight.weight1 : 0) +
            (weight.boneIndex2 == bone ? weight.weight2 : 0) + (weight.boneIndex3 == bone ? weight.weight3 : 0);

        private static Vector3 Skin(SkinnedMeshRenderer renderer, Mesh mesh, int index)
        {
            var data = Read(mesh);
            var weight = data.weights[index]; var vertex = data.vertices[index]; var bind = data.bind; var bones = renderer.bones;
            return (bones[weight.boneIndex0].localToWorldMatrix * bind[weight.boneIndex0]).MultiplyPoint3x4(vertex) * weight.weight0 +
                   (bones[weight.boneIndex1].localToWorldMatrix * bind[weight.boneIndex1]).MultiplyPoint3x4(vertex) * weight.weight1 +
                   (bones[weight.boneIndex2].localToWorldMatrix * bind[weight.boneIndex2]).MultiplyPoint3x4(vertex) * weight.weight2 +
                   (bones[weight.boneIndex3].localToWorldMatrix * bind[weight.boneIndex3]).MultiplyPoint3x4(vertex) * weight.weight3;
        }
    }
}
