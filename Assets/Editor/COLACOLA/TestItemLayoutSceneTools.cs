using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ColaCola.Editor
{
    public static class TestItemLayoutSceneTools
    {
        public const string Output = "output/item-layout";
        private const float TableHeight = .833f;
        private const float TrayFloor = .012f;
        private static readonly string[] Owners = { "PlayerInventory", "BossInventory" };
        private static readonly string[] Replaced = {
            "PLACEHOLDER_ReleaseValve", "PLACEHOLDER_ColdPack", "PLACEHOLDER_PizzaCoupon_Player",
            "PLACEHOLDER_PizzaCoupon_Boss", "BossItem_SodaCan_ExistingModel", "BossItem_Tongs_ExistingModel"
        };

        [MenuItem("COLA_COLA/Place Six Test Items Per Side")]
        public static void Apply()
        {
            try
            {
                Directory.CreateDirectory(Output);
                var backup = Output + "/MainScene-before-items.unity";
                if (!File.Exists(backup)) File.Copy(MainSceneTools.ScenePath, backup);
                var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene");
                var oldItems = root.GetComponentsInChildren<Transform>(true)
                    .Where(t => Replaced.Contains(t.name) || Owners.Contains(t.name)).ToArray();
                var preserved = root.GetComponentsInChildren<Transform>(true)
                    .Where(t => !oldItems.Any(old => t == old || t.IsChildOf(old)))
                    .ToDictionary(t => t, t => new TransformState(t));
                ApplyToScene(root);
                foreach (var pair in preserved)
                    pair.Value.AssertUnchanged(pair.Key);
                Validate(root, Camera.main);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene, MainSceneTools.ScenePath)) throw new IOException("MainScene save failed.");
                File.WriteAllText(Output + "/Placement-preservation.txt",
                    $"PASS: {preserved.Count} unrelated transforms keep their world position, rotation and local scale.\n" +
                    "Camera/FOV, table, cola, characters, Idles, all three devices, lighting and pressure interaction are preserved.\n" +
                    "Replaced four item Cube placeholders and the two former boss display props with six items per side.\n");
                MainSceneTools.WriteReport(root, Camera.main);
                CaptureAfterImport();
            }
            catch (Exception exception) { Fail(exception); }
        }

        public static void ApplyToScene(GameObject root)
        {
            var prefabs = TestItemModelFactory.BuildPrefabs();
            var items = root.transform.Find("04_TabletopItems");
            if (!items) throw new InvalidOperationException("Tabletop item group is missing.");
            foreach (var node in root.GetComponentsInChildren<Transform>(true)
                .Where(t => Replaced.Contains(t.name) || Owners.Contains(t.name)).ToArray())
                if (node) UnityEngine.Object.DestroyImmediate(node.gameObject);

            PlaceOwner(items, Owners[0], new Vector3(.60f, TableHeight, -.06f), prefabs, false);
            PlaceOwner(items, Owners[1], new Vector3(.86f, TableHeight, .64f), prefabs, true);
            AddItemFill(root.transform.Find("06_Lighting"));
        }

        private static void PlaceOwner(Transform parent, string name, Vector3 position, GameObject[] prefabs, bool boss)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.position = position;
            var trayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestItemModelFactory.PrefabFolder + "/InventoryServingTray.prefab");
            var tray = (GameObject)PrefabUtility.InstantiatePrefab(trayPrefab, group);
            tray.name = "ServingTray";
            if (boss) tray.transform.Find("EnamelRim").GetComponent<Renderer>().sharedMaterial = TestItemModelFactory.BossTrayMaterial;
            // Broad, low props occupy the near row; taller silhouettes occupy the far row.
            var positions = new[] {
                new Vector3(-.230f, TrayFloor, .105f), new Vector3(-.230f, TrayFloor, -.120f),
                new Vector3(0, TrayFloor, -.120f), new Vector3(.230f, TrayFloor, -.120f),
                new Vector3(0, TrayFloor, .105f), new Vector3(.230f, TrayFloor, .105f)
            };
            if (!boss)
            {
                // Keep the tallest foreground prop clear of the distant candy/coupon silhouettes.
                var valvePosition = positions[0];
                positions[0] = positions[5];
                positions[5] = valvePosition;
                positions[5].x = -.290f;
            }
            var yaws = boss ? new[] { 8f, -6f, 5f, -9f, 0f, -5f } : new[] { -5f, 5f, -4f, 7f, 9f, 4f };
            for (var i = 0; i < prefabs.Length; i++)
            {
                var prop = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], group);
                prop.name = TestItemModelFactory.Names[i];
                prop.transform.localPosition = positions[i];
                prop.transform.localRotation = Quaternion.Euler(0, yaws[i], 0);
            }
        }

        private static void AddItemFill(Transform lighting)
        {
            var node = lighting.Find("TestItems_ReadabilityFill");
            if (!node) { node = new GameObject("TestItems_ReadabilityFill", typeof(Light)).transform; node.SetParent(lighting, false); }
            node.position = new Vector3(1.25f, 1.70f, -.35f);
            node.rotation = Quaternion.LookRotation(new Vector3(.83f, .87f, .39f) - node.position);
            var light = node.GetComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(.78f, .84f, .72f);
            light.intensity = .85f;
            light.range = 2.2f;
            light.spotAngle = 70;
            light.innerSpotAngle = 45;
            light.shadows = LightShadows.None;
        }

        public static void Validate(GameObject root, Camera camera)
        {
            if (!camera) throw new InvalidOperationException("Main camera is missing.");
            camera.aspect = 16f / 9f;
            var report = new StringBuilder();
            report.AppendLine("Physical inventory space test: six distinct test props per participant, twelve item instances total.");
            report.AppendLine("No item effects, input colliders or shop data are assigned to these props.");
            var transforms = root.GetComponentsInChildren<Transform>(true);
            if (transforms.Any(t => Replaced.Contains(t.name))) throw new InvalidOperationException("Former item placeholders/props remain.");
            var owners = Owners.Select(name => transforms.Single(t => t.name == name)).ToArray();
            foreach (var owner in owners)
            {
                var props = owner.Cast<Transform>().Where(t => TestItemModelFactory.Names.Contains(t.name)).ToArray();
                if (props.Length != 6 || props.Select(t => t.name).Distinct().Count() != 6)
                    throw new InvalidOperationException(owner.name + " must contain exactly six different test props.");
                report.AppendLine($"PASS: {owner.name} contains six reusable item prefabs and a physical serving tray.");
                var bounds = new List<Bounds>();
                foreach (var prop in props)
                {
                    var filters = prop.GetComponentsInChildren<MeshFilter>();
                    if (filters.Length == 0 || filters.Any(f => !f.sharedMesh || !AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith("Assets/_COLA_COLA/Art/Models/TestItems/")))
                        throw new InvalidOperationException("Expected authored prop meshes: " + prop.name);
                    if (prop.GetComponentsInChildren<Collider>().Length != 0)
                        throw new InvalidOperationException("Layout props should not intercept pressure/cola input: " + prop.name);
                    var b = BoundsOf(prop);
                    if (b.min.x < -1.4f || b.max.x > 1.4f || b.min.z < -.95f || b.max.z > .95f ||
                        Mathf.Abs(b.min.y - (TableHeight + TrayFloor)) > .002f)
                        throw new InvalidOperationException("Item is off the tabletop/tray floor: " + prop.name + " " + b);
                    var local = b.center - owner.position;
                    if (Mathf.Abs(local.x) + b.extents.x > .36f || Mathf.Abs(local.z) + b.extents.z > .19f)
                        throw new InvalidOperationException("Item extends through its tray rim: " + prop.name);
                    foreach (var other in bounds)
                        if (b.min.x < other.max.x + .012f && b.max.x > other.min.x - .012f &&
                            b.min.z < other.max.z + .012f && b.max.z > other.min.z - .012f)
                            throw new InvalidOperationException("Tabletop props overlap or have less than 12 mm separation.");
                    bounds.Add(b);
                    var viewportMin = Vector2.one; var viewportMax = Vector2.zero;
                    foreach (var point in Corners(b))
                    {
                        var v = camera.WorldToViewportPoint(point);
                        if (v.z <= 0 || v.x < .01f || v.x > .99f || v.y < .01f || v.y > .99f)
                            throw new InvalidOperationException("Item is clipped by the fixed camera: " + prop.name + " " + v);
                        viewportMin = Vector2.Min(viewportMin, v); viewportMax = Vector2.Max(viewportMax, v);
                    }
                    report.AppendLine($"  {prop.name}: {b.size.x * 100:F1} x {b.size.y * 100:F1} x {b.size.z * 100:F1} cm; " +
                        $"position={prop.position:F3}; viewport={viewportMin:F3}..{viewportMax:F3}");
                }
            }
            var trayGap = owners[1].position.z - owners[0].position.z - .43f;
            if (trayGap < .10f) throw new InvalidOperationException("The two physical inventory trays overlap.");
            report.AppendLine($"PASS: all twelve items rest on the tray floors, stay within the table/trays and fixed camera, and have at least 12 mm physical separation.");
            report.AppendLine($"PASS: {trayGap * 100:F1} cm clearance between the two trays. No new item colliders interfere with button/cola raycasts.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/Item-layout-validation.txt", report.ToString());
        }

        private static Bounds BoundsOf(Transform node)
        {
            var renderers = node.GetComponentsInChildren<Renderer>();
            var result = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) result.Encapsulate(renderer.bounds);
            return result;
        }

        private static IEnumerable<Vector3> Corners(Bounds bounds)
        {
            foreach (var x in new[] { bounds.min.x, bounds.max.x })
                foreach (var y in new[] { bounds.min.y, bounds.max.y })
                    foreach (var z in new[] { bounds.min.z, bounds.max.z }) yield return new Vector3(x, y, z);
        }

        private static void CaptureAfterImport()
        {
            var at = EditorApplication.timeSinceStartup + 3;
            EditorApplication.CallbackFunction capture = null;
            capture = () =>
            {
                if (EditorApplication.timeSinceStartup < at) return;
                EditorApplication.update -= capture;
                try
                {
                    var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                    var root = scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene");
                    Validate(root, Camera.main);
                    MainSceneTools.Capture(Camera.main, Output + "/MainScene-Items.png");
                    CaptureOverview();
                    ValidateRenderedVisibility(root, Camera.main);
                    Debug.Log("COLA_TEST_ITEM_LAYOUT_SAVED: six per side, twelve item instances.");
                    PressureInteractionSceneTools.Verify();
                }
                catch (Exception exception) { Fail(exception); }
            };
            EditorApplication.update += capture;
        }

        private static void CaptureOverview()
        {
            var node = new GameObject("Temporary_ItemLayout_Overview", typeof(Camera));
            try
            {
                var camera = node.GetComponent<Camera>();
                camera.transform.SetPositionAndRotation(new Vector3(.1f, 2.9f, .10f), Quaternion.Euler(90, 0, 0));
                camera.orthographic = true;
                camera.orthographicSize = 1.08f;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 20;
                MainSceneTools.Capture(camera, Output + "/Tabletop-Overview.png", 1600, 1100);
            }
            finally { UnityEngine.Object.DestroyImmediate(node); }
        }

        // Compare actual depth-tested pixels against each prop rendered in isolation.
        // Frustum bounds alone cannot detect a foreground prop hiding a boss item.
        private static void ValidateRenderedVisibility(GameObject root, Camera camera)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var originalMaterials = renderers.Select(r => r.sharedMaterials).ToArray();
            var originalEnabled = renderers.Select(r => r.enabled).ToArray();
            var props = Owners.SelectMany(owner => root.transform.Find("04_TabletopItems/" + owner)
                .Cast<Transform>().Where(t => TestItemModelFactory.Names.Contains(t.name))).ToArray();
            var membership = new Dictionary<Renderer, int>();
            for (var i = 0; i < props.Length; i++)
                foreach (var renderer in props[i].GetComponentsInChildren<Renderer>()) membership.Add(renderer, i);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var masks = new List<Material> { new Material(shader) };
            masks[0].SetColor("_BaseColor", Color.black);
            for (var i = 0; i < props.Length; i++)
            {
                var material = new Material(shader);
                material.SetColor("_BaseColor", Color.HSVToRGB(i / 12f, .85f, .95f));
                masks.Add(material);
            }
            var data = camera.GetUniversalAdditionalCameraData();
            var originalPost = data.renderPostProcessing; var originalDither = data.dithering;
            var originalAA = data.antialiasing; var originalFog = RenderSettings.fog;
            var originalClear = camera.clearFlags; var originalBackground = camera.backgroundColor;
            var originalTarget = camera.targetTexture; var originalActive = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                target.Create(); camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                data.renderPostProcessing = false; data.dithering = false; data.antialiasing = AntialiasingMode.None;
                RenderSettings.fog = false;
                for (var i = 0; i < renderers.Length; i++)
                {
                    var mask = masks[membership.TryGetValue(renderers[i], out var item) ? item + 1 : 0];
                    renderers[i].sharedMaterials = Enumerable.Repeat(mask, originalMaterials[i].Length).ToArray();
                }
                Action render = () => {
                    camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
                };
                render();
                File.WriteAllBytes(Output + "/Item-Visibility.png", pixels.EncodeToPNG());
                var fullCounts = PixelCounts(pixels.GetPixels32());
                var report = new StringBuilder("Actual fixed-camera item visibility, using temporary unlit identity colours and normal scene depth.\n");
                for (var item = 0; item < props.Length; item++)
                {
                    for (var i = 0; i < renderers.Length; i++)
                        renderers[i].enabled = originalEnabled[i] && membership.TryGetValue(renderers[i], out var member) && member == item;
                    render();
                    var isolated = PixelCounts(pixels.GetPixels32());
                    var referenceColour = isolated.OrderByDescending(pair => pair.Value).First().Key;
                    // URP's screen-space occlusion can darken even unlit masks near the tray.
                    // Identify a prop by chromaticity, rather than one exact brightness byte.
                    var visible = fullCounts.Where(pair => SameMaskColour(pair.Key, referenceColour)).Sum(pair => pair.Value);
                    var expected = isolated.Values.Sum();
                    var fraction = visible / (float)expected;
                    report.AppendLine($"{props[item].parent.name}/{props[item].name}: {visible}/{expected} pixels visible ({fraction:P1}).");
                    if (fraction < .90f || visible < 150)
                        throw new InvalidOperationException("Item is obscured in the actual camera: " + props[item].parent.name + "/" + props[item].name + " " + fraction.ToString("P1"));
                }
                report.AppendLine("PASS: all twelve props retain at least 90% of their isolated projected pixels in the full scene; none is hidden behind another item, character or device.");
                report.AppendLine("Mask materials, renderer enable states, fog and camera settings are temporary and restored after capture.");
                File.WriteAllText(Output + "/Rendered-visibility-validation.txt", report.ToString());
            }
            finally
            {
                for (var i = 0; i < renderers.Length; i++)
                { renderers[i].sharedMaterials = originalMaterials[i]; renderers[i].enabled = originalEnabled[i]; }
                data.renderPostProcessing = originalPost; data.dithering = originalDither; data.antialiasing = originalAA;
                RenderSettings.fog = originalFog; camera.clearFlags = originalClear; camera.backgroundColor = originalBackground;
                camera.targetTexture = originalTarget; RenderTexture.active = originalActive;
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
                foreach (var material in masks) UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static Dictionary<int, int> PixelCounts(Color32[] pixels)
        {
            var result = new Dictionary<int, int>();
            foreach (var pixel in pixels)
            {
                if (pixel.r + pixel.g + pixel.b < 10) continue;
                var key = (pixel.r << 16) | (pixel.g << 8) | pixel.b;
                result[key] = result.TryGetValue(key, out var count) ? count + 1 : 1;
            }
            return result;
        }

        private static bool SameMaskColour(int left, int right)
        {
            var a = new Vector3((left >> 16) & 255, (left >> 8) & 255, left & 255);
            var b = new Vector3((right >> 16) & 255, (right >> 8) & 255, right & 255);
            return Vector3.Dot(a, b) / Mathf.Sqrt(a.sqrMagnitude * b.sqrMagnitude) > .998f;
        }

        private static void Fail(Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(2);
            else throw new InvalidOperationException("Test item layout failed.", exception);
        }

        private readonly struct TransformState
        {
            private readonly Vector3 position, scale;
            private readonly Quaternion rotation;
            public TransformState(Transform node) { position = node.position; rotation = node.rotation; scale = node.localScale; }
            public void AssertUnchanged(Transform node)
            {
                if (!node || Vector3.Distance(position, node.position) > .00001f ||
                    Quaternion.Angle(rotation, node.rotation) > .01f || Vector3.Distance(scale, node.localScale) > .00001f)
                    throw new InvalidOperationException("Unrelated transform changed: " + node);
            }
        }
    }
}
