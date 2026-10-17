using ColaCola.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ColaCola.Presentation
{
    /// <summary>In-turn order slip and a six-slot tabletop inventory made from actual Cube meshes.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaItemPresentation : MonoBehaviour
    {
        public bool OrderOpen => orderSheet && orderSheet.BlocksGameplay;
        private ColaOrderSheet orderSheet;
        private ColaItemController items;
        private GameObject inventoryRoot, bossInventoryRoot;
        private readonly GameObject[] cubes = new GameObject[ColaItemController.Capacity];
        private readonly GameObject[] bossCubes = new GameObject[ColaItemController.Capacity];
        private readonly Material[] materials = new Material[11];
        private Material playerShell, bossShell, ivoryRim, steelRim, paperLiner, playerAccent, bossAccent;
        private readonly List<Material> storageMaterials = new();
        private readonly List<Mesh> storageMeshes = new();
        private Font cubeFont, menuFont;
        private GUIStyle label, detail;
        private int hoveredSlot = -1;
        private bool hoveredBoss;
        private Vector2 mousePosition;
        private static readonly Color Ink = new(.81f, .82f, .68f);
        private static readonly Color Paper = new(.065f, .09f, .075f, .98f);
        private float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 960f, Screen.height / 640f), .6f, 1.2f);

        public void Bind(ColaItemController controller)
        {
            if (items) items.InventoryChanged -= RefreshInventory;
            items = controller;
            items.InventoryChanged += RefreshInventory;
            orderSheet = GetComponent<ColaOrderSheet>();
            EnsureProps();
            RefreshInventory();
        }

        public bool ToggleOrders()
        {
            if (!orderSheet) orderSheet = GetComponent<ColaOrderSheet>();
            return orderSheet && orderSheet.Toggle();
        }

        public bool BlocksWorldInput(Vector2 screenPoint)
        {
            if (!orderSheet) orderSheet = GetComponent<ColaOrderSheet>();
            return OrderOpen;
        }

        public Collider CubeCollider(int slot, bool boss = false)
        {
            var owned = boss ? bossCubes : cubes;
            return slot >= 0 && slot < owned.Length && owned[slot] && owned[slot].activeSelf ? owned[slot].GetComponent<Collider>() : null;
        }

        public Transform StorageRoot(bool boss = false) => (boss ? bossInventoryRoot : inventoryRoot).transform;

        private Material MakeMaterial(string materialName, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = materialName, hideFlags = HideFlags.DontSave };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_Metallic", 0);
            return material;
        }

        private void EnsureProps()
        {
            if (inventoryRoot && bossInventoryRoot) return;
            cubeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            playerShell = StorageMaterial("ServingTray_TealEnamel", new Color(.11f, .34f, .30f));
            bossShell = StorageMaterial("DeliveryCrate_RedWood", new Color(.38f, .13f, .085f));
            ivoryRim = StorageMaterial("ServingTray_IvoryRim", new Color(.70f, .65f, .49f));
            steelRim = StorageMaterial("DeliveryCrate_SteelBands", new Color(.34f, .37f, .31f));
            paperLiner = StorageMaterial("Pizzeria_PaperLiner", new Color(.65f, .59f, .43f));
            playerAccent = StorageMaterial("ServingTray_DarkChecks", new Color(.07f, .20f, .17f));
            bossAccent = StorageMaterial("DeliveryCrate_DarkRed", new Color(.22f, .055f, .035f));
            for (int i = 0; i < materials.Length; i++)
                materials[i] = MakeMaterial("ItemCube_" + (i + 1), Color.HSVToRGB(i / 11f, .40f, .53f));
            inventoryRoot = BuildInventory(false);
            bossInventoryRoot = BuildInventory(true);
        }

        private Material StorageMaterial(string name, Color color)
        {
            var result = MakeMaterial(name, color); storageMaterials.Add(result); return result;
        }

        private GameObject StorageBlock(Transform root, string name, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name; block.transform.SetParent(root, false);
            block.transform.localPosition = position; block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Destroy(block.GetComponent<Collider>());
            return block;
        }

        private GameObject BeveledStorage(Transform root, string name, Vector3 position, float width, float depth, float height, Material material)
        {
            const float corner = .025f;
            var ring = new[] { new Vector2(-width / 2 + corner, -depth / 2), new Vector2(width / 2 - corner, -depth / 2),
                new Vector2(width / 2, -depth / 2 + corner), new Vector2(width / 2, depth / 2 - corner),
                new Vector2(width / 2 - corner, depth / 2), new Vector2(-width / 2 + corner, depth / 2),
                new Vector2(-width / 2, depth / 2 - corner), new Vector2(-width / 2, -depth / 2 + corner) };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            { int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2); }
            for (int i = 0; i < ring.Length; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % ring.Length];
                Vector3 lowA = new(a.x, 0, a.y), lowB = new(b.x, 0, b.y);
                Vector3 highA = lowA + Vector3.up * height, highB = lowB + Vector3.up * height;
                Triangle(Vector3.up * height, highB, highA); Triangle(Vector3.zero, lowA, lowB);
                Triangle(lowA, highA, lowB); Triangle(lowB, highA, highB);
            }
            var mesh = new Mesh { name = name + "_LowPolyMesh", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); storageMeshes.Add(mesh);
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
            part.transform.SetParent(root, false); part.transform.localPosition = position;
            part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<Renderer>().sharedMaterial = material;
            part.GetComponent<BoxCollider>().center = Vector3.up * height / 2;
            part.GetComponent<BoxCollider>().size = new Vector3(width, height, depth);
            return part;
        }

        private void PizzaBadge(Transform root)
        {
            var mesh = new Mesh { name = "DeliveryCrate_PizzaSliceBadge", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(-.036f, .025f, 0), new Vector3(.036f, .025f, 0), new Vector3(0, -.030f, 0) };
            mesh.triangles = new[] { 0, 1, 2 }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); storageMeshes.Add(mesh);
            var badge = new GameObject("PizzaSliceEmblem", typeof(MeshFilter), typeof(MeshRenderer));
            badge.transform.SetParent(root, false); badge.transform.localPosition = new Vector3(0, .163f, .193f);
            badge.GetComponent<MeshFilter>().sharedMesh = mesh; badge.GetComponent<Renderer>().sharedMaterial = ivoryRim;
            StorageBlock(root, "PizzaCrust", new Vector3(0, .190f, .190f), new Vector3(.078f, .009f, .006f), paperLiner, false);
            StorageBlock(root, "PepperoniTop", new Vector3(-.011f, .175f, .189f), new Vector3(.010f, .010f, .004f), bossAccent, false);
            StorageBlock(root, "PepperoniBottom", new Vector3(.005f, .155f, .189f), new Vector3(.009f, .009f, .004f), bossAccent, false);
        }

        private GameObject BuildInventory(bool boss)
        {
            var root = new GameObject(boss ? "BossItemCubes_Runtime" : "PlayerItemCubes_Runtime") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(items.transform.root, false);
            root.transform.position = boss ? new Vector3(.62f, .913f, .64f) : new Vector3(.62f, .833f, .05f);
            Material shell = boss ? bossShell : playerShell, trim = boss ? steelRim : ivoryRim;
            if (boss) StorageBlock(root.transform, "CrateRaisedStand", new Vector3(0, -.04f, 0), new Vector3(.59f, .08f, .40f), bossAccent);
            BeveledStorage(root.transform, boss ? "DeliveryCrateShell" : "ServingTrayShell", Vector3.zero, .60f, .42f, .024f, shell);
            BeveledStorage(root.transform, "SixSlotRearShelf", new Vector3(0, .024f, .105f), .568f, .186f, .060f, shell);
            StorageBlock(root.transform, "FrontRolledRim", new Vector3(0, .040f, -.204f), new Vector3(.60f, .032f, .018f), trim);
            StorageBlock(root.transform, "BackRim", new Vector3(0, boss ? .120f : .071f, .204f), new Vector3(.60f, boss ? .192f : .094f, .018f), shell);
            foreach (int side in new[] { -1, 1 })
            {
                StorageBlock(root.transform, "FrontSideRim_" + side, new Vector3(side * .291f, .040f, -.10f), new Vector3(.018f, .032f, .20f), trim);
                StorageBlock(root.transform, "RearSideRim_" + side, new Vector3(side * .291f, boss ? .124f : .100f, .105f),
                    new Vector3(.018f, boss ? .080f : .032f, .19f), trim);
                StorageBlock(root.transform, "FrontSlotDivider_" + side, new Vector3(side * .09f, .040f, -.10f), new Vector3(.008f, .032f, .186f), shell);
                StorageBlock(root.transform, "RearSlotDivider_" + side, new Vector3(side * .09f, .100f, .105f), new Vector3(.008f, .032f, .182f), shell);
                if (boss)
                {
                    StorageBlock(root.transform, "SteelFrontCorner_" + side, new Vector3(side * .278f, .064f, -.216f), new Vector3(.022f, .080f, .008f), steelRim);
                    StorageBlock(root.transform, "SteelRearCorner_" + side, new Vector3(side * .278f, .122f, .216f), new Vector3(.022f, .196f, .008f), steelRim);
                    StorageBlock(root.transform, "CrateCarryHandle_" + side, new Vector3(side * .326f, .132f, .105f), new Vector3(.016f, .016f, .150f), steelRim);
                    foreach (int end in new[] { -1, 1 })
                        StorageBlock(root.transform, "CrateHandleMount_" + side + "_" + end, new Vector3(side * .307f, .132f, .105f + end * .066f), new Vector3(.052f, .016f, .016f), steelRim);
                }
                else StorageBlock(root.transform, "TrayHandleUpright_" + side, new Vector3(side * .085f, .065f, -.242f), new Vector3(.014f, .055f, .014f), ivoryRim);
            }
            if (boss)
            {
                StorageBlock(root.transform, "CrateTopBand", new Vector3(0, .218f, .204f), new Vector3(.60f, .012f, .023f), steelRim);
                PizzaBadge(root.transform);
            }
            else StorageBlock(root.transform, "ServingTrayHandle", new Vector3(0, .092f, -.242f), new Vector3(.184f, .014f, .016f), ivoryRim);
            var owned = boss ? bossCubes : cubes;
            for (int i = 0; i < owned.Length; i++)
            {
                float x = (i % 3 - 1) * .18f, z = i / 3 == 0 ? -.10f : .10f, floor = i / 3 == 0 ? .024f : .084f;
                StorageBlock(root.transform, "CompartmentLiner_" + i, new Vector3(x, floor + .0005f, z), new Vector3(.153f, .001f, .153f), paperLiner, false);
                var check = StorageBlock(root.transform, "LinerCheck_" + i, new Vector3(x, floor + .0011f, z - .056f), new Vector3(.142f, .0004f, .014f), boss ? bossAccent : playerAccent, false);
                check.transform.localRotation = Quaternion.Euler(0, 12, 0);
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "ItemSlot_" + (i + 1); cube.transform.SetParent(root.transform, false);
                cube.transform.localPosition = new Vector3(x, floor + .0535f, z);
                cube.transform.localScale = Vector3.one * .105f;
                cube.AddComponent<ColaItemCube>().Bind(i, boss);
                cube.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
                var number = new GameObject("ItemNumber"); number.transform.SetParent(cube.transform, false);
                number.transform.localPosition = new Vector3(0, .12f, -.515f);
                var text = number.AddComponent<TextMesh>(); text.font = cubeFont; text.fontSize = 40; text.characterSize = .12f;
                text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = Ink; ColaWorldText.Bind(text);
                owned[i] = cube; cube.SetActive(false);
            }
            return root;
        }
        private void RefreshInventory()
        {
            if (!items || !inventoryRoot) return;
            for (int actor = 0; actor < 2; actor++)
            {
                bool boss = actor == 1;
                var owned = boss ? bossCubes : cubes;
                for (int i = 0; i < owned.Length; i++)
                {
                    var id = items.ItemAt(i, boss);
                    owned[i].SetActive(id.HasValue);
                    if (!id.HasValue) continue;
                    owned[i].name = (boss ? "Boss" : "Player") + "ItemSlot_" + (i + 1) + "_" + id.Value;
                    owned[i].GetComponent<MeshRenderer>().sharedMaterial = materials[(int)id.Value];
                    owned[i].GetComponentInChildren<TextMesh>(true).text = ((int)id.Value + 1).ToString("00");
                }
            }
        }

        private void Update()
        {
            if (!items) return;
            if (!orderSheet) orderSheet = GetComponent<ColaOrderSheet>();
            hoveredSlot = -1;
            if (Mouse.current == null || OrderOpen) return;
            mousePosition = Mouse.current.position.ReadValue();
            if (BlocksWorldInput(mousePosition)) return;
            var camera = items.match.interaction.playerCamera;
            if (Physics.Raycast(camera.ScreenPointToRay(mousePosition), out var hit, 8, ~0, QueryTriggerInteraction.Ignore))
            {
                var cube = hit.collider.GetComponent<ColaItemCube>();
                if (cube) { hoveredSlot = cube.Slot; hoveredBoss = cube.IsBoss; }
            }
        }

        private void EnsureStyles()
        {
            if (label != null) return;
            menuFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Arial" }, 18);
            label = new GUIStyle(GUI.skin.label) { font = menuFont, fontSize = 15, alignment = TextAnchor.MiddleLeft, richText = false };
            label.normal.textColor = Ink;
            detail = new GUIStyle(label) { fontSize = 13, wordWrap = true };
        }

        private void OnGUI()
        {
            if (!Application.IsPlaying(gameObject) || !items || Screen.width < 320 || Screen.height < 240) return;
            EnsureStyles();
            float scale = Scale;
            label.fontSize = Mathf.RoundToInt(15 * scale);
            detail.fontSize = Mathf.RoundToInt(13 * scale);
            if (!OrderOpen && hoveredSlot >= 0 && items.ItemAt(hoveredSlot, hoveredBoss).HasValue)
            {
                var definition = ColaItemCatalog.Get(items.ItemAt(hoveredSlot, hoveredBoss).Value);
                float width = Mathf.Min(380 * scale, Screen.width - 20);
                var tooltip = new Rect(Mathf.Clamp(mousePosition.x + 15, 10, Screen.width - width - 10),
                    Mathf.Clamp(Screen.height - mousePosition.y - 80 * scale, 50, Screen.height - 145 * scale), width, 75 * scale);
                DrawPanel(tooltip);
                GUI.Label(new Rect(tooltip.x + 8, tooltip.y + 4, width - 16, 25 * scale), definition.Name + (hoveredBoss ? "  /  보스 보유" : "  /  클릭 사용"), label);
                GUI.Label(new Rect(tooltip.x + 8, tooltip.y + 28 * scale, width - 16, 43 * scale), definition.Effect, detail);
            }
            if (!OrderOpen && Time.unscaledTime < items.BossNoticeUntil)
            {
                var notice = new Rect(10 * scale, Screen.height - 153 * scale, Screen.width - 20 * scale, 45 * scale);
                DrawPanel(notice);
                GUI.Label(new Rect(notice.x + 8, notice.y + 3, notice.width - 16, notice.height - 6), items.BossNotice, detail);
            }
            GUI.Label(new Rect(15 * scale, Screen.height - 104 * scale, Screen.width - 30 * scale, 24 * scale),
                "내 효과: " + items.EffectsText(false), detail);
            GUI.Label(new Rect(15 * scale, Screen.height - 80 * scale, Screen.width - 30 * scale, 24 * scale),
                "보스 효과: " + items.EffectsText(true), detail);
        }

        private static void DrawPanel(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = new Color(.25f, .32f, .24f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Paper;
            GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (items) items.InventoryChanged -= RefreshInventory;
            if (inventoryRoot) Destroy(inventoryRoot);
            if (bossInventoryRoot) Destroy(bossInventoryRoot);
            foreach (var material in materials) if (material) Destroy(material);
            foreach (var material in storageMaterials) if (material) Destroy(material);
            foreach (var mesh in storageMeshes) if (mesh) Destroy(mesh);
            if (menuFont) Destroy(menuFont);
        }
    }
}
