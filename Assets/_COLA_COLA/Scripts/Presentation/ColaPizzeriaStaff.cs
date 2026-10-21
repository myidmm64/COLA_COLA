using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColaCola.Presentation
{
    /// <summary>A single quiet background worker. Its schedule never consumes duel or item randomness.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaPizzeriaStaff : MonoBehaviour
    {
        private enum Activity { Hidden, Arriving, Working, Leaving }
        private static readonly int Visibility = Shader.PropertyToID("_Visibility");
        private readonly System.Random timing = new System.Random(2061031);
        private readonly List<Material> materials = new();
        private readonly List<Mesh> meshes = new();
        private MaterialPropertyBlock properties;
        private ColaDuelController match;
        private ColaDuelMusic music;
        private GameObject root, visual;
        private Renderer[] renderers;
        private Transform[] nodes;
        private Vector3[] restPositions;
        private Quaternion[] restRotations;
        private Transform thighL, thighR, shinL, shinR, footL, footR, armL, armR, forearmL, forearmR, torso, head;
        private AudioSource footsteps;
        private AudioClip leftStep, rightStep;
        private Activity activity;
        private Vector3 entrance, counter, from, to, visualOffset;
        private float ambientTime;
        private float nextVisit, progress, workUntil, phase, visibility, workClock;
        private int lastStep;

        public static void EnsureFor(ColaDuelController duel)
        {
            if (!duel || !Application.IsPlaying(duel.gameObject)) return;
            var staff = duel.GetComponent<ColaPizzeriaStaff>();
            if (!staff) staff = duel.gameObject.AddComponent<ColaPizzeriaStaff>();
            staff.match = duel; staff.music = duel.GetComponent<ColaDuelMusic>();
            if (!staff.root) staff.Initialize();
        }

        private void Initialize()
        {
            var prefab = Resources.Load<GameObject>("PizzeriaStaff");
            var shader = Resources.Load<Shader>("COLA_COLA_Staff");
            if (!prefab || !shader) { Debug.LogWarning("Pizzeria background staff resources are missing.", this); return; }
            root = new GameObject("Pizzeria / Occasional Staff") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform.root, false);
            visual = Instantiate(prefab, root.transform);
            visual.name = "Existing PSX model / kitchen uniform";
            foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { root.SetActive(false); return; }
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.localScale *= 1.66f / Mathf.Max(.01f, bounds.size.y);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.position -= Vector3.up * bounds.min.y;
            visualOffset = visual.transform.localPosition;
            var replacements = new Dictionary<Material, Material>();
            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    var source = slots[i];
                    if (!source) continue;
                    if (!replacements.TryGetValue(source, out var material))
                    {
                        material = NewMaterial(shader, new Color(.72f, .77f, .66f));
                        var texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") :
                            source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                        if (texture) material.SetTexture("_BaseMap", texture);
                        replacements.Add(source, material);
                    }
                    slots[i] = material;
                }
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
            thighL = Find("thigh.L"); thighR = Find("thigh.R"); shinL = Find("shin.L"); shinR = Find("shin.R");
            footL = Find("foot.L"); footR = Find("foot.R"); armL = Find("upper_arm.L"); armR = Find("upper_arm.R");
            forearmL = Find("forearm.L"); forearmR = Find("forearm.R"); torso = Find("spine.002");
            // "head" is a renderer object in this asset; spine.005 is the actual head joint.
            head = Find("spine.005");
            LowerArm(armL, forearmL); LowerArm(armR, forearmR);
            AddUniform(shader);
            renderers = visual.GetComponentsInChildren<Renderer>(true);
            nodes = visual.GetComponentsInChildren<Transform>(true);
            restPositions = new Vector3[nodes.Length]; restRotations = new Quaternion[nodes.Length];
            for (int i = 0; i < nodes.Length; i++) { restPositions[i] = nodes[i].localPosition; restRotations[i] = nodes[i].localRotation; }
            footsteps = root.AddComponent<AudioSource>();
            footsteps.playOnAwake = false; footsteps.spatialBlend = 1; footsteps.dopplerLevel = 0;
            footsteps.minDistance = 1.5f; footsteps.maxDistance = 10; footsteps.priority = 220;
            leftStep = BuildStep(9131); rightStep = BuildStep(9132);
            activity = Activity.Hidden; nextVisit = ambientTime + Range(16, 28);
            SetVisibility(0); root.SetActive(false);
        }

        public void BeginMenuAmbience()
        {
            if (activity == Activity.Hidden) nextVisit = ambientTime + 2.5f;
        }

        private void Update()
        {
            if (!Application.IsPlaying(gameObject) || !root || nodes == null || !match) return;
            if (!match.isActiveAndEnabled) { if (activity != Activity.Hidden) Hide(); return; }
            float dt = ColaFrontEnd.AnimatesMenuBackground ? Time.unscaledDeltaTime : ColaGameClock.Delta;
            ambientTime += dt;
            float now = ambientTime;
            var interaction = match.interaction;
            var burst = interaction ? interaction.burstPresentation : null;
            bool quietMoment = ColaFrontEnd.AnimatesMenuBackground || (match.isActiveAndEnabled && match.State == ColaDuelController.DuelState.Playing &&
                (!burst || burst.Stage == ColaBurstPresentation.BurstStage.Idle) && (!interaction || !interaction.IsBusy));
            if (activity == Activity.Hidden)
            {
                if (now < nextVisit) return;
                if (!quietMoment) { nextVisit = now + 3; return; }
                bool left = timing.Next(3) != 0;
                // Two clear kitchen alcoves: behind the service counter, outside the oven and soda-machine footprints.
                entrance = new Vector3(left ? -3.55f : 3.55f, 0, left ? 4.35f : 4.40f);
                counter = new Vector3(left ? -1.85f : 2.70f, 0, entrance.z);
                BeginPath(entrance, counter, Activity.Arriving);
                root.SetActive(true);
            }
            if (dt <= 0) return;
            bool walking = activity != Activity.Working;
            if (walking)
            {
                progress = Mathf.Min(1, progress + dt * .46f / Mathf.Max(.1f, Vector3.Distance(from, to)));
                Vector3 local = Vector3.Lerp(from, to, progress);
                root.transform.position = transform.root.TransformPoint(local);
                var rotation = Quaternion.LookRotation(transform.root.TransformDirection(to - from).normalized, Vector3.up);
                root.transform.rotation = Quaternion.Slerp(root.transform.rotation, rotation, 1 - Mathf.Exp(-dt * 8));
                SetVisibility(activity == Activity.Arriving ? Mathf.SmoothStep(0, 1, progress / .16f) :
                    1 - Mathf.SmoothStep(0, 1, (progress - .78f) / .22f));
                phase += dt * (2 * Mathf.PI / 1.18f);
                int step = Mathf.FloorToInt(phase / Mathf.PI);
                if (step != lastStep)
                {
                    lastStep = step;
                    float duck = music ? music.Duck : 1;
                    if (!ColaFrontEnd.AnimatesMenuBackground && duck > .25f && quietMoment) footsteps.PlayOneShot(step % 2 == 0 ? leftStep : rightStep, .14f * visibility * duck);
                }
                if (progress >= 1)
                {
                    if (activity == Activity.Arriving) { activity = Activity.Working; workUntil = now + Range(1.8f, 3.2f); workClock = 0; }
                    else { Hide(); return; }
                }
            }
            else
            {
                workClock += dt;
                root.transform.rotation = Quaternion.Slerp(root.transform.rotation, transform.root.rotation * Quaternion.Euler(0, 180, 0), 1 - Mathf.Exp(-dt * 4));
                if (now >= workUntil || !ColaFrontEnd.AnimatesMenuBackground && match.State != ColaDuelController.DuelState.Playing)
                    BeginPath(counter, entrance, Activity.Leaving);
            }
            Animate(walking);
        }

        private void BeginPath(Vector3 start, Vector3 end, Activity next)
        {
            from = start; to = end; progress = 0; activity = next;
            lastStep = Mathf.FloorToInt(phase / Mathf.PI);
            if (next == Activity.Arriving)
            {
                root.transform.SetPositionAndRotation(transform.root.TransformPoint(start),
                    Quaternion.LookRotation(transform.root.TransformDirection(end - start), Vector3.up));
            }
        }
        private void Animate(bool walking)
        {
            for (int i = 0; i < nodes.Length; i++) { nodes[i].localPosition = restPositions[i]; nodes[i].localRotation = restRotations[i]; }
            float gait = walking ? Mathf.Sin(phase) : 0;
            visual.transform.localPosition = visualOffset + Vector3.up * (walking ? .009f * Mathf.Sin(phase * 2) : .002f * Mathf.Sin(workClock * 3));
            Vector3 right = root.transform.right;
            Rotate(thighL, gait * 20, right); Rotate(thighR, -gait * 20, right);
            Rotate(shinL, Mathf.Max(0, -gait) * 27, right); Rotate(shinR, Mathf.Max(0, gait) * 27, right);
            Rotate(footL, -gait * 6, right); Rotate(footR, gait * 6, right);
            Rotate(armL, -gait * 10, right); Rotate(armR, gait * 10, right);
            float work = walking ? 0 : Mathf.SmoothStep(0, 1, workClock / .5f);
            Rotate(forearmL, -22 * work, right); Rotate(forearmR, -30 * work, right);
            Rotate(torso, walking ? Mathf.Sin(phase) * 2 : 3 * work, root.transform.up);
            Rotate(head, walking ? Mathf.Sin(phase * .5f) : (9 + Mathf.Sin(workClock * 2) * 2) * work, right);
        }
        private void Hide()
        {
            activity = Activity.Hidden; SetVisibility(0);
            if (root) root.SetActive(false);
            if (footsteps) footsteps.Stop();
            nextVisit = ambientTime + (ColaFrontEnd.AnimatesMenuBackground ? Range(12, 20) : Range(30, 55));
        }
        private void SetVisibility(float value)
        {
            visibility = value;
            if (renderers == null || renderers.Length == 0) return;
            // Native Unity objects must be created in a main-thread callback, not a MonoBehaviour field initializer.
            properties ??= new MaterialPropertyBlock();
            properties.SetFloat(Visibility, value);
            foreach (var renderer in renderers) if (renderer) renderer.SetPropertyBlock(properties);
        }
        private Transform Find(string name)
        { foreach (var node in visual.GetComponentsInChildren<Transform>(true)) if (node.name == name) return node; return null; }
        private static void LowerArm(Transform upper, Transform lower)
        { if (upper && lower) upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, Vector3.down) * upper.rotation; }
        private static void Rotate(Transform bone, float angle, Vector3 axis)
        { if (bone) bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation; }
        private float Range(float low, float high) => Mathf.Lerp(low, high, (float)timing.NextDouble());
        private Material NewMaterial(Shader shader, Color tint)
        {
            var material = new Material(shader) { name = "Kitchen staff uniform", hideFlags = HideFlags.DontSave };
            material.SetColor("_BaseColor", tint); materials.Add(material); return material;
        }
        private void AddUniform(Shader shader)
        {
            // Measure the posed source geometry once. No guessed world-space clothing coordinates.
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.sharedMesh) continue;
                var baked = new Mesh { name = "Staff tailoring measurement", hideFlags = HideFlags.DontSave };
                // Compensate the imported renderer's 100x scale before TransformPoint applies it.
                skin.BakeMesh(baked, true);
                int offset = vertices.Count;
                foreach (var vertex in baked.vertices)
                    vertices.Add(visual.transform.InverseTransformPoint(skin.transform.TransformPoint(vertex)));
                foreach (var index in baked.triangles) triangles.Add(offset + index);
                Destroy(baked);
            }
            if (vertices.Count == 0) return;
            Bounds body = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices) body.Encapsulate(vertex);
            float height = body.size.y;
            float shoulderWidth = armL && armR ? Mathf.Abs(Local(armL).x - Local(armR).x) : height * .23f;
            shoulderWidth = Mathf.Clamp(shoulderWidth, height * .18f, height * .30f);
            float chestY = armL && armR ? (Local(armL).y + Local(armR).y) * .5f - height * .035f : body.min.y + height * .76f;
            float hipY = thighL && thighR ? (Local(thighL).y + Local(thighR).y) * .5f : body.min.y + height * .51f;
            float waistY = Mathf.Min(chestY - height * .12f, hipY + height * .055f);
            float hemY = hipY - height * .15f;
            var apron = NewMaterial(shader, new Color(.49f, .25f, .18f));
            apron.SetFloat("_Cull", 0);
            ColaPropPatina.BindWearMap(apron);
            apron.SetFloat("_PatinaStrength", .55f); apron.SetFloat("_PatinaKind", 6); apron.SetFloat("_PatinaUV", 1);
            var edging = NewMaterial(shader, new Color(.67f, .58f, .40f));
            edging.SetFloat("_Cull", 0);
            ColaPropPatina.BindWearMap(edging);
            edging.SetFloat("_PatinaStrength", .48f); edging.SetFloat("_PatinaKind", 6); edging.SetFloat("_PatinaUV", 1);
            var paper = NewMaterial(shader, new Color(.80f, .77f, .65f));
            ColaPropPatina.BindWearMap(paper);
            paper.SetFloat("_PatinaStrength", .48f); paper.SetFloat("_PatinaKind", 2);
            Transform waist = Find("spine");
            if (!waist) waist = visual.transform;
            Transform chest = torso ? torso : waist;

            var panel = new List<Vector3>();
            var indices = new List<int>();
            const int rows = 8, columns = 9;
            for (int row = 0; row < rows; row++)
            {
                float y = Mathf.Lerp(hemY, chestY, (float)row / (rows - 1));
                float width = shoulderWidth * (y <= waistY ? Mathf.Lerp(.55f, .51f, Mathf.InverseLerp(hemY, waistY, y)) :
                    Mathf.Lerp(.51f, .31f, Mathf.InverseLerp(waistY, chestY, y)));
                if (y >= waistY)
                    width = Mathf.Min(FitHalfWidth(vertices, triangles, y, width, -1), FitHalfWidth(vertices, triangles, y, width, 1)) * .96f;
                float front = SurfaceDepth(vertices, triangles, 0, Mathf.Max(y, waistY), body.center.z);
                for (int column = 0; column < columns; column++)
                {
                    float x = Mathf.Lerp(-width, width, (float)column / (columns - 1));
                    float z = SurfaceDepth(vertices, triangles, x, y, front);
                    // The lower cloth bridges the legs rather than dipping into their gap.
                    if (y < waistY) z = Mathf.Max(z, front) + (waistY - y) * .24f;
                    panel.Add(new Vector3(x, y, z + height * .010f));
                    if (row > 0 && column > 0) Quad(indices, row * columns + column - columns - 1,
                        row * columns + column - columns, row * columns + column - 1, row * columns + column);
                }
            }
            AddCloth("Tailored bib and apron skirt", panel, indices, apron, waist, chest, waistY, chestY);

            // Two straps follow the actual chest surface up to the shoulders.
            foreach (float side in new[] { -1f, 1f })
            {
                var strap = new List<Vector3>(); var strapIndices = new List<int>();
                float shoulderY = chestY + height * .045f;
                for (int row = 0; row < 5; row++)
                {
                    float t = row / 4f, y = Mathf.Lerp(chestY - height * .012f, shoulderY, t);
                    float centerX = side * shoulderWidth * Mathf.Lerp(.25f, .36f, t);
                    for (int edge = 0; edge < 2; edge++)
                    {
                        float x = centerX + (edge == 0 ? -1 : 1) * height * .008f;
                        float z = SurfaceDepth(vertices, triangles, x, y, body.center.z);
                        strap.Add(new Vector3(x, y, z + height * .012f));
                    }
                    if (row > 0) Quad(strapIndices, row * 2 - 2, row * 2 - 1, row * 2, row * 2 + 1);
                }
                AddCloth("Shoulder strap " + side, strap, strapIndices, edging, waist, chest, waistY, chestY);
            }
            // A fitted waist tie wraps around the torso, making the ownership of the garment clear from the side.
            var belt = new List<Vector3>(); var beltIndices = new List<int>();
            for (int segment = 0; segment <= 16; segment++)
            {
                float angle = segment * Mathf.PI * 2 / 16;
                float x = Mathf.Sin(angle) * shoulderWidth * .52f;
                float front = SurfaceDepth(vertices, triangles, x, waistY, body.center.z + height * .05f);
                float back = SurfaceDepth(vertices, triangles, x, waistY, body.center.z - height * .05f, false);
                float z = Mathf.Lerp(back - height * .009f, front + height * .012f, (Mathf.Cos(angle) + 1) * .5f);
                for (int edge = 0; edge < 2; edge++) belt.Add(new Vector3(x, waistY + (edge == 0 ? -.009f : .009f) * height, z));
                if (segment > 0) Quad(beltIndices, segment * 2 - 2, segment * 2, segment * 2 - 1, segment * 2 + 1);
            }
            AddCloth("Fitted waist tie", belt, beltIndices, edging, waist, chest, waistY, chestY);

            // Fit an eight-sided paper crown around the upper head/hair, with its bottom slightly seated in the hair.
            Vector3 headCenter = head ? Local(head) : new Vector3(body.center.x, body.max.y - height * .10f, body.center.z);
            Bounds crown = new Bounds(new Vector3(headCenter.x, body.max.y - height * .045f, headCenter.z), Vector3.zero);
            foreach (var vertex in vertices)
                if (vertex.y >= body.max.y - height * .085f && Mathf.Abs(vertex.x - headCenter.x) < height * .15f) crown.Encapsulate(vertex);
            float radiusX = Mathf.Clamp(crown.extents.x + height * .007f, height * .055f, height * .095f);
            float radiusZ = Mathf.Clamp(crown.extents.z + height * .007f, height * .050f, height * .085f);
            var cap = new List<Vector3>(); var capIndices = new List<int>();
            for (int ring = 0; ring < 3; ring++)
            {
                float y = body.max.y + height * (ring == 0 ? -.037f : ring == 1 ? -.016f : .035f);
                float taper = ring == 2 ? .81f : 1;
                for (int segment = 0; segment < 8; segment++)
                {
                    float angle = (segment + .5f) * Mathf.PI * 2 / 8;
                    cap.Add(new Vector3(crown.center.x + Mathf.Sin(angle) * radiusX * taper, y,
                        crown.center.z + Mathf.Cos(angle) * radiusZ * taper));
                    if (ring > 0) Quad(capIndices, (ring - 1) * 8 + segment, (ring - 1) * 8 + (segment + 1) % 8,
                        ring * 8 + segment, ring * 8 + (segment + 1) % 8);
                }
            }
            cap.Add(new Vector3(crown.center.x, body.max.y + height * .035f, crown.center.z));
            for (int segment = 0; segment < 8; segment++) { capIndices.Add(24); capIndices.Add(16 + segment); capIndices.Add(16 + (segment + 1) % 8); }
            for (int i = 0; i < cap.Count; i++) cap[i] = visual.transform.TransformPoint(cap[i]);
            AddPart("Fitted folded paper cap", head ? head : chest, cap.ToArray(), capIndices.ToArray(), paper);
        }
        private Vector3 Local(Transform joint) => visual.transform.InverseTransformPoint(joint.position);
        private static void Quad(List<int> indices, int bottomLeft, int bottomRight, int topLeft, int topRight)
        { indices.Add(bottomLeft); indices.Add(bottomRight); indices.Add(topLeft); indices.Add(topLeft); indices.Add(bottomRight); indices.Add(topRight); }
        private static float FitHalfWidth(List<Vector3> vertices, List<int> triangles, float y, float desired, float side)
        {
            if (!float.IsNaN(SurfaceDepth(vertices, triangles, desired * side, y, float.NaN))) return desired;
            float low = 0, high = desired;
            for (int i = 0; i < 7; i++)
            {
                float middle = (low + high) * .5f;
                if (float.IsNaN(SurfaceDepth(vertices, triangles, middle * side, y, float.NaN))) high = middle;
                else low = middle;
            }
            return low;
        }
        private static float SurfaceDepth(List<Vector3> vertices, List<int> triangles, float x, float y, float fallback, bool front = true)
        {
            float result = front ? float.NegativeInfinity : float.PositiveInfinity;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                float determinant = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(determinant) < .00000001f) continue;
                float u = ((b.y - c.y) * (x - c.x) + (c.x - b.x) * (y - c.y)) / determinant;
                float v = ((c.y - a.y) * (x - c.x) + (a.x - c.x) * (y - c.y)) / determinant;
                if (u < -.0001f || v < -.0001f || u + v > 1.0001f) continue;
                float z = u * a.z + v * b.z + (1 - u - v) * c.z;
                result = front ? Mathf.Max(result, z) : Mathf.Min(result, z);
            }
            return float.IsInfinity(result) ? fallback : result;
        }
        private void AddCloth(string name, List<Vector3> vertices, List<int> triangles, Material material,
            Transform waist, Transform chest, float waistY, float chestY)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            var clothUV = new List<Vector2>(vertices.Count);
            Vector3 surfaceScale = visual.transform.lossyScale;
            foreach (var vertex in vertices) clothUV.Add(new Vector2(vertex.x * Mathf.Abs(surfaceScale.x), vertex.y * Mathf.Abs(surfaceScale.y)));
            mesh.SetUVs(0, clothUV);
            var weights = new BoneWeight[vertices.Count];
            for (int i = 0; i < weights.Length; i++)
            {
                float upper = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(waistY, chestY, vertices[i].y));
                weights[i] = new BoneWeight { boneIndex0 = 0, boneIndex1 = 1, weight0 = 1 - upper, weight1 = upper };
            }
            mesh.boneWeights = weights;
            mesh.bindposes = new[] { waist.worldToLocalMatrix * visual.transform.localToWorldMatrix,
                chest.worldToLocalMatrix * visual.transform.localToWorldMatrix };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var go = new GameObject(name, typeof(SkinnedMeshRenderer)); go.transform.SetParent(visual.transform, false);
            var skin = go.GetComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh; skin.sharedMaterial = material; skin.bones = new[] { waist, chest }; skin.rootBone = waist;
            skin.localBounds = mesh.bounds; skin.updateWhenOffscreen = true; skin.shadowCastingMode = ShadowCastingMode.Off;
        }
        private void AddPart(string name, Transform parent, Vector3[] vertices, int[] triangles, Material material)
        {
            for (int i = 0; i < vertices.Length; i++) vertices[i] = parent.InverseTransformPoint(vertices[i]);
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles, hideFlags = HideFlags.DontSave };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        private static AudioClip BuildStep(int seed)
        {
            const int rate = 22050; var data = new float[3528]; var noise = new System.Random(seed); float low = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate, white = (float)noise.NextDouble() * 2 - 1;
                low += (white - low) * .11f;
                float hit = Mathf.Sin(2 * Mathf.PI * 118 * t) * Mathf.Exp(-t * 52);
                float scuff = low * .45f * (1 - Mathf.Exp(-t * 400)) * Mathf.Exp(-t * 29);
                data[i] = (hit * .34f + scuff) * Mathf.Min(1, (data.Length - i) / 220f);
            }
            var clip = AudioClip.Create("Quiet kitchen shoe " + seed, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        private void OnDisable() => Hide();
        private void OnDestroy()
        {
            if (root) Destroy(root);
            foreach (var material in materials) if (material) Destroy(material);
            foreach (var mesh in meshes) if (mesh) Destroy(mesh);
            if (leftStep) Destroy(leftStep); if (rightStep) Destroy(rightStep);
        }
    }
}
