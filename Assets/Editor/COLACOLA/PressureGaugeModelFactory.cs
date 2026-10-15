using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ColaCola.Editor
{
    /// <summary>Builds the faceted tabletop pressure gauge without imported fonts or models.</summary>
    public static class PressureGaugeModelFactory
    {
        public const string PrefabPath = "Assets/_COLA_COLA/Art/Prefabs/PressureGauge.prefab";
        private const string ModelPath = "Assets/_COLA_COLA/Art/Models/PressureGauge";
        private const string MaterialPath = "Assets/_COLA_COLA/Art/Materials/PressureGauge";
        private const int Sides = 12;
        private static readonly Vector2 DialCenter = new Vector2(0, .026f);

        public static GameObject BuildPrefab()
        {
            Directory.CreateDirectory(ModelPath);
            Directory.CreateDirectory(MaterialPath);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();

            var teal = Material("Enamel_Teal", new Color(.055f, .19f, .19f), .12f, .22f);
            var tealEdge = Material("Enamel_Edge", new Color(.095f, .27f, .26f), .15f, .25f);
            var darkTeal = Material("Enamel_Shadow", new Color(.035f, .10f, .105f), .1f, .18f);
            var metal = Material("Bezel_AgedMetal", new Color(.43f, .41f, .33f), .45f, .25f);
            var paleMetal = Material("Bezel_PaleMetal", new Color(.68f, .65f, .53f), .4f, .22f);
            var cream = Material("Dial_Cream", new Color(.79f, .74f, .59f), 0, .1f);
            var black = Material("Markings_Charcoal", new Color(.035f, .035f, .027f), 0, .1f);
            var red = Material("Needle_Red", new Color(.72f, .055f, .025f), .05f, .2f);
            var brass = Material("Hub_Brass", new Color(.52f, .33f, .10f), .6f, .3f);

            var root = new GameObject("PressureGauge");
            try
            {
                var housing = Group("Housing", root.transform);
                var housingMesh = new MeshBuilder();
                SolidLoft(housingMesh, new[]
                {
                    Ellipse(.155f, .162f, new Vector2(0, .018f)),
                    Ellipse(.170f, .177f, new Vector2(0, .018f)),
                    Ellipse(.170f, .177f, new Vector2(0, .018f)),
                    Ellipse(.155f, .162f, new Vector2(0, .018f))
                }, new[] { -.034f, -.019f, .035f, .052f });
                Part("FacetedBody", housing, "Housing", housingMesh, teal);
                Part("TopCap", housing, "TopCap", BevelBox(new Vector2(0, .193f), .108f, .017f, -.017f, .051f, .005f), tealEdge);
                Part("LowerFoot", housing, "LowerFoot", BevelBox(new Vector2(0, -.183f), .106f, .027f, -.025f, .050f, .006f), darkTeal);

                var bezel = Group("Bezel", root.transform);
                Part("OuterTwelveSidedRim", bezel, "OuterBezel", BevelRing(.149f, .154f, .132f, .137f, -.046f, -.030f, .003f), metal);
                Part("InnerRaisedRim", bezel, "InnerBezel", BevelRing(.134f, .139f, .122f, .127f, -.052f, -.043f, .002f), paleMetal);

                var dial = Group("Dial", root.transform);
                var dialMesh = new MeshBuilder();
                Extrude(dialMesh, Ellipse(.123f, .128f, DialCenter), -.054f, -.046f);
                Part("CreamFace", dial, "DialFace", dialMesh, cream);

                var ticks = Group("TickMarks", root.transform);
                var tickMesh = new MeshBuilder();
                // A 270-degree sweep leaves a clear region above the number window.
                for (var i = 0; i <= 18; i++)
                {
                    var angle = (225f - i * 15f) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var length = i % 3 == 0 ? .017f : .008f;
                    var width = i % 3 == 0 ? .004f : .0025f;
                    var center = DialCenter + direction * (.117f - length * .5f);
                    Extrude(tickMesh, OrientedRectangle(center, direction, length, width), -.056f, -.055f);
                }
                Part("RaisedBlackTicks", ticks, "TickMarks", tickMesh, black);
                var question = new MeshBuilder();
                PixelText(question, "?", new Vector2(.060f, -.026f), .0045f, -.057f, -.0555f);
                Part("HiddenLimit_QuestionMark", dial, "QuestionMark", question, black);

                var needle = Group("NeedlePivot", root.transform);
                needle.localPosition = new Vector3(DialCenter.x, DialCenter.y, -.058f);
                needle.localRotation = Quaternion.Euler(0, 0, 210);
                var needleMesh = new MeshBuilder();
                Extrude(needleMesh, new[]
                {
                    new Vector2(-.009f, -.004f), new Vector2(.077f, -.0035f),
                    new Vector2(.108f, 0), new Vector2(.077f, .0035f), new Vector2(-.009f, .004f)
                }, -.003f, -.001f);
                Part("RedPointer", needle, "Needle", needleMesh, red);
                var counterweight = new MeshBuilder();
                Extrude(counterweight, new[]
                {
                    new Vector2(-.048f, -.008f), new Vector2(-.039f, -.013f),
                    new Vector2(-.012f, -.005f), new Vector2(-.012f, .005f),
                    new Vector2(-.039f, .013f), new Vector2(-.048f, .008f)
                }, -.003f, -.001f);
                Part("BlackCounterweight", needle, "Counterweight", counterweight, black);
                var hub = new MeshBuilder();
                Extrude(hub, Ellipse(.012f, .012f, Vector2.zero), -.008f, .001f);
                Part("CenterHub", needle, "Hub", hub, black);
                var pin = new MeshBuilder();
                Extrude(pin, Ellipse(.0055f, .0055f, Vector2.zero, 8), -.010f, -.008f);
                Part("BrassAxle", needle, "BrassAxle", pin, brass);

                var numberWindow = Group("NumberWindow", root.transform);
                var windowCenter = new Vector2(0, -.153f);
                Part("MetalFrame", numberWindow, "NumberFrame", BevelBox(windowCenter, .077f, .037f, -.059f, -.024f, .004f), metal);
                var recess = new MeshBuilder();
                Extrude(recess, ChamferRectangle(windowCenter, .069f, .029f, .0015f), -.061f, -.0595f);
                Part("DarkInset", numberWindow, "NumberInset", recess, black);
                var plate = new MeshBuilder();
                Extrude(plate, ChamferRectangle(windowCenter, .065f, .025f, .0015f), -.063f, -.0615f);
                Part("CreamNumberPlate", numberWindow, "NumberPlate", plate, cream);
                var digits = new MeshBuilder();
                PixelText(digits, "15", windowCenter, .0046f, -.065f, -.064f);
                Part("CurrentPressure_15", numberWindow, "PressureDigits", digits, black);
                var fasteners = new MeshBuilder();
                foreach (var x in new[] { -.071f, .071f })
                    foreach (var y in new[] { -.180f, -.126f })
                        Extrude(fasteners, Ellipse(.0024f, .0024f, new Vector2(x, y), 6), -.061f, -.0595f);
                Part("FrameFasteners", numberWindow, "FrameFasteners", fasteners, paleMetal);

                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, 0, -.008f);
                collider.size = new Vector3(.34f, .42f, .12f);
                AssetDatabase.SaveAssets();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (!prefab) throw new InvalidOperationException("Could not save pressure gauge prefab.");
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Material Material(string name, Color color, float metallic, float smoothness)
        {
            var path = MaterialPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (!shader) throw new InvalidOperationException("A lit shader is required for the pressure gauge.");
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
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static void Part(string name, Transform parent, string meshName, MeshBuilder builder, Material material)
        {
            var mesh = builder.Build(meshName);
            var path = ModelPath + "/" + meshName + ".asset";
            var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (savedMesh)
            {
                EditorUtility.CopySerialized(mesh, savedMesh);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(savedMesh);
            }
            else
            {
                savedMesh = mesh;
                AssetDatabase.CreateAsset(savedMesh, path);
            }
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = savedMesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Vector2[] Ellipse(float radiusX, float radiusY, Vector2 center, int count = Sides)
        {
            var points = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2 / count;
                points[i] = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
            }
            return points;
        }

        private static Vector2[] ChamferRectangle(Vector2 center, float halfX, float halfY, float bevel)
        {
            bevel = Mathf.Min(bevel, Mathf.Min(halfX, halfY) * .8f);
            return new[]
            {
                center + new Vector2(-halfX + bevel, -halfY), center + new Vector2(halfX - bevel, -halfY),
                center + new Vector2(halfX, -halfY + bevel), center + new Vector2(halfX, halfY - bevel),
                center + new Vector2(halfX - bevel, halfY), center + new Vector2(-halfX + bevel, halfY),
                center + new Vector2(-halfX, halfY - bevel), center + new Vector2(-halfX, -halfY + bevel)
            };
        }

        private static Vector2[] OrientedRectangle(Vector2 center, Vector2 direction, float length, float width)
        {
            var along = direction * (length * .5f);
            var across = new Vector2(-direction.y, direction.x) * (width * .5f);
            return new[] { center - along - across, center + along - across, center + along + across, center - along + across };
        }

        private static MeshBuilder BevelBox(Vector2 center, float halfX, float halfY, float frontZ, float backZ, float bevel)
        {
            var mesh = new MeshBuilder();
            SolidLoft(mesh, new[]
            {
                ChamferRectangle(center, halfX - bevel, halfY - bevel, bevel * .5f),
                ChamferRectangle(center, halfX, halfY, bevel),
                ChamferRectangle(center, halfX, halfY, bevel),
                ChamferRectangle(center, halfX - bevel, halfY - bevel, bevel * .5f)
            }, new[] { frontZ, frontZ + bevel, backZ - bevel, backZ });
            return mesh;
        }

        private static MeshBuilder BevelRing(float outerX, float outerY, float innerX, float innerY, float frontZ, float backZ, float bevel)
        {
            var mesh = new MeshBuilder();
            var outerFront = Ellipse(outerX - bevel, outerY - bevel, DialCenter);
            var outerSide = Ellipse(outerX, outerY, DialCenter);
            var innerFront = Ellipse(innerX + bevel, innerY + bevel, DialCenter);
            var innerSide = Ellipse(innerX, innerY, DialCenter);
            Join(mesh, outerFront, frontZ, outerSide, frontZ + bevel);
            Join(mesh, outerSide, frontZ + bevel, outerSide, backZ);
            Join(mesh, innerFront, frontZ, innerSide, frontZ + bevel, true);
            Join(mesh, innerSide, frontZ + bevel, innerSide, backZ, true);
            for (var i = 0; i < outerFront.Length; i++)
            {
                var next = (i + 1) % outerFront.Length;
                mesh.Quad(V(outerFront[i], frontZ), V(innerFront[i], frontZ), V(innerFront[next], frontZ), V(outerFront[next], frontZ));
                mesh.Quad(V(outerSide[i], backZ), V(outerSide[next], backZ), V(innerSide[next], backZ), V(innerSide[i], backZ));
            }
            return mesh;
        }

        private static void SolidLoft(MeshBuilder mesh, Vector2[][] rings, float[] z)
        {
            for (var i = 0; i < rings.Length - 1; i++) Join(mesh, rings[i], z[i], rings[i + 1], z[i + 1]);
            Cap(mesh, rings[0], z[0], true);
            Cap(mesh, rings[rings.Length - 1], z[z.Length - 1], false);
        }

        private static void Extrude(MeshBuilder mesh, Vector2[] outline, float frontZ, float backZ)
        {
            Join(mesh, outline, frontZ, outline, backZ);
            Cap(mesh, outline, frontZ, true);
            Cap(mesh, outline, backZ, false);
        }

        private static void Join(MeshBuilder mesh, Vector2[] front, float frontZ, Vector2[] back, float backZ, bool reverse = false)
        {
            for (var i = 0; i < front.Length; i++)
            {
                var next = (i + 1) % front.Length;
                if (reverse) mesh.Quad(V(front[next], frontZ), V(front[i], frontZ), V(back[i], backZ), V(back[next], backZ));
                else mesh.Quad(V(front[i], frontZ), V(front[next], frontZ), V(back[next], backZ), V(back[i], backZ));
            }
        }

        private static void Cap(MeshBuilder mesh, Vector2[] outline, float z, bool front)
        {
            var center = Vector2.zero;
            foreach (var point in outline) center += point;
            center /= outline.Length;
            for (var i = 0; i < outline.Length; i++)
            {
                var next = (i + 1) % outline.Length;
                if (front) mesh.Triangle(V(center, z), V(outline[next], z), V(outline[i], z));
                else mesh.Triangle(V(center, z), V(outline[i], z), V(outline[next], z));
            }
        }

        private static Vector3 V(Vector2 xy, float z) { return new Vector3(xy.x, xy.y, z); }

        private static void PixelText(MeshBuilder mesh, string text, Vector2 center, float pixel, float frontZ, float backZ)
        {
            var width = (text.Length * 5 + (text.Length - 1)) * pixel;
            for (var letter = 0; letter < text.Length; letter++)
            {
                var glyph = Glyph(text[letter]);
                for (var row = 0; row < 7; row++)
                    for (var column = 0; column < 5; column++)
                    {
                        if (glyph[row][column] != '1') continue;
                        var pixelCenter = center + new Vector2(-width * .5f + (letter * 6 + column + .5f) * pixel, (3f - row) * pixel);
                        // One shared mesh per inscription; raised square pixels have their own flat side faces.
                        var half = pixel * .49f;
                        Extrude(mesh, OrientedRectangle(pixelCenter, Vector2.right, half * 2, half * 2), frontZ, backZ);
                    }
            }
        }

        private static string[] Glyph(char value)
        {
            switch (value)
            {
                case '?': return new[] { "01110", "10001", "00001", "00010", "00100", "00000", "00100" };
                case '1': return new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" };
                case '5': return new[] { "11111", "10000", "10000", "11110", "00001", "00001", "11110" };
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported gauge glyph.");
            }
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
                uv.Add(new Vector2(vertex.x * 3 + .5f, vertex.y * 3 + .5f));
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
