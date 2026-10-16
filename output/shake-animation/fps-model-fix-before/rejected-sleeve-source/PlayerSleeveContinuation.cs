using System;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Continues the imported viewmodel's open shoulder rim back to the hidden player body.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(2000)]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PlayerSleeveContinuation : MonoBehaviour
    {
        [Serializable]
        public struct RimVertex
        {
            public int sourceIndex;
            public Vector3 bindPosition;
            public Vector2 uv;
            public Vector4 boneIndices, weights;
        }

        public SkinnedMeshRenderer source;
        public Transform anchorFrame;
        public RimVertex[] rim = Array.Empty<RimVertex>();
        public Matrix4x4[] bindPoses = Array.Empty<Matrix4x4>();
        public Vector3 restCenterLocal, hiddenAnchorLocal, edgeGuideLocal;
        public Vector3[] anchorOffsetsLocal = Array.Empty<Vector3>();
        public int RingVertexCount => rim != null ? rim.Length : 0;
        public float MaximumRimError { get; private set; }

        private const int Rings = 5;
        private Mesh ownedMesh;
        private Vector3[] worldRim, vertices;
        private Vector2[] uv;
        private int[] triangles;
        private Matrix4x4[] skinMatrices;
        private Vector3[] ringCenters;

        private void OnEnable() => RefreshGeometry();
        private void LateUpdate() => RefreshGeometry();

        /// <summary>Call after sampling a character clip when producing an editor preview.</summary>
        public void RefreshGeometry()
        {
            int count = RingVertexCount;
            if (!source || !anchorFrame || count < 3 || anchorOffsetsLocal == null || anchorOffsetsLocal.Length != count ||
                bindPoses == null || bindPoses.Length != source.bones.Length) return;
            EnsureMesh(count);
            var bones = source.bones;
            for (int bone = 0; bone < bones.Length; bone++)
                skinMatrices[bone] = bones[bone] ? bones[bone].localToWorldMatrix * bindPoses[bone] : Matrix4x4.identity;
            var center = Vector3.zero;
            for (int vertex = 0; vertex < count; vertex++)
            {
                var sample = rim[vertex];
                var point = Vector3.zero;
                for (int influence = 0; influence < 4; influence++)
                {
                    float weight = sample.weights[influence];
                    int bone = (int)sample.boneIndices[influence];
                    if (weight > 0 && bone >= 0 && bone < skinMatrices.Length)
                        point += skinMatrices[bone].MultiplyPoint3x4(sample.bindPosition) * weight;
                }
                worldRim[vertex] = point;
                center += point;
            }
            center /= count;
            var anchor = anchorFrame.TransformPoint(hiddenAnchorLocal);
            var restCenter = anchorFrame.TransformPoint(restCenterLocal);
            var guide = anchorFrame.TransformPoint(edgeGuideLocal);
            guide.y = Mathf.Max(guide.y, center.y + .025f);
            float moved = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Vector3.Distance(center, restCenter) - .025f) / .14f));
            var controlA = Vector3.Lerp(Vector3.Lerp(center, anchor, 1f / 3), Vector3.Lerp(center, guide, .60f), moved);
            var controlB = Vector3.Lerp(Vector3.Lerp(center, anchor, 2f / 3), guide, moved);
            MaximumRimError = 0;
            for (int ring = 0; ring < Rings; ring++)
            {
                float t = ring / (float)(Rings - 1);
                float u = 1 - t;
                var ringCenter = u * u * u * center + 3 * u * u * t * controlA + 3 * u * t * t * controlB + t * t * t * anchor;
                ringCenters[ring] = transform.InverseTransformPoint(ringCenter);
                for (int vertex = 0; vertex < count; vertex++)
                {
                    var nearOffset = worldRim[vertex] - center;
                    var farOffset = anchorFrame.TransformVector(anchorOffsetsLocal[vertex]);
                    var point = ringCenter + Vector3.Lerp(nearOffset, farOffset, Mathf.SmoothStep(0, 1, t));
                    vertices[ring * count + vertex] = transform.InverseTransformPoint(point);
                    if (ring == 0) MaximumRimError = Mathf.Max(MaximumRimError, Vector3.Distance(point, worldRim[vertex]));
                }
            }
            ownedMesh.vertices = vertices;
            // Determine winding from the current exterior, rather than relying on the FBX rim's traversal direction.
            int nextTriangle = 0;
            for (int ring = 0; ring < Rings - 1; ring++)
                for (int vertex = 0; vertex < count; vertex++)
                {
                    int a = ring * count + vertex, b = ring * count + (vertex + 1) % count;
                    int c = (ring + 1) * count + vertex, d = (ring + 1) * count + (vertex + 1) % count;
                    var outward = vertices[a] - ringCenters[ring];
                    bool forward = Vector3.Dot(Vector3.Cross(vertices[c] - vertices[a], vertices[b] - vertices[a]), outward) >= 0;
                    triangles[nextTriangle++] = a;
                    triangles[nextTriangle++] = forward ? c : b;
                    triangles[nextTriangle++] = forward ? b : c;
                    triangles[nextTriangle++] = b;
                    triangles[nextTriangle++] = forward ? c : d;
                    triangles[nextTriangle++] = forward ? d : c;
                }
            ownedMesh.triangles = triangles;
            ownedMesh.RecalculateNormals();
            ownedMesh.RecalculateBounds();
            ownedMesh.MarkModified();
        }

        private void EnsureMesh(int count)
        {
            if (ownedMesh && vertices != null && vertices.Length == count * Rings && skinMatrices.Length == bindPoses.Length) return;
            ReleaseMesh();
            ownedMesh = new Mesh { name = "Player_RightSleeve_Continuation_Dynamic", hideFlags = HideFlags.DontSave };
            ownedMesh.MarkDynamic();
            worldRim = new Vector3[count];
            vertices = new Vector3[count * Rings];
            uv = new Vector2[vertices.Length];
            triangles = new int[count * (Rings - 1) * 6];
            skinMatrices = new Matrix4x4[bindPoses.Length];
            ringCenters = new Vector3[Rings];
            for (int ring = 0; ring < Rings; ring++)
                for (int vertex = 0; vertex < count; vertex++) uv[ring * count + vertex] = rim[vertex].uv;
            ownedMesh.vertices = vertices;
            ownedMesh.uv = uv;
            GetComponent<MeshFilter>().sharedMesh = ownedMesh;
        }

        private void OnDisable() => ReleaseMesh();
        private void OnDestroy() => ReleaseMesh();
        private void ReleaseMesh()
        {
            if (!ownedMesh) return;
            var filter = GetComponent<MeshFilter>();
            if (filter && filter.sharedMesh == ownedMesh) filter.sharedMesh = null;
            if (Application.IsPlaying(gameObject)) Destroy(ownedMesh);
            else DestroyImmediate(ownedMesh);
            ownedMesh = null;
        }
    }
}
