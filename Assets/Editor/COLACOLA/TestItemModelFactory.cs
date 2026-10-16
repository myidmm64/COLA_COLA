using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColaCola.Editor
{
    /// <summary>Six tabletop scale props for inventory space tests. No item effects are assigned.</summary>
    public static class TestItemModelFactory
    {
        public const string PrefabFolder = "Assets/_COLA_COLA/Art/Prefabs/TestItems";
        private const string Models = "Assets/_COLA_COLA/Art/Models/TestItems";
        private const string Materials = "Assets/_COLA_COLA/Art/Materials/TestItems";
        public static readonly string[] Names = {
            "TestItem01_ReleaseValve", "TestItem02_ColdPack", "TestItem03_FoldedCoupon",
            "TestItem04_WrappedCandy", "TestItem05_ExpansionCap", "TestItem06_AdditiveBottle"
        };
        private static readonly Dictionary<string, Material> palette = new Dictionary<string, Material>();

        public static GameObject[] BuildPrefabs()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(Models);
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();
            palette.Clear();
            MakeMaterial("Brass", new Color(.51f, .32f, .12f), .58f, .24f);
            MakeMaterial("Steel", new Color(.41f, .43f, .38f), .65f, .19f);
            MakeMaterial("Charcoal", new Color(.035f, .045f, .038f), .05f, .12f);
            MakeMaterial("Red", new Color(.64f, .075f, .035f), .05f, .16f);
            MakeMaterial("Cream", new Color(.88f, .79f, .56f), 0, .12f);
            MakeMaterial("Paper", new Color(.84f, .76f, .55f), 0, .02f);
            MakeMaterial("ColdBlue", new Color(.065f, .39f, .47f), 0, .13f);
            MakeMaterial("BlueSeam", new Color(.25f, .61f, .64f), 0, .1f);
            MakeMaterial("Amber", new Color(.55f, .26f, .055f), .04f, .22f);
            MakeMaterial("TrayInside", new Color(.50f, .46f, .34f), .22f, .16f);
            MakeMaterial("TrayPlayer", new Color(.11f, .27f, .25f), .25f, .18f);
            MakeMaterial("TrayBoss", new Color(.38f, .09f, .055f), .25f, .18f);
            var builders = new Action<Transform>[] { Valve, ColdPack, Coupon, Candy, Cap, Bottle };
            var prefabs = new GameObject[builders.Length];
            for (var i = 0; i < builders.Length; i++)
                prefabs[i] = SavePrefab(Names[i], builders[i]);
            SavePrefab("InventoryServingTray", Tray);
            AssetDatabase.SaveAssets();
            return prefabs;
        }

        private static GameObject SavePrefab(string name, Action<Transform> build)
        {
            var root = new GameObject(name);
            try
            {
                build(root.transform);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + name + ".prefab");
                if (!prefab) throw new IOException("Could not save " + name);
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Valve(Transform root)
        {
            var body = new Geometry();
            body.Lathe(new Vector3(0, .027f, 0), new[] { -.067f, -.055f, -.051f, .051f, .055f, .067f },
                new[] { .027f, .027f, .020f, .020f, .027f, .027f }, 8, Quaternion.Euler(0, 0, 90));
            body.Lathe(new Vector3(0, .024f, 0), new[] { 0f, .055f, .061f }, new[] { .014f, .011f, .011f }, 8);
            Part(root, "BrassTeeAndStem", body, "Brass");
            var wheel = new Geometry();
            wheel.Ring(.044f, .030f, .087f, .098f, 8);
            for (var i = 0; i < 3; i++)
            {
                var rotation = Quaternion.Euler(0, i * 60, 0);
                wheel.Box(new Vector3(0, .0925f, 0), new Vector3(.071f, .009f, .009f), .002f, rotation);
            }
            Part(root, "RedHandwheel", wheel, "Red");
            var fittings = new Geometry();
            fittings.Lathe(new Vector3(0, .101f, 0), new[] { 0f, .005f }, new[] { .009f, .009f }, 6);
            foreach (var x in new[] { -.0674f, .0674f })
                fittings.Lathe(new Vector3(x, .027f, 0), new[] { -.0005f, .0005f }, new[] { .017f, .017f }, 8, Quaternion.Euler(0, 0, 90));
            Part(root, "BoltAndDarkPipeEnds", fittings, "Charcoal");
        }

        private static void ColdPack(Transform root)
        {
            var body = new Geometry();
            body.Box(new Vector3(0, .024f, 0), new Vector3(.164f, .048f, .106f), .013f);
            Part(root, "FacetedCoolingPouch", body, "ColdBlue");
            var seam = new Geometry();
            foreach (var z in new[] { -.046f, .046f })
                seam.Box(new Vector3(0, .024f, z), new Vector3(.155f, .006f, .013f), .002f);
            Part(root, "HeatSealedEdges", seam, "BlueSeam");
            var details = new Geometry();
            details.Lathe(new Vector3(.051f, .046f, .015f), new[] { 0f, .006f, .020f }, new[] { .013f, .013f, .011f }, 8);
            details.Box(new Vector3(-.017f, .049f, -.004f), new Vector3(.008f, .002f, .035f), .0005f);
            details.Box(new Vector3(-.017f, .049f, -.004f), new Vector3(.035f, .002f, .008f), .0005f);
            Part(root, "WhiteCapAndColdMark", details, "Cream");
        }

        private static void Coupon(Transform root)
        {
            var paper = new Geometry();
            var a = new Vector3(-.073f, .001f, -.042f);
            var b = new Vector3(.073f, .001f, -.042f);
            var c = new Vector3(.073f, .045f, 0);
            var d = new Vector3(-.073f, .045f, 0);
            paper.DoubleQuad(a, b, c, d);
            paper.DoubleQuad(d, c, new Vector3(.073f, .001f, .042f), new Vector3(-.073f, .001f, .042f));
            Part(root, "FoldedPaperTent", paper, "Paper");
            var print = new Geometry();
            Func<float, float, Vector3> front = (x, v) => new Vector3(x, .002f + v * .044f, -.0425f + v * .042f);
            print.Triangle(front(-.052f, .29f), front(-.016f, .29f), front(-.034f, .76f));
            for (var row = 0; row < 3; row++)
            {
                var v = .32f + row * .15f;
                print.DoubleQuad(front(.004f, v), front(.052f - row * .006f, v),
                    front(.052f - row * .006f, v + .045f), front(.004f, v + .045f));
            }
            Part(root, "RedPizzaAndPrintedLines", print, "Red");
        }

        private static void Candy(Transform root)
        {
            var candy = new Geometry();
            candy.Lathe(new Vector3(0, .023f, 0), new[] { -.038f, -.026f, .026f, .038f },
                new[] { .008f, .023f, .023f, .008f }, 8, Quaternion.Euler(0, 0, 90));
            Part(root, "RedWrappedSweet", candy, "Red");
            var ends = new Geometry();
            foreach (var side in new[] { -1f, 1f })
            {
                var tip = new Vector3(side * .038f, .023f, 0);
                var a = new Vector3(side * .064f, .008f, -.025f);
                var b = new Vector3(side * .067f, .032f, -.007f);
                var c = new Vector3(side * .064f, .011f, .023f);
                ends.DoubleTriangle(tip, a, b);
                ends.DoubleTriangle(tip, b, c);
            }
            ends.Lathe(new Vector3(0, .023f, 0), new[] { -.007f, .007f }, new[] { .0235f, .0235f }, 8, Quaternion.Euler(0, 0, 90));
            Part(root, "CreamTwistedWrapperEndsAndBand", ends, "Cream");
        }

        private static void Cap(Transform root)
        {
            var skirt = new Geometry();
            skirt.Lathe(Vector3.zero, new[] { .001f, .005f, .039f, .044f }, new[] { .031f, .037f, .037f, .032f }, 10);
            for (var i = 0; i < 10; i++)
            {
                var rotation = Quaternion.Euler(0, i * 36, 0);
                skirt.Box(rotation * new Vector3(0, .022f, .037f), new Vector3(.008f, .030f, .005f), .001f, rotation);
            }
            Part(root, "RidgedCreamCap", skirt, "Cream");
            var crown = new Geometry();
            crown.Lathe(Vector3.zero, new[] { .043f, .056f, .068f, .072f }, new[] { .030f, .028f, .020f, .011f }, 10);
            Part(root, "SwollenRedCrown", crown, "Red");
        }

        private static void Bottle(Transform root)
        {
            var glass = new Geometry();
            glass.Lathe(Vector3.zero, new[] { .001f, .008f, .103f, .124f, .137f },
                new[] { .026f, .034f, .034f, .016f, .016f }, 8);
            Part(root, "FacetedAmberBottle", glass, "Amber");
            var lid = new Geometry();
            lid.Lathe(Vector3.zero, new[] { .134f, .140f, .155f, .169f }, new[] { .021f, .021f, .014f, .007f }, 8);
            Part(root, "BlackDropperTop", lid, "Charcoal");
            var label = new Geometry();
            label.Box(new Vector3(0, .068f, -.034f), new Vector3(.045f, .052f, .0015f), .0004f);
            Part(root, "CreamPaperLabel", label, "Cream");
            var mark = new Geometry();
            mark.Box(new Vector3(0, .068f, -.035f), new Vector3(.025f, .006f, .001f), 0);
            mark.Box(new Vector3(0, .068f, -.035f), new Vector3(.006f, .027f, .001f), 0);
            Part(root, "RedBottleMark", mark, "Red");
        }

        private static void Tray(Transform root)
        {
            var baseMesh = new Geometry();
            baseMesh.Box(new Vector3(0, .006f, 0), new Vector3(.734f, .012f, .404f), .005f);
            Part(root, "PressedMetalBase", baseMesh, "TrayInside");
            var rim = new Geometry();
            foreach (var z in new[] { -.207f, .207f })
                rim.Box(new Vector3(0, .016f, z), new Vector3(.758f, .026f, .016f), .004f);
            foreach (var x in new[] { -.371f, .371f })
                rim.Box(new Vector3(x, .016f, 0), new Vector3(.016f, .026f, .400f), .004f);
            Part(root, "EnamelRim", rim, "TrayPlayer");
            var handles = new Geometry();
            foreach (var x in new[] { -.388f, .388f })
                handles.Box(new Vector3(x, .019f, 0), new Vector3(.022f, .013f, .112f), .004f);
            Part(root, "SideHandles", handles, "Charcoal");
        }

        public static Material BossTrayMaterial => AssetDatabase.LoadAssetAtPath<Material>(Materials + "/TrayBoss.mat");

        private static void MakeMaterial(string name, Color color, float metal, float smoothness)
        {
            var path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            palette.Add(name, material);
        }

        private static void Part(Transform root, string name, Geometry geometry, string material)
        {
            var meshName = root.name + "_" + name;
            var path = Models + "/" + meshName + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh) { mesh = new Mesh(); geometry.Populate(mesh, meshName); AssetDatabase.CreateAsset(mesh, path); }
            else { geometry.Populate(mesh, meshName); mesh.MarkModified(); mesh.UploadMeshData(false); EditorUtility.SetDirty(mesh); }
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(root, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = palette[material];
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<int> indices = new List<int>();
            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                var cross = Vector3.Cross(b - a, c - a);
                var length = cross.magnitude;
                var normal = length > 0 ? cross / length : Vector3.up;
                var start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            }
            public void DoubleTriangle(Vector3 a, Vector3 b, Vector3 c) { Triangle(a, b, c); Triangle(c, b, a); }
            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Triangle(a, b, c); Triangle(a, c, d); }
            public void DoubleQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Quad(a, b, c, d); Quad(d, c, b, a); }
            public void Lathe(Vector3 origin, float[] levels, float[] radii, int sides, Quaternion? rotation = null)
            {
                var rings = new Vector3[levels.Length][];
                var q = rotation ?? Quaternion.identity;
                for (var level = 0; level < levels.Length; level++)
                {
                    rings[level] = new Vector3[sides];
                    for (var i = 0; i < sides; i++)
                    {
                        var angle = i * Mathf.PI * 2 / sides;
                        rings[level][i] = origin + q * new Vector3(Mathf.Cos(angle) * radii[level], levels[level], Mathf.Sin(angle) * radii[level]);
                    }
                }
                Solid(rings);
            }
            public void Ring(float outer, float inner, float bottom, float top, int sides)
            {
                for (var i = 0; i < sides; i++)
                {
                    var a = i * Mathf.PI * 2 / sides;
                    var b = (i + 1) * Mathf.PI * 2 / sides;
                    var oa = new Vector3(Mathf.Cos(a) * outer, 0, Mathf.Sin(a) * outer);
                    var ob = new Vector3(Mathf.Cos(b) * outer, 0, Mathf.Sin(b) * outer);
                    var ia = oa * (inner / outer); var ib = ob * (inner / outer);
                    var low = Vector3.up * bottom; var high = Vector3.up * top;
                    Quad(oa + low, oa + high, ob + high, ob + low);
                    Quad(ib + low, ib + high, ia + high, ia + low);
                    Quad(oa + high, ia + high, ib + high, ob + high);
                    Quad(ob + low, ib + low, ia + low, oa + low);
                }
            }
            public void Box(Vector3 center, Vector3 size, float bevel, Quaternion? rotation = null)
            {
                var q = rotation ?? Quaternion.identity;
                bevel = Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * .4f);
                var hx = size.x * .5f; var hz = size.z * .5f; var hy = size.y * .5f;
                var outline = new[] { new Vector2(-hx + bevel, -hz), new Vector2(hx - bevel, -hz),
                    new Vector2(hx, -hz + bevel), new Vector2(hx, hz - bevel), new Vector2(hx - bevel, hz),
                    new Vector2(-hx + bevel, hz), new Vector2(-hx, hz - bevel), new Vector2(-hx, -hz + bevel) };
                var rings = new Vector3[4][];
                var heights = new[] { -hy, -hy + bevel, hy - bevel, hy };
                for (var level = 0; level < rings.Length; level++)
                {
                    rings[level] = new Vector3[outline.Length];
                    for (var i = 0; i < outline.Length; i++)
                    {
                        var p = outline[i];
                        if (level == 0 || level == 3)
                            p = new Vector2(Mathf.Sign(p.x) * Mathf.Max(0, Mathf.Abs(p.x) - bevel), Mathf.Sign(p.y) * Mathf.Max(0, Mathf.Abs(p.y) - bevel));
                        rings[level][i] = center + q * new Vector3(p.x, heights[level], p.y);
                    }
                }
                Solid(rings);
            }
            private void Solid(Vector3[][] rings)
            {
                for (var r = 0; r < rings.Length - 1; r++)
                    for (var i = 0; i < rings[r].Length; i++)
                    {
                        var next = (i + 1) % rings[r].Length;
                        Quad(rings[r][i], rings[r + 1][i], rings[r + 1][next], rings[r][next]);
                    }
                var bottom = Vector3.zero; var top = Vector3.zero;
                foreach (var p in rings[0]) bottom += p / rings[0].Length;
                foreach (var p in rings[rings.Length - 1]) top += p / rings[rings.Length - 1].Length;
                for (var i = 0; i < rings[0].Length; i++)
                {
                    var next = (i + 1) % rings[0].Length;
                    Triangle(bottom, rings[0][i], rings[0][next]);
                    Triangle(top, rings[rings.Length - 1][next], rings[rings.Length - 1][i]);
                }
            }
            public void Populate(Mesh mesh, string name)
            {
                mesh.Clear(false); mesh.name = name;
                mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(indices, 0);
                mesh.RecalculateBounds();
            }
        }
    }
}
