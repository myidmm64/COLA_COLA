using ColaCola.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColaCola.Presentation
{
    /// <summary>Debug order slip and eight-slot tabletop inventories containing original physical item models.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaItemPresentation : MonoBehaviour
    {
        public bool OrderOpen => orderSheet && orderSheet.BlocksGameplay;
        private ColaOrderSheet orderSheet;
        private ColaWaiterBell hoveredBell;
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
        private GUIStyle label, detail, bellHint, bellHintShadow;
        private const int SlotColumns = 4;
        private const float SlotPitch = .17f;
        private const float TrayWidth = SlotColumns * SlotPitch + .04f;
        private int hoveredSlot = -1;
        private bool hoveredBoss;
        private Vector2 mousePosition;
        private readonly List<Delivery> deliveries = new();
        private const float DeliverySeconds = .46f;
        private sealed class Delivery
        {
            public int slot;
            public bool boss;
            public Transform model;
            public Collider collider;
            public Vector3 position, scale;
            public Quaternion rotation;
            public float started;
            public bool sliding, landed;
        }
        private static readonly Color Ink = new(.81f, .82f, .68f);
        private static readonly Color Paper = new(.065f, .09f, .075f, .98f);
        private float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 960f, Screen.height / 640f), .6f, 1.2f);

        public void Bind(ColaItemController controller)
        {
            if (items)
            {
                items.InventoryChanged -= RefreshInventory;
                items.WaiterOrdered -= DeliverOrder;
                items.PresentationReset -= CancelDeliveries;
            }
            CancelDeliveries();
            items = controller;
            items.InventoryChanged += RefreshInventory;
            items.WaiterOrdered += DeliverOrder;
            items.PresentationReset += CancelDeliveries;
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

        public bool IsReceivingItems(bool boss)
        {
            if (!isActiveAndEnabled) return false;
            foreach (var delivery in deliveries) if (delivery.boss == boss) return true;
            return false;
        }

        public bool IsSlotArriving(int slot, bool boss)
        {
            if (!isActiveAndEnabled) return false;
            foreach (var delivery in deliveries) if (delivery.slot == slot && delivery.boss == boss) return true;
            return false;
        }

        private void DeliverOrder(int[] slots, bool boss)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || slots == null) return;
            var models = boss ? bossModels : playerModels;
            for (int i = 0; i < slots.Length; i++)
            {
                int slot = slots[i];
                if (slot < 0 || slot >= models.Length || !models[slot]) continue;
                CancelDelivery(slot, boss);
                var model = models[slot].transform;
                var hit = CubeCollider(slot, boss);
                var delivery = new Delivery { slot = slot, boss = boss, model = model, collider = hit,
                    position = model.localPosition, rotation = model.localRotation, scale = model.localScale,
                    started = Time.unscaledTime + .045f + i * .10f };
                deliveries.Add(delivery);
                if (hit) hit.enabled = false;
                model.gameObject.SetActive(false);
            }
        }

        private static void RestoreDelivery(Delivery delivery)
        {
            if (delivery.model)
            {
                delivery.model.localPosition = delivery.position;
                delivery.model.localRotation = delivery.rotation;
                delivery.model.localScale = delivery.scale;
                delivery.model.gameObject.SetActive(true);
            }
            if (delivery.collider) delivery.collider.enabled = true;
        }

        private void CancelDelivery(int slot, bool boss)
        {
            for (int i = deliveries.Count - 1; i >= 0; i--)
            {
                if (deliveries[i].slot != slot || deliveries[i].boss != boss) continue;
                RestoreDelivery(deliveries[i]); deliveries.RemoveAt(i);
            }
        }

        private void CancelDeliveries()
        {
            foreach (var delivery in deliveries) RestoreDelivery(delivery);
            deliveries.Clear();
        }

        private void AnimateDeliveries()
        {
            if (deliveries.Count == 0) return;
            if (!Application.IsPlaying(gameObject) || !items || !items.isActiveAndEnabled || !items.match ||
                !items.match.interaction || !items.match.interaction.isActiveAndEnabled ||
                items.match.State != ColaDuelController.DuelState.Playing)
            { CancelDeliveries(); return; }
            for (int i = deliveries.Count - 1; i >= 0; i--)
            {
                var delivery = deliveries[i];
                if (!delivery.model)
                { RestoreDelivery(delivery); deliveries.RemoveAt(i); continue; }
                float elapsed = Time.unscaledTime - delivery.started;
                if (elapsed < 0) continue;
                if (!delivery.sliding)
                {
                    delivery.sliding = true;
                    ColaTabletopAudio.Cue(items.match.interaction, "DeliverySlide", delivery.model.position, .48f);
                }
                if (!delivery.landed && elapsed >= DeliverySeconds * .72f)
                {
                    delivery.landed = true;
                    var id = items.ItemAt(delivery.slot, delivery.boss);
                    bool glass = id == ColaItemId.IceCup || id == ColaItemId.HotSauce || id == ColaItemId.CarbonationSyrup;
                    ColaTabletopAudio.Cue(items.match.interaction, glass ? "DeliveryLandGlass" : "DeliveryLand", delivery.model.position, .65f);
                }
                if (elapsed >= DeliverySeconds)
                { RestoreDelivery(delivery); deliveries.RemoveAt(i); continue; }
                delivery.model.gameObject.SetActive(true);
                float t = Mathf.Clamp01(elapsed / DeliverySeconds);
                float travel = Mathf.Clamp01(t / .72f);
                float ease = travel * travel * (3 - 2 * travel);
                float settle = Mathf.Clamp01((t - .72f) / .28f);
                float bounce = Mathf.Sin(settle * Mathf.PI * 2) * (1 - settle);
                Vector3 entry = delivery.position + new Vector3(.12f, .16f, .045f);
                delivery.model.localPosition = Vector3.Lerp(entry, delivery.position, ease) +
                    Vector3.up * (Mathf.Sin(ease * Mathf.PI) * .018f + Mathf.Abs(bounce) * .009f);
                delivery.model.localRotation = Quaternion.Slerp(delivery.rotation * Quaternion.Euler(12, -9, 7), delivery.rotation, ease);
                float size = Mathf.Lerp(.88f, 1, ease);
                delivery.model.localScale = Vector3.Scale(delivery.scale,
                    new Vector3(size + Mathf.Abs(bounce) * .025f, size - Mathf.Abs(bounce) * .06f, size));
            }
        }

        public GameObject TakeModelForUse(int slot, bool boss)
        {
            if (slot < 0 || slot >= ColaItemController.Capacity) return null;
            var models = boss ? bossModels : playerModels;
            var ids = boss ? bossModelIds : playerModelIds;
            var model = models[slot];
            if (!model) return null;
            model.transform.SetParent(null, true);
            model.SetActive(true);
            models[slot] = null;
            ids[slot] = null;
            return model;
        }

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
                boss ? new Vector3(.835f, .927f, .62f) : new Vector3(.655f, .833f, .08f),
                Quaternion.Euler(0, 8, 0));
            root.transform.localScale = Vector3.one * .84f;
            Material shell = boss ? bossShell : playerShell, trim = boss ? steelRim : ivoryRim;
            if (boss) BeveledStorage(root.transform, "CrateRaisedStand", new Vector3(0, -.112f, 0), TrayWidth - .04f, .35f, .112f, bossAccent);
            BeveledStorage(root.transform, boss ? "DeliveryCrateShell" : "ServingTrayShell", Vector3.zero, TrayWidth, .39f, .022f, shell);
            BeveledStorage(root.transform, "EightSlotRearShelf", new Vector3(0, .022f, .095f), TrayWidth - .024f, .178f, .078f, shell);
            StorageBlock(root.transform, "FrontRolledRim", new Vector3(0, .029f, -.188f), new Vector3(TrayWidth, .014f, .014f), trim);
            StorageBlock(root.transform, "BackRim", new Vector3(0, .111f, .188f), new Vector3(TrayWidth, .022f, .014f), shell);
            StorageBlock(root.transform, "RearShelfEdge", new Vector3(0, .093f, .003f), new Vector3(TrayWidth - .024f, .014f, .008f), trim);
            foreach (int side in new[] { -1, 1 })
            {
                StorageBlock(root.transform, "FrontSideRim_" + side, new Vector3(side * (TrayWidth / 2 - .007f), .029f, -.095f), new Vector3(.014f, .014f, .178f), trim);
                StorageBlock(root.transform, "RearSideRim_" + side, new Vector3(side * (TrayWidth / 2 - .007f), .109f, .095f),
                    new Vector3(.014f, .018f, .178f), trim);
                StorageBlock(root.transform, "RecessedSideGrip_" + side, new Vector3(side * (TrayWidth / 2 + .001f), .109f, .095f),
                    new Vector3(.003f, .008f, .062f), boss ? bossAccent : playerAccent, false);
                if (boss)
                {
                    StorageBlock(root.transform, "SteelFrontCorner_" + side, new Vector3(side * (TrayWidth / 2 - .021f), .029f, -.198f), new Vector3(.016f, .020f, .006f), steelRim);
                    StorageBlock(root.transform, "SteelRearCorner_" + side, new Vector3(side * (TrayWidth / 2 - .021f), .109f, .198f), new Vector3(.016f, .024f, .006f), steelRim);
                }
            }
            for (int column = 1; column < SlotColumns; column++)
            {
                float x = (column - SlotColumns / 2f) * SlotPitch;
                StorageBlock(root.transform, "FrontSlotDivider_" + column, new Vector3(x, .031f, -.095f), new Vector3(.006f, .018f, .178f), shell);
                StorageBlock(root.transform, "RearSlotDivider_" + column, new Vector3(x, .109f, .095f), new Vector3(.006f, .018f, .178f), shell);
            }
            if (boss)
            {
                StorageBlock(root.transform, "CrateTopBand", new Vector3(0, .125f, .188f), new Vector3(TrayWidth, .006f, .016f), steelRim);
                PizzaBadge(root.transform);
            }
            var owned = boss ? bossCubes : cubes;
            for (int i = 0; i < owned.Length; i++)
            {
                float x = (i % SlotColumns - (SlotColumns - 1) / 2f) * SlotPitch;
                float z = i / SlotColumns == 0 ? -.095f : .095f, floor = i / SlotColumns == 0 ? .022f : .100f;
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
                    if (!id.HasValue || modelIds[i] != id) CancelDelivery(i, boss);
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
            AnimateDeliveries();
            if (!items) return;
            if (!orderSheet) orderSheet = GetComponent<ColaOrderSheet>();
            hoveredSlot = -1;
            hoveredBell = null;
            if (Mouse.current == null || OrderOpen) return;
            mousePosition = Mouse.current.position.ReadValue();
            if (BlocksWorldInput(mousePosition)) return;
            var camera = items.match.interaction.playerCamera;
            if (Physics.Raycast(camera.ScreenPointToRay(mousePosition), out var hit, 8, ~0, QueryTriggerInteraction.Ignore))
            {
                var cube = hit.collider.GetComponent<ColaItemCube>();
                if (cube) { hoveredSlot = cube.Slot; hoveredBoss = cube.IsBoss; }
                var bellTarget = hit.collider.GetComponent<ColaWaiterBellTarget>();
                if (bellTarget && bellTarget.Bell && bellTarget.Bell.CanHover) hoveredBell = bellTarget.Bell;
            }
        }

        private void EnsureStyles()
        {
            if (label != null) return;
            menuFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Arial" }, 18);
            label = new GUIStyle(GUI.skin.label) { font = menuFont, fontSize = 15, alignment = TextAnchor.MiddleLeft, richText = false };
            SetFixedLabelColor(label, Ink);
            detail = new GUIStyle(label) { fontSize = 13, wordWrap = true };
            SetFixedLabelColor(detail, Ink);
            bellHint = new GUIStyle(detail);
            SetFixedLabelColor(bellHint, Ink);
            bellHintShadow = new GUIStyle(detail);
            SetFixedLabelColor(bellHintShadow, new Color(.01f, .015f, .01f, .95f));
        }

        private static void SetFixedLabelColor(GUIStyle style, Color color)
        {
            // Do not inherit the skin's white hover/active colours when a tooltip appears under the pointer.
            foreach (var state in new[] { style.normal, style.hover, style.active, style.focused,
                style.onNormal, style.onHover, style.onActive, style.onFocused })
            {
                state.textColor = color;
                state.background = null;
            }
        }

        private void OnGUI()
        {
            if (!Application.IsPlaying(gameObject) || !items || Screen.width < 320 || Screen.height < 240) return;
            EnsureStyles();
            Color previousTint = GUI.color, previousContent = GUI.contentColor;
            GUI.color = GUI.contentColor = Color.white;
            float scale = Scale;
            label.fontSize = Mathf.RoundToInt(15 * scale);
            detail.fontSize = Mathf.RoundToInt(13 * scale);
            bellHint.fontSize = bellHintShadow.fontSize = Mathf.RoundToInt(14 * scale);
            if (!OrderOpen && hoveredBell && hoveredBell.CanHover)
            {
                var rect = new Rect(15 * scale, Screen.height - 123 * scale, Screen.width - 30 * scale, 54 * scale);
                GUI.Label(new Rect(rect.x + scale, rect.y + scale, rect.width, rect.height), hoveredBell.HoverDescription, bellHintShadow);
                GUI.Label(rect, hoveredBell.HoverDescription, bellHint);
            }
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
            GUI.color = previousTint;
            GUI.contentColor = previousContent;
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

        private void OnDisable() => CancelDeliveries();

        private void OnDestroy()
        {
            CancelDeliveries();
            var itemUse = GetComponent<ColaItemUsePresentation>();
            if (itemUse) itemUse.Cancel();
            if (items)
            {
                items.InventoryChanged -= RefreshInventory;
                items.WaiterOrdered -= DeliverOrder;
                items.PresentationReset -= CancelDeliveries;
            }
            if (inventoryRoot) Destroy(inventoryRoot);
            if (bossInventoryRoot) Destroy(bossInventoryRoot);
            modelLibrary?.Dispose();
            foreach (var material in storageMaterials) if (material) Destroy(material);
            foreach (var mesh in storageMeshes) if (mesh) Destroy(mesh);
            if (menuFont) Destroy(menuFont);
        }
    }
}
