using System;
using System.Collections.Generic;
using System.IO;
using ColaCola.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColaCola.Editor
{
    /// <summary>Authors the pizzeria's shared tabletop status terminal from reusable meshes.</summary>
    public static class StatusTerminalModelFactory
    {
        public const string PrefabPath = "Assets/_COLA_COLA/Art/Prefabs/StatusTerminal.prefab";
        private const string ModelPath = "Assets/_COLA_COLA/Art/Models/StatusTerminal";
        private const string MaterialPath = "Assets/_COLA_COLA/Art/Materials/StatusTerminal";

        // The front points down local -Z. Origin is the feet's table contact, in metres.
        public static GameObject BuildPrefab()
        {
            Directory.CreateDirectory(ModelPath);
            Directory.CreateDirectory(MaterialPath);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();

            var enamel = Material("Enamel_Teal", new Color(.065f, .195f, .18f), .15f, .22f);
            var edge = Material("Enamel_Edge", new Color(.11f, .275f, .24f), .18f, .22f);
            var cream = Material("Bezel_WarmCream", new Color(.65f, .61f, .45f), .15f, .20f);
            var shadow = Material("Inset_DeepTeal", new Color(.025f, .06f, .051f), .10f, .16f);
            var rubber = Material("Rubber_Charcoal", new Color(.026f, .029f, .025f), 0, .08f);
            var steel = Material("Hardware_AgedSteel", new Color(.35f, .34f, .28f), .65f, .24f);
            var chip = Material("Wear_ExposedUndercoat", new Color(.29f, .31f, .24f), .20f, .12f);
            var rust = Material("Wear_OxidizedCorners", new Color(.22f, .115f, .055f), .05f, .10f);
            // Phosphor is self-lit, so it must not depend on scene lights or Lit emission keywords.
            // These colours stay below HDR white; the existing scene bloom remains restrained.
            var screen = ScreenMaterial("CRT_DarkGlass", new Color(.012f, .022f, .016f));
            var labels = ScreenMaterial("CRT_LabelPhosphor", new Color(.46f, .64f, .37f));
            var healthOn = ScreenMaterial("CRT_HealthGreen", new Color(.34f, .74f, .30f));
            var healthOff = ScreenMaterial("CRT_HealthOff", new Color(.035f, .082f, .044f));
            var tokens = ScreenMaterial("CRT_TokenAmber", new Color(.91f, .51f, .13f));
            var damage = ScreenMaterial("CRT_DamageRed", new Color(.88f, .17f, .075f));
            var scanlines = TransparentMaterial("CRT_Scanlines", new Color(.003f, .012f, .005f, .09f));

            var root = new GameObject("StatusTerminal");
            try
            {
                var housing = Group("Housing", root.transform);
                Part("FacetedEnamelShell", housing, "Shell", BevelBox(new Vector2(0, .157f),
                    .275f, .135f, -.071f, .080f, .012f), enamel);
                Part("TopFoldedLip", housing, "TopLip", BevelBox(new Vector2(0, .290f),
                    .259f, .010f, -.060f, .068f, .005f), edge);
                Part("BottomMetalPlinth", housing, "BottomPlinth", BevelBox(new Vector2(0, .025f),
                    .259f, .013f, -.067f, .070f, .006f), shadow);

                var feet = new MeshBuilder();
                foreach (var x in new[] { -.209f, .209f })
                    foreach (var z in new[] { -.039f, .045f })
                        AddBevelBox(feet, new Vector2(x, .009f), .024f, .009f,
                            z - .022f, z + .022f, .004f);
                Part("FourRubberFeet", housing, "Feet", feet, rubber);

                // Front tilts toward the seated player's eyes; the housing stays planted on the table.
                var face = Group("AngledDisplayAssembly", root.transform);
                face.localPosition = new Vector3(0, .169f, -.084f);
                face.localRotation = Quaternion.Euler(8, 0, 0);
                Part("CreamChamferedBezel", face, "CreamBezel", Ring(.262f, .114f,
                    .239f, .098f, -.015f, .004f, .008f), cream);
                Part("RecessedScreenGasket", face, "ScreenGasket", Ring(.242f, .100f,
                    .231f, .092f, -.017f, -.008f, .005f), rubber);

                var glass = new MeshBuilder();
                Extrude(glass, ChamferRectangle(Vector2.zero, .233f, .094f, .008f), -.018f, -.013f);
                Part("OpaqueInsetCRTGlass", face, "DarkScreen", glass, screen);

                var hardware = new MeshBuilder();
                var screwSlots = new MeshBuilder();
                foreach (var x in new[] { -.250f, .250f })
                    foreach (var y in new[] { -.102f, .102f })
                    {
                        Extrude(hardware, Circle(new Vector2(x, y), .0035f, 8), -.0174f, -.0149f);
                        Extrude(screwSlots, Rectangle(new Vector2(x, y), .0024f, .00065f),
                            -.0177f, -.0175f);
                    }
                Part("FourBezelScrews", face, "BezelScrews", hardware, steel);
                Part("FlatHeadScrewSlots", face, "ScrewSlots", screwSlots, rubber);

                var graphic = new MeshBuilder();
                // The divider and panel titles are part of the physical screen, with no font dependency.
                FlatRectangle(graphic, new Vector2(0, 0), .0008f, .080f, -.0210f);
                foreach (var panelX in new[] { -.117f, .117f })
                {
                    PixelText(graphic, panelX < 0 ? "BOSS" : "PLAYER", new Vector2(panelX, .074f),
                        .00275f, -.0215f);
                    PixelText(graphic, "HP", new Vector2(panelX - .064f, .008f),
                        .0020f, -.0215f);
                    PixelText(graphic, "/", new Vector2(panelX + .022f, .008f),
                        .0025f, -.0215f);
                    PixelText(graphic, "TOKENS", new Vector2(panelX, -.027f),
                        .00195f, -.0215f);
                }
                Part("FixedPixelLabelsAndDivider", face, "PixelLabels", graphic, labels);

                var digitMeshes = new Mesh[10];
                for (var digit = 0; digit < digitMeshes.Length; digit++)
                {
                    var glyph = new MeshBuilder();
                    PixelText(glyph, digit.ToString(), Vector2.zero, 1, -.04f);
                    digitMeshes[digit] = SaveMesh("PixelDigit_" + digit, glyph);
                }

                var segment = new MeshBuilder();
                FlatRectangle(segment, Vector2.zero, .0074f, .0075f, -.0005f);
                var segmentMesh = SaveMesh("HealthSegment", segment);
                var lamp = new MeshBuilder();
                Cap(lamp, ChamferRectangle(Vector2.zero, .0032f, .0032f, .001f), -.0005f, true);
                var lampMesh = SaveMesh("TurnLamp", lamp);

                var display = root.AddComponent<StatusTerminalDisplay>();
                display.digitMeshes = digitMeshes;
                display.healthOnMaterial = healthOn;
                display.healthOffMaterial = healthOff;
                display.tokenMaterial = tokens;
                display.damageMaterial = damage;
                display.boss = BuildPanel("Boss", face, -.117f, digitMeshes, segmentMesh,
                    lampMesh, healthOn, tokens);
                display.player = BuildPanel("Player", face, .117f, digitMeshes, segmentMesh,
                    lampMesh, healthOn, tokens);
                display.SetBossState(3, 3, 12, false);
                display.SetPlayerState(3, 3, 15, false);

                // Thin translucent geometry softens the phosphor without an extra render feature.
                var raster = new MeshBuilder();
                for (var i = 0; i < 65; i++)
                    FlatRectangle(raster, new Vector2(0, -.088f + i * .00275f),
                        .226f, .00015f, -.0230f);
                var rasterPart = Part("SubtleCRTScanlines", face, "Scanlines", raster, scanlines);
                rasterPart.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                rasterPart.GetComponent<MeshRenderer>().receiveShadows = false;

                BuildHousingDetails(housing, face, steel, rubber, chip, rust, cream);

                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, .150f, -.014f);
                collider.size = new Vector3(.55f, .300f, .195f);
                EditorUtility.SetDirty(display);
                AssetDatabase.SaveAssets();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (!prefab) throw new InvalidOperationException("Could not save the status terminal prefab.");
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static StatusTerminalDisplay.Panel BuildPanel(string name, Transform face, float x,
            Mesh[] digits, Mesh segmentMesh, Mesh lampMesh, Material health, Material tokens)
        {
            var parent = Group(name + "Display", face);
            var panel = new StatusTerminalDisplay.Panel
            {
                healthSegments = new Renderer[10],
                currentHealthDigits = new MeshFilter[2],
                maxHealthDigits = new MeshFilter[2],
                tokenDigits = new MeshFilter[4]
            };
            for (var i = 0; i < panel.healthSegments.Length; i++)
            {
                var part = ObjectWithMesh("HealthCell_" + (i + 1).ToString("00"), parent, segmentMesh, health);
                part.transform.localPosition = new Vector3(x - .0855f + i * .019f, .038f, -.0222f);
                panel.healthSegments[i] = part.GetComponent<Renderer>();
            }
            for (var i = 0; i < 2; i++)
            {
                panel.currentHealthDigits[i] = Digit("CurrentHP_" + i, parent, digits[0], health,
                    new Vector3(x - .0135f + i * .0180f, .008f, -.0222f), .0030f);
                panel.maxHealthDigits[i] = Digit("MaximumHP_" + i, parent, digits[0], health,
                    new Vector3(x + .045f + i * .0180f, .008f, -.0222f), .0030f);
            }
            for (var i = 0; i < 4; i++)
                panel.tokenDigits[i] = Digit("Tokens_" + i, parent, digits[0], tokens,
                    new Vector3(x - .0342f + i * .0228f, -.063f, -.0222f), .0038f);
            var turnLamp = ObjectWithMesh("TurnIndicator", parent, lampMesh, health);
            turnLamp.transform.localPosition = new Vector3(x - .095f, .074f, -.0222f);
            panel.turnLamp = turnLamp.GetComponent<Renderer>();
            return panel;
        }

        private static MeshFilter Digit(string name, Transform parent, Mesh mesh, Material material,
            Vector3 position, float scale)
        {
            var part = ObjectWithMesh(name, parent, mesh, material);
            part.transform.localPosition = position;
            part.transform.localScale = Vector3.one * scale;
            return part.GetComponent<MeshFilter>();
        }

        private static void BuildHousingDetails(Transform housing, Transform face, Material steel,
            Material rubber, Material chip, Material rust, Material cream)
        {
            var vents = new MeshBuilder();
            for (var side = -1; side <= 1; side += 2)
            {
                var x = side * .2751f;
                for (var i = 0; i < 6; i++)
                {
                    var y = .108f + i * .016f;
                    var a = new Vector3(x, y, -.025f);
                    var b = new Vector3(x, y + .0055f, -.025f);
                    var c = new Vector3(x, y + .0055f, .046f);
                    var d = new Vector3(x, y, .046f);
                    if (side > 0) vents.Quad(a, b, c, d);
                    else vents.Quad(d, c, b, a);
                }
            }
            Part("InsetSideVentSlots", housing, "SideVents", vents, rubber);

            var rear = new MeshBuilder();
            AddBevelBox(rear, new Vector2(0, .160f), .203f, .090f, .0795f, .0820f, .004f);
            Part("RearServiceCover", housing, "ServiceCover", rear, steel);

            var cable = new MeshBuilder();
            Tube(cable, new[]
            {
                new Vector3(.177f, .043f, .080f), new Vector3(.180f, .025f, .094f),
                new Vector3(.164f, .008f, .107f), new Vector3(.126f, .004f, .110f),
                new Vector3(.089f, .004f, .098f), new Vector3(.057f, .004f, .105f)
            }, .0038f, 6);
            Part("ShortRearPowerLead", housing, "PowerLead", cable, rubber);

            var plate = new MeshBuilder();
            Extrude(plate, ChamferRectangle(new Vector2(-.018f, .043f), .066f, .009f, .002f),
                -.0730f, -.0710f);
            Part("SmallMakerPlate", housing, "MakerPlate", plate, steel);
            var maker = new MeshBuilder();
            PixelText(maker, "COLA", new Vector2(-.018f, .043f), .00175f, -.0738f);
            Part("PrintedMakerMark", housing, "MakerMark", maker, cream);

            // A few deliberate chips at handling points, rather than a high-frequency noise texture.
            var wear = new MeshBuilder();
            Extrude(wear, new[] { new Vector2(-.245f, .109f), new Vector2(-.228f, .109f),
                new Vector2(-.235f, .105f), new Vector2(-.245f, .106f) }, -.0152f, -.0150f);
            Extrude(wear, new[] { new Vector2(.226f, -.108f), new Vector2(.244f, -.108f),
                new Vector2(.240f, -.104f), new Vector2(.231f, -.105f) }, -.0152f, -.0150f);
            Extrude(wear, Rectangle(new Vector2(-.207f, -.105f), .009f, .0006f), -.0152f, -.0150f);
            Extrude(wear, Rectangle(new Vector2(.200f, .106f), .005f, .0006f), -.0152f, -.0150f);
            Part("SparseBezelEdgeChips", face, "BezelWear", wear, chip);
            var corrosion = new MeshBuilder();
            Extrude(corrosion, Rectangle(new Vector2(-.255f, .274f), .0045f, .0015f), -.0709f, -.0707f);
            Extrude(corrosion, Rectangle(new Vector2(.249f, .034f), .005f, .0010f), -.0732f, -.0730f);
            Part("SmallOxidizedCorners", housing, "CornerOxidation", corrosion, rust);
        }

        private static Material Material(string name, Color color, float metallic, float smoothness)
        {
            var path = MaterialPath + "/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (!shader) throw new InvalidOperationException("A lit shader is required for the status terminal.");
            if (!result)
            {
                result = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(result, path);
            }
            else result.shader = shader;
            result.color = color;
            if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
            if (result.HasProperty("_Metallic")) result.SetFloat("_Metallic", metallic);
            if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", smoothness);
            if (result.HasProperty("_Glossiness")) result.SetFloat("_Glossiness", smoothness);
            result.DisableKeyword("_EMISSION");
            if (result.HasProperty("_EmissionColor")) result.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(result);
            return result;
        }

        private static Material ScreenMaterial(string name, Color color)
        {
            var path = MaterialPath + "/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) throw new InvalidOperationException("URP Unlit is required for the CRT phosphor.");
            if (!result)
            {
                result = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(result, path);
            }
            else result.shader = shader;
            // Clear stale Lit keywords when updating an existing material in place.
            result.shaderKeywords = Array.Empty<string>();
            result.SetColor("_BaseColor", color);
            result.color = color;
            result.SetFloat("_Surface", 0);
            result.SetFloat("_Blend", 0);
            result.SetFloat("_AlphaClip", 0);
            result.SetFloat("_SrcBlend", (float)BlendMode.One);
            result.SetFloat("_DstBlend", (float)BlendMode.Zero);
            result.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            result.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
            result.SetFloat("_ZWrite", 1);
            result.SetOverrideTag("RenderType", "Opaque");
            result.renderQueue = (int)RenderQueue.Geometry;
            result.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            result.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(result);
            return result;
        }

        private static Material TransparentMaterial(string name, Color color)
        {
            var result = ScreenMaterial(name, color);
            result.SetFloat("_Surface", 1);
            result.SetFloat("_Blend", 0);
            result.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            result.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            result.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            result.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            result.SetFloat("_ZWrite", 0);
            result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            result.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            result.SetOverrideTag("RenderType", "Transparent");
            result.renderQueue = (int)RenderQueue.Transparent;
            result.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(result);
            return result;
        }

        private static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static GameObject Part(string name, Transform parent, string meshName,
            MeshBuilder builder, Material material)
        {
            return ObjectWithMesh(name, parent, SaveMesh(meshName, builder), material);
        }

        private static GameObject ObjectWithMesh(string name, Transform parent, Mesh mesh, Material material)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (material.name.StartsWith("CRT_", StringComparison.Ordinal))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            return part;
        }

        private static Mesh SaveMesh(string name, MeshBuilder builder)
        {
            var path = ModelPath + "/" + name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved)
            {
                // CopySerialized updates the saved CPU buffers but can leave an already bound
                // MeshRenderer drawing stale native buffers until the next Editor process.
                // Native setters refresh the existing asset and preserve every prefab reference.
                builder.Populate(saved, name);
                EditorUtility.SetDirty(saved);
            }
            else
            {
                saved = builder.Build(name);
                AssetDatabase.CreateAsset(saved, path);
            }
            saved.MarkModified();
            saved.UploadMeshData(false);
            return saved;
        }

        private static MeshBuilder BevelBox(Vector2 center, float halfX, float halfY,
            float frontZ, float backZ, float bevel)
        {
            var result = new MeshBuilder();
            AddBevelBox(result, center, halfX, halfY, frontZ, backZ, bevel);
            return result;
        }

        private static void AddBevelBox(MeshBuilder mesh, Vector2 center, float halfX, float halfY,
            float frontZ, float backZ, float bevel)
        {
            bevel = Mathf.Min(bevel, (backZ - frontZ) * .45f, halfX * .45f, halfY * .45f);
            var front = ChamferRectangle(center, halfX - bevel, halfY - bevel, bevel * .5f);
            var side = ChamferRectangle(center, halfX, halfY, bevel);
            Join(mesh, front, frontZ, side, frontZ + bevel);
            Join(mesh, side, frontZ + bevel, side, backZ - bevel);
            Join(mesh, side, backZ - bevel, front, backZ);
            Cap(mesh, front, frontZ, true);
            Cap(mesh, front, backZ, false);
        }

        private static MeshBuilder Ring(float outerX, float outerY, float innerX, float innerY,
            float frontZ, float backZ, float bevel)
        {
            var mesh = new MeshBuilder();
            var outer = ChamferRectangle(Vector2.zero, outerX, outerY, .012f);
            var frontOuter = ChamferRectangle(Vector2.zero, outerX - bevel * .35f,
                outerY - bevel * .35f, .010f);
            var inner = ChamferRectangle(Vector2.zero, innerX, innerY, .008f);
            Join(mesh, frontOuter, frontZ, outer, frontZ + bevel);
            Join(mesh, outer, frontZ + bevel, outer, backZ);
            for (var i = 0; i < outer.Length; i++)
            {
                var next = (i + 1) % outer.Length;
                mesh.Quad(V(frontOuter[i], frontZ), V(inner[i], frontZ),
                    V(inner[next], frontZ), V(frontOuter[next], frontZ));
                mesh.Quad(V(inner[i], frontZ), V(inner[i], backZ),
                    V(inner[next], backZ), V(inner[next], frontZ));
                mesh.Quad(V(outer[i], backZ), V(outer[next], backZ),
                    V(inner[next], backZ), V(inner[i], backZ));
            }
            return mesh;
        }

        private static Vector2[] Rectangle(Vector2 center, float halfX, float halfY)
        {
            return new[] { center + new Vector2(-halfX, -halfY), center + new Vector2(halfX, -halfY),
                center + new Vector2(halfX, halfY), center + new Vector2(-halfX, halfY) };
        }

        private static Vector2[] ChamferRectangle(Vector2 center, float halfX, float halfY, float bevel)
        {
            return new[]
            {
                center + new Vector2(-halfX + bevel, -halfY), center + new Vector2(halfX - bevel, -halfY),
                center + new Vector2(halfX, -halfY + bevel), center + new Vector2(halfX, halfY - bevel),
                center + new Vector2(halfX - bevel, halfY), center + new Vector2(-halfX + bevel, halfY),
                center + new Vector2(-halfX, halfY - bevel), center + new Vector2(-halfX, -halfY + bevel)
            };
        }

        private static Vector2[] Circle(Vector2 center, float radius, int count)
        {
            var result = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2 / count;
                result[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return result;
        }

        private static void Tube(MeshBuilder mesh, Vector3[] points, float radius, int sides)
        {
            var rings = new Vector3[points.Length][];
            for (var i = 0; i < points.Length; i++)
            {
                var tangent = (points[Mathf.Min(i + 1, points.Length - 1)] -
                    points[Mathf.Max(i - 1, 0)]).normalized;
                var across = Vector3.Cross(tangent, Vector3.up).normalized;
                if (across.sqrMagnitude < .1f) across = Vector3.right;
                var up = Vector3.Cross(across, tangent).normalized;
                rings[i] = new Vector3[sides];
                for (var side = 0; side < sides; side++)
                {
                    var angle = side * Mathf.PI * 2 / sides;
                    rings[i][side] = points[i] + radius * (across * Mathf.Cos(angle) + up * Mathf.Sin(angle));
                }
            }
            for (var i = 0; i < points.Length - 1; i++)
                for (var side = 0; side < sides; side++)
                {
                    var next = (side + 1) % sides;
                    mesh.Quad(rings[i][side], rings[i + 1][side], rings[i + 1][next], rings[i][next]);
                }
            for (var side = 0; side < sides; side++)
            {
                var next = (side + 1) % sides;
                mesh.Triangle(points[0], rings[0][next], rings[0][side]);
                mesh.Triangle(points[points.Length - 1], rings[points.Length - 1][side], rings[points.Length - 1][next]);
            }
        }

        private static void Extrude(MeshBuilder mesh, Vector2[] outline, float frontZ, float backZ)
        {
            Join(mesh, outline, frontZ, outline, backZ);
            Cap(mesh, outline, frontZ, true);
            Cap(mesh, outline, backZ, false);
        }

        private static void Join(MeshBuilder mesh, Vector2[] front, float frontZ, Vector2[] back, float backZ)
        {
            for (var i = 0; i < front.Length; i++)
            {
                var next = (i + 1) % front.Length;
                mesh.Quad(V(front[i], frontZ), V(front[next], frontZ), V(back[next], backZ), V(back[i], backZ));
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

        private static void FlatRectangle(MeshBuilder mesh, Vector2 center, float halfX, float halfY, float z)
        {
            // Clockwise from the player's side; the normal points toward local -Z.
            mesh.Quad(new Vector3(center.x - halfX, center.y - halfY, z),
                new Vector3(center.x - halfX, center.y + halfY, z),
                new Vector3(center.x + halfX, center.y + halfY, z),
                new Vector3(center.x + halfX, center.y - halfY, z));
        }

        private static void PixelText(MeshBuilder mesh, string text, Vector2 center,
            float pixel, float frontZ)
        {
            var width = (text.Length * 6 - 1) * pixel;
            for (var letter = 0; letter < text.Length; letter++)
            {
                var glyph = Glyph(text[letter]);
                for (var row = 0; row < 7; row++)
                    for (var col = 0; col < 5; col++)
                    {
                        if (glyph[row][col] != '1') continue;
                        var pixelCenter = center + new Vector2(-width * .5f + (letter * 6 + col + .5f) * pixel,
                            (3 - row) * pixel);
                        // Pixels touch without narrow shadow-catching seams or hidden side/cap faces.
                        FlatRectangle(mesh, pixelCenter, pixel * .5f, pixel * .5f, frontZ);
                    }
            }
        }

        private static string[] Glyph(char value)
        {
            switch (value)
            {
                case '0': return new[] { "01110", "11011", "11011", "11011", "11011", "11011", "01110" };
                case '1': return new[] { "00110", "01110", "00110", "00110", "00110", "00110", "11111" };
                case '2': return new[] { "11110", "00011", "00011", "01110", "11000", "11000", "11111" };
                case '3': return new[] { "11110", "00011", "00011", "01110", "00011", "00011", "11110" };
                case '4': return new[] { "11011", "11011", "11011", "11111", "00011", "00011", "00011" };
                case '5': return new[] { "11111", "11000", "11000", "11110", "00011", "00011", "11110" };
                case '6': return new[] { "01110", "11000", "11000", "11110", "11011", "11011", "01110" };
                case '7': return new[] { "11111", "00011", "00011", "00110", "00110", "01100", "01100" };
                case '8': return new[] { "01110", "11011", "11011", "01110", "11011", "11011", "01110" };
                case '9': return new[] { "01110", "11011", "11011", "01111", "00011", "00011", "01110" };
                case 'A': return new[] { "01110", "11011", "11011", "11111", "11011", "11011", "11011" };
                case 'B': return new[] { "11110", "11011", "11011", "11110", "11011", "11011", "11110" };
                case 'C': return new[] { "01111", "11000", "11000", "11000", "11000", "11000", "01111" };
                case 'E': return new[] { "11111", "11000", "11000", "11110", "11000", "11000", "11111" };
                case 'H': return new[] { "11011", "11011", "11011", "11111", "11011", "11011", "11011" };
                case 'K': return new[] { "11011", "11011", "11110", "11100", "11110", "11011", "11011" };
                case 'L': return new[] { "11000", "11000", "11000", "11000", "11000", "11000", "11111" };
                case 'N': return new[] { "11001", "11101", "11101", "11011", "11011", "11001", "11001" };
                case 'O': return new[] { "01110", "11011", "11011", "11011", "11011", "11011", "01110" };
                case 'P': return new[] { "11110", "11011", "11011", "11110", "11000", "11000", "11000" };
                case 'R': return new[] { "11110", "11011", "11011", "11110", "11100", "11011", "11011" };
                case 'S': return new[] { "01111", "11000", "11000", "01110", "00011", "00011", "11110" };
                case 'T': return new[] { "11111", "00110", "00110", "00110", "00110", "00110", "00110" };
                case 'Y': return new[] { "11011", "11011", "11011", "01110", "00110", "00110", "00110" };
                case '/': return new[] { "00011", "00011", "00110", "00110", "01100", "11000", "11000" };
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported terminal glyph.");
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
                var normal = FaceNormal(a, b, c);
                Vertex(a, normal); Vertex(b, normal); Vertex(c, normal);
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                var first = vertices.Count;
                var normal = FaceNormal(a, b, c);
                Vertex(a, normal); Vertex(b, normal); Vertex(c, normal); Vertex(d, normal);
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            }

            private void Vertex(Vector3 point, Vector3 normal)
            {
                vertices.Add(point);
                normals.Add(normal);
                uv.Add(new Vector2(point.x + .5f, point.y + .5f));
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh();
                Populate(mesh, name);
                return mesh;
            }

            public void Populate(Mesh mesh, string name)
            {
                mesh.Clear(false);
                mesh.name = name;
                mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
            }

            private static Vector3 FaceNormal(Vector3 a, Vector3 b, Vector3 c)
            {
                var cross = Vector3.Cross(b - a, c - a);
                var length = cross.magnitude;
                // Vector3.normalized treats areas below 1e-5 as zero; millimetre-sized
                // pixel and screw faces still need real unit normals for shading passes.
                return length > 0 ? cross / length : Vector3.back;
            }
        }
    }
}
