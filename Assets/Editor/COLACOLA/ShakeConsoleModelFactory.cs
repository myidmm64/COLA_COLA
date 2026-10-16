using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ColaCola.Editor
{
    /// <summary>Builds a physical, faceted shake selector with three independently movable keycaps.</summary>
    public static class ShakeConsoleModelFactory
    {
        public const string PrefabPath = "Assets/_COLA_COLA/Art/Prefabs/ShakeConsole.prefab";
        private const string ModelPath = "Assets/_COLA_COLA/Art/Models/ShakeConsole";
        private const string MaterialPath = "Assets/_COLA_COLA/Art/Materials/ShakeConsole";
        private const float TopSlope = .13333333f;

        public static void ApplyMattePalette()
        {
            Material("Console_EnamelTeal", new Color(.047f, .155f, .16f), 0, .04f);
            Material("Console_TopTeal", new Color(.075f, .22f, .21f), 0, .04f);
            Material("Console_AgedMetal", new Color(.32f, .31f, .25f), .08f, .05f);
            Material("Console_Charcoal", new Color(.023f, .034f, .031f), 0, .04f);
            Material("Key_Cream", new Color(.56f, .52f, .40f), 0, .04f);
            Material("Key_Red", new Color(.48f, .045f, .022f), 0, .04f);
            Material("Marking_WarmIvory", new Color(.65f, .60f, .46f), 0, .04f);
        }

        public static GameObject BuildPrefab()
        {
            Directory.CreateDirectory(ModelPath);
            Directory.CreateDirectory(MaterialPath);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();

            ApplyMattePalette();
            var teal = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "/Console_EnamelTeal.mat");
            var topTeal = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "/Console_TopTeal.mat");
            var metal = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "/Console_AgedMetal.mat");
            var dark = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "/Console_Charcoal.mat");
            var cream = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "/Key_Cream.mat");
            var red = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "/Key_Red.mat");
            var pale = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "/Marking_WarmIvory.mat");

            var root = new GameObject("ShakeConsole");
            try
            {
                var housing = Group("Housing", root.transform);
                Part("BeveledWedgeBody", housing, "WedgeBody", Wedge(.204f, .150f, -.065f, .045f, .008f), teal);
                Part("ThinMetalRim", housing, "MetalRim", Wedge(.201f, .147f, .045f, .049f, .001f, true), metal);
                Part("InsetSlopedTop", housing, "TopPanel", Wedge(.195f, .141f, .049f, .051f, .001f, true), topTeal);

                var footMesh = new MeshBuilder();
                foreach (var x in new[] { -.157f, .157f })
                    foreach (var z in new[] { -.105f, .105f })
                        AddBox(footMesh, new Vector3(x, -.0675f, z), new Vector3(.045f, .005f, .038f), .004f);
                Part("FourRubberFeet", housing, "RubberFeet", footMesh, dark);

                // This small rear saddle meets the existing gauge foot while keeping the key row in front of it.
                Part("GaugeMountSaddle", housing, "GaugeMountSaddle", Box(new Vector3(0, .070f, .080f), new Vector3(.152f, .024f, .074f), .004f), metal);
                var fasteners = new MeshBuilder();
                foreach (var x in new[] { -.178f, .178f })
                    foreach (var z in new[] { -.120f, .120f })
                        AddCylinder(fasteners, new Vector3(x, TopY(z) + .007f, z), .0035f, .002f, 6);
                Part("TopPanelFasteners", housing, "TopPanelFasteners", fasteners, metal);

                var sockets = Group("ButtonSockets", root.transform);
                var tilt = Quaternion.Euler(-Mathf.Atan(TopSlope) * Mathf.Rad2Deg, 0, 0);
                for (var i = 0; i < 3; i++)
                {
                    var keyNumber = i + 1;
                    var center = new Vector3((i - 1) * .128f, TopY(-.072f) + .009f, -.072f);
                    var socket = Group("Socket" + keyNumber, sockets);
                    socket.localPosition = center;
                    socket.localRotation = tilt;
                    Part("RecessedDarkSocket", socket, "Socket" + keyNumber, Box(Vector3.zero, new Vector3(.113f, .010f, .110f), .005f), dark);
                    Part("MetalSocketLip", socket, "SocketLip" + keyNumber, Ring(.055f, .0535f, .0505f, .049f, .004f, .008f, .003f), metal);

                    // Move this Transform down by 0.012 m to press the entire key, including its raised numeral.
                    var button = Group("ShakeButton" + keyNumber, root.transform);
                    button.localPosition = center + new Vector3(0, .012f, 0);
                    button.localRotation = tilt;
                    Part("BeveledKeycap", button, "Keycap" + keyNumber, Box(new Vector3(0, .014f, 0), new Vector3(.099f, .033f, .096f), .006f), i == 2 ? red : cream);
                    var digits = new MeshBuilder();
                    PixelDigit(digits, (char)('1' + i), .0062f, .0307f, .0322f);
                    Part("RaisedNumber" + keyNumber, button, "Digit" + keyNumber, digits, i == 2 ? pale : dark);
                    var collider = button.gameObject.AddComponent<BoxCollider>();
                    collider.center = new Vector3(0, .014f, 0);
                    collider.size = new Vector3(.099f, .033f, .096f);
                }

                var bodyCollider = root.AddComponent<BoxCollider>();
                bodyCollider.center = Vector3.zero;
                bodyCollider.size = new Vector3(.408f, .13f, .30f);
                AssetDatabase.SaveAssets();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (!prefab) throw new InvalidOperationException("Could not save shake console prefab.");
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static float TopY(float z) { return .045f + TopSlope * z; }

        private static Transform Group(string name, Transform parent)
        {
            var transform = new GameObject(name).transform;
            transform.SetParent(parent, false);
            return transform;
        }

        private static Material Material(string name, Color color, float metallic, float smoothness)
        {
            var path = MaterialPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (!shader) throw new InvalidOperationException("A lit shader is required for the shake console.");
            if (!material)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_SpecularHighlights", 0);
            material.SetFloat("_EnvironmentReflections", 0);
            material.SetColor("_EmissionColor", Color.black);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Part(string name, Transform parent, string meshName, MeshBuilder builder, Material material)
        {
            var mesh = builder.Build(meshName);
            var path = ModelPath + "/" + meshName + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved)
            {
                EditorUtility.CopySerialized(mesh, saved);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(saved);
            }
            else
            {
                saved = mesh;
                AssetDatabase.CreateAsset(saved, path);
            }
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = saved;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Vector2[] Outline(float halfX, float halfZ, float bevel)
        {
            return new[]
            {
                new Vector2(-halfX + bevel, -halfZ), new Vector2(halfX - bevel, -halfZ),
                new Vector2(halfX, -halfZ + bevel), new Vector2(halfX, halfZ - bevel),
                new Vector2(halfX - bevel, halfZ), new Vector2(-halfX + bevel, halfZ),
                new Vector2(-halfX, halfZ - bevel), new Vector2(-halfX, -halfZ + bevel)
            };
        }

        private static Vector3[] Level(Vector2[] outline, float height, float slope = 0, Vector3 offset = default)
        {
            var vertices = new Vector3[outline.Length];
            for (var i = 0; i < vertices.Length; i++)
                vertices[i] = offset + new Vector3(outline[i].x, height + slope * outline[i].y, outline[i].y);
            return vertices;
        }

        private static MeshBuilder Wedge(float halfX, float halfZ, float bottom, float top, float bevel, bool slopedBottom = false)
        {
            var mesh = new MeshBuilder();
            bevel = Mathf.Min(bevel, (top - bottom) * .4f);
            var small = Outline(halfX - bevel, halfZ - bevel, bevel * .5f);
            var full = Outline(halfX, halfZ, bevel);
            Solid(mesh, new[]
            {
                Level(small, bottom, slopedBottom ? TopSlope : 0),
                Level(full, bottom + bevel, slopedBottom ? TopSlope : 0),
                Level(full, top - bevel, TopSlope),
                Level(small, top, TopSlope)
            });
            return mesh;
        }

        private static MeshBuilder Box(Vector3 center, Vector3 size, float bevel)
        {
            var mesh = new MeshBuilder();
            AddBox(mesh, center, size, bevel);
            return mesh;
        }

        private static void AddBox(MeshBuilder mesh, Vector3 center, Vector3 size, float bevel)
        {
            bevel = Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * .4f);
            var small = Outline(size.x * .5f - bevel, size.z * .5f - bevel, bevel * .5f);
            var full = Outline(size.x * .5f, size.z * .5f, bevel);
            Solid(mesh, new[]
            {
                Level(small, -size.y * .5f, 0, center),
                Level(full, -size.y * .5f + bevel, 0, center),
                Level(full, size.y * .5f - bevel, 0, center),
                Level(small, size.y * .5f, 0, center)
            });
        }

        private static MeshBuilder Ring(float outerX, float outerZ, float innerX, float innerZ, float bottom, float top, float bevel)
        {
            var mesh = new MeshBuilder();
            var outside = Outline(outerX, outerZ, bevel);
            var inside = Outline(innerX, innerZ, bevel);
            var low = Level(outside, bottom);
            var high = Level(outside, top);
            var insideLow = Level(inside, bottom);
            var insideHigh = Level(inside, top);
            for (var i = 0; i < outside.Length; i++)
            {
                var next = (i + 1) % outside.Length;
                mesh.Quad(low[i], high[i], high[next], low[next]);
                mesh.Quad(insideLow[next], insideHigh[next], insideHigh[i], insideLow[i]);
                mesh.Quad(high[i], insideHigh[i], insideHigh[next], high[next]);
                mesh.Quad(low[i], low[next], insideLow[next], insideLow[i]);
            }
            return mesh;
        }

        private static void AddCylinder(MeshBuilder mesh, Vector3 center, float radius, float depth, int sides)
        {
            var outline = new Vector2[sides];
            for (var i = 0; i < sides; i++)
            {
                var angle = i * Mathf.PI * 2 / sides;
                outline[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            Solid(mesh, new[] { Level(outline, -depth * .5f, 0, center), Level(outline, depth * .5f, 0, center) });
        }

        private static void Solid(MeshBuilder mesh, Vector3[][] rings)
        {
            for (var ring = 0; ring < rings.Length - 1; ring++)
                for (var i = 0; i < rings[ring].Length; i++)
                {
                    var next = (i + 1) % rings[ring].Length;
                    mesh.Quad(rings[ring][i], rings[ring + 1][i], rings[ring + 1][next], rings[ring][next]);
                }
            Cap(mesh, rings[0], false);
            Cap(mesh, rings[rings.Length - 1], true);
        }

        private static void Cap(MeshBuilder mesh, Vector3[] outline, bool top)
        {
            var center = Vector3.zero;
            foreach (var point in outline) center += point;
            center /= outline.Length;
            for (var i = 0; i < outline.Length; i++)
            {
                var next = (i + 1) % outline.Length;
                if (top) mesh.Triangle(center, outline[next], outline[i]);
                else mesh.Triangle(center, outline[i], outline[next]);
            }
        }

        private static void PixelDigit(MeshBuilder mesh, char digit, float pixel, float bottom, float top)
        {
            string[] rows;
            switch (digit)
            {
                case '1': rows = new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" }; break;
                case '2': rows = new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" }; break;
                case '3': rows = new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" }; break;
                default: throw new ArgumentOutOfRangeException(nameof(digit));
            }
            for (var row = 0; row < 7; row++)
                for (var column = 0; column < 5; column++)
                {
                    if (rows[row][column] != '1') continue;
                    var center = new Vector3((column - 2) * pixel, (bottom + top) * .5f, (3 - row) * pixel);
                    // Small closed boxes make the numerals physical raised pixels, with no font or texture dependency.
                    AddPixel(mesh, center, pixel * .98f, top - bottom);
                }
        }

        private static void AddPixel(MeshBuilder mesh, Vector3 center, float size, float height)
        {
            var half = size * .5f;
            var outline = new[] { new Vector2(-half, -half), new Vector2(half, -half), new Vector2(half, half), new Vector2(-half, half) };
            Solid(mesh, new[] { Level(outline, -height * .5f, 0, center), Level(outline, height * .5f, 0, center) });
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();

            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                var first = vertices.Count;
                var normal = Vector3.Cross(b - a, c - a).normalized;
                Vertex(a, normal); Vertex(b, normal); Vertex(c, normal);
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                var first = vertices.Count;
                var normal = Vector3.Cross(b - a, c - a).normalized;
                Vertex(a, normal); Vertex(b, normal); Vertex(c, normal); Vertex(d, normal);
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            }

            private void Vertex(Vector3 vertex, Vector3 normal)
            {
                vertices.Add(vertex);
                normals.Add(normal);
                uv.Add(new Vector2(vertex.x * 3 + .5f, vertex.z * 3 + .5f));
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
