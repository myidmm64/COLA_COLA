using ColaCola.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColaCola.Presentation
{
    /// <summary>In-turn order slip and six-slot tabletop inventories containing original physical item models.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaItemPresentation : MonoBehaviour
    {
        public bool OrderOpen => orderSheet && orderSheet.BlocksGameplay;
        private ColaOrderSheet orderSheet;
        private ColaItemController items;
        private GameObject inventoryRoot, bossInventoryRoot;
        private readonly GameObject[] cubes = new GameObject[ColaItemController.Capacity];
        private readonly GameObject[] bossCubes = new GameObject[ColaItemController.Capacity];
        private readonly GameObject[] playerModels = new GameObject[ColaItemController.Capacity];
        private readonly GameObject[] bossModels = new GameObject[ColaItemController.Capacity];
        private readonly ColaItemId?[] playerModelIds = new ColaItemId?[ColaItemController.Capacity];
        private readonly ColaItemId?[] bossModelIds = new ColaItemId?[ColaItemController.Capacity];
        private ColaItemModelLibrary modelLibrary;
        private Material playerShell, bossShell, ivoryRim, steelRim, paperLiner, playerAccent, bossAccent;
        private readonly List<Material> storageMaterials = new();
        private readonly List<Mesh> storageMeshes = new();
        private Font menuFont;
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
            modelLibrary = new ColaItemModelLibrary();
            playerShell = StorageMaterial("ServingTray_TealEnamel", new Color(.11f, .34f, .30f));
            bossShell = StorageMaterial("DeliveryCrate_RedWood", new Color(.38f, .13f, .085f));
            ivoryRim = StorageMaterial("ServingTray_IvoryRim", new Color(.70f, .65f, .49f));
            steelRim = StorageMaterial("DeliveryCrate_SteelBands", new Color(.34f, .37f, .31f));
            paperLiner = StorageMaterial("Pizzeria_PaperLiner", new Color(.75f, .69f, .52f));
            playerAccent = StorageMaterial("ServingTray_DarkChecks", new Color(.07f, .20f, .17f));
            bossAccent = StorageMaterial("DeliveryCrate_DarkRed", new Color(.22f, .055f, .035f));
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
            // Put the badge on the outside of the short front lip, clear of the item numbers.
            badge.transform.SetParent(root, false); badge.transform.localPosition = new Vector3(0, .029f, -.198f);
            badge.transform.localScale = Vector3.one * .36f;
            badge.GetComponent<MeshFilter>().sharedMesh = mesh; badge.GetComponent<Renderer>().sharedMaterial = ivoryRim;
            StorageBlock(badge.transform, "PizzaCrust", new Vector3(0, .027f, -.003f), new Vector3(.078f, .009f, .006f), paperLiner, false);
            StorageBlock(badge.transform, "PepperoniTop", new Vector3(-.011f, .012f, -.004f), new Vector3(.010f, .010f, .004f), bossAccent, false);
            StorageBlock(badge.transform, "PepperoniBottom", new Vector3(.005f, -.008f, -.004f), new Vector3(.009f, .009f, .004f), bossAccent, false);
        }

        private GameObject BuildInventory(bool boss)
        {
            var root = new GameObject(boss ? "BossItemCubes_Runtime" : "PlayerItemCubes_Runtime") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(items.transform.root, false);
            // Both trays share one grid and yaw. The rear tray sits higher and farther right in perspective.
            root.transform.SetPositionAndRotation(
                boss ? new Vector3(.80f, .927f, .62f) : new Vector3(.62f, .833f, .08f),
                Quaternion.Euler(0, 8, 0));
            root.transform.localScale = Vector3.one * .84f;
            Material shell = boss ? bossShell : playerShell, trim = boss ? steelRim : ivoryRim;
            if (boss) BeveledStorage(root.transform, "CrateRaisedStand", new Vector3(0, -.112f, 0), .51f, .35f, .112f, bossAccent);
            BeveledStorage(root.transform, boss ? "DeliveryCrateShell" : "ServingTrayShell", Vector3.zero, .55f, .39f, .022f, shell);
            BeveledStorage(root.transform, "SixSlotRearShelf", new Vector3(0, .022f, .095f), .526f, .178f, .078f, shell);
            StorageBlock(root.transform, "FrontRolledRim", new Vector3(0, .029f, -.188f), new Vector3(.55f, .014f, .014f), trim);
            StorageBlock(root.transform, "BackRim", new Vector3(0, .111f, .188f), new Vector3(.55f, .022f, .014f), shell);
            StorageBlock(root.transform, "RearShelfEdge", new Vector3(0, .093f, .003f), new Vector3(.526f, .014f, .008f), trim);
            foreach (int side in new[] { -1, 1 })
            {
                StorageBlock(root.transform, "FrontSideRim_" + side, new Vector3(side * .268f, .029f, -.095f), new Vector3(.014f, .014f, .178f), trim);
                StorageBlock(root.transform, "RearSideRim_" + side, new Vector3(side * .268f, .109f, .095f),
                    new Vector3(.014f, .018f, .178f), trim);
                StorageBlock(root.transform, "FrontSlotDivider_" + side, new Vector3(side * .085f, .031f, -.095f), new Vector3(.006f, .018f, .178f), shell);
                StorageBlock(root.transform, "RearSlotDivider_" + side, new Vector3(side * .085f, .109f, .095f), new Vector3(.006f, .018f, .178f), shell);
                StorageBlock(root.transform, "RecessedSideGrip_" + side, new Vector3(side * .276f, .109f, .095f),
                    new Vector3(.003f, .008f, .062f), boss ? bossAccent : playerAccent, false);
                if (boss)
                {
                    StorageBlock(root.transform, "SteelFrontCorner_" + side, new Vector3(side * .254f, .029f, -.198f), new Vector3(.016f, .020f, .006f), steelRim);
                    StorageBlock(root.transform, "SteelRearCorner_" + side, new Vector3(side * .254f, .109f, .198f), new Vector3(.016f, .024f, .006f), steelRim);
                }
            }
            if (boss)
            {
                StorageBlock(root.transform, "CrateTopBand", new Vector3(0, .125f, .188f), new Vector3(.55f, .006f, .016f), steelRim);
                PizzaBadge(root.transform);
            }
            var owned = boss ? bossCubes : cubes;
            for (int i = 0; i < owned.Length; i++)
            {
                float x = (i % 3 - 1) * .17f, z = i / 3 == 0 ? -.095f : .095f, floor = i / 3 == 0 ? .022f : .100f;
                StorageBlock(root.transform, "CompartmentLiner_" + i, new Vector3(x, floor + .0005f, z), new Vector3(.154f, .001f, .154f), paperLiner, false);
                StorageBlock(root.transform, "LinerCheck_" + i, new Vector3(x, floor + .0011f, z - .060f),
                    new Vector3(.134f, .0004f, .008f), boss ? bossAccent : playerAccent, false);
                var cube = new GameObject("ItemSlot_" + (i + 1), typeof(BoxCollider));
                cube.transform.SetParent(root.transform, false);
                cube.transform.localPosition = new Vector3(x, floor + .001f, z);
                cube.AddComponent<ColaItemCube>().Bind(i, boss);
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
                var models = boss ? bossModels : playerModels;
                var modelIds = boss ? bossModelIds : playerModelIds;
                for (int i = 0; i < owned.Length; i++)
                {
                    var id = items.ItemAt(i, boss);
                    owned[i].SetActive(id.HasValue);
                    if (!id.HasValue) continue;
                    owned[i].name = (boss ? "Boss" : "Player") + "ItemSlot_" + (i + 1) + "_" + id.Value;
                    if (!models[i] || modelIds[i] != id)
                    {
                        if (models[i])
                        {
                            models[i].SetActive(false);
                            models[i].transform.SetParent(null, false);
                            Destroy(models[i]);
                        }
                        models[i] = modelLibrary.Create(id.Value, owned[i].transform);
                        modelIds[i] = id;
                        ColaItemModelLibrary.FitHitTarget(owned[i].transform, owned[i].GetComponent<BoxCollider>());
                    }
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
                GUI.Label(new Rect(tooltip.x + 8, tooltip.y + 4, width - 16, 25 * scale), definition.Name, label);
                GUI.Label(new Rect(tooltip.x + 8, tooltip.y + 28 * scale, width - 16, 43 * scale), definition.Effect, detail);
            }
            GUI.Label(new Rect(15 * scale, Screen.height - 59 * scale, Screen.width - 30 * scale, 24 * scale),
                "내 효과: " + items.EffectsText(false), detail);
            GUI.Label(new Rect(15 * scale, Screen.height - 33 * scale, Screen.width - 30 * scale, 24 * scale),
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
            modelLibrary?.Dispose();
            foreach (var material in storageMaterials) if (material) Destroy(material);
            foreach (var mesh in storageMeshes) if (mesh) Destroy(mesh);
            if (menuFont) Destroy(menuFont);
        }
    }
}
