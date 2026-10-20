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
        private bool CanShowHints => items && items.CanInspectItems && !OrderOpen;
        public bool HasHoverDescription => CanShowHints && !string.IsNullOrEmpty(hoverDescription);
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
        private readonly Vector3[] playerRestPositions = new Vector3[ColaItemController.Capacity];
        private readonly Vector3[] bossRestPositions = new Vector3[ColaItemController.Capacity];
        private readonly float[] playerHover = new float[ColaItemController.Capacity];
        private readonly float[] bossHover = new float[ColaItemController.Capacity];
        private const float HoverLift = .035f, HoverSeconds = .16f;
        private ColaItemModelLibrary modelLibrary;
        private Material playerShell, bossShell, ivoryRim, steelRim, paperLiner, playerAccent, bossAccent;
        private readonly List<Material> storageMaterials = new();
        private readonly List<Mesh> storageMeshes = new();
        private Font menuFont;
        private GUIStyle label, detail, hoverText, hoverNameShadow, hoverTextShadow;
        private const int SlotColumns = 4;
        private const float SlotPitch = .17f;
        private const float TrayWidth = SlotColumns * SlotPitch + .04f;
        private int hoveredSlot = -1;
        private bool hoveredBoss;
        private Vector2 mousePosition;
        private string hoverTitle, hoverDescription;
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
        private float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 960f, Screen.height / 640f), .6f, 1.2f);

        public void Bind(ColaItemController controller)
        {
            if (items)
            {
                items.InventoryChanged -= RefreshInventory;
                items.WaiterOrdered -= DeliverOrder;
                items.PresentationReset -= ResetPresentation;
            }
            ResetPresentation();
            items = controller;
            items.InventoryChanged += RefreshInventory;
            items.WaiterOrdered += DeliverOrder;
            items.PresentationReset += ResetPresentation;
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
                ResetHover(slot, boss);
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

        private void ResetHover(int slot, bool boss)
        {
            var models = boss ? bossModels : playerModels;
            var rests = boss ? bossRestPositions : playerRestPositions;
            var weights = boss ? bossHover : playerHover;
            weights[slot] = 0;
            if (models[slot]) models[slot].transform.localPosition = rests[slot];
        }

        private void ResetPresentation()
        {
            CancelDeliveries();
            hoveredSlot = -1; hoveredBell = null;
            hoverTitle = hoverDescription = null;
            for (int i = 0; i < ColaItemController.Capacity; i++)
            {
                ResetHover(i, false); ResetHover(i, true);
            }
        }

        private void AnimateHover()
        {
            for (int actor = 0; actor < 2; actor++)
            {
                bool boss = actor == 1;
                var models = boss ? bossModels : playerModels;
                var rests = boss ? bossRestPositions : playerRestPositions;
                var weights = boss ? bossHover : playerHover;
                for (int i = 0; i < models.Length; i++)
                {
                    if (!models[i] || !models[i].activeInHierarchy || IsSlotArriving(i, boss))
                    { weights[i] = 0; continue; }
                    float target = CanShowHints && hoveredSlot == i && hoveredBoss == boss ? 1 : 0;
                    weights[i] = Mathf.MoveTowards(weights[i], target, Time.unscaledDeltaTime / HoverSeconds);
                    float eased = weights[i] * weights[i] * (3 - 2 * weights[i]);
                    models[i].transform.localPosition = rests[i] + Vector3.up * (HoverLift * eased);
                }
            }
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
                    ColaTabletopAudio.Cue(items.match.interaction, "DeliverySlide", delivery.model.position, .48f, delivery.boss);
                }
                if (!delivery.landed && elapsed >= DeliverySeconds * .72f)
                {
                    delivery.landed = true;
                    var id = items.ItemAt(delivery.slot, delivery.boss);
                    bool glass = id == ColaItemId.IceCup || id == ColaItemId.HotSauce || id == ColaItemId.CarbonationSyrup || id == ColaItemId.SodaWater;
                    ColaTabletopAudio.Cue(items.match.interaction, glass ? "DeliveryLandGlass" : "DeliveryLand", delivery.model.position, .65f, delivery.boss);
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
            (boss ? bossHover : playerHover)[slot] = 0;
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
                    if (!id.HasValue || modelIds[i] != id)
                    { CancelDelivery(i, boss); ResetHover(i, boss); }
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
                        (boss ? bossRestPositions : playerRestPositions)[i] = models[i].transform.localPosition;
                        var hitTarget = owned[i].GetComponent<BoxCollider>();
                        ColaItemModelLibrary.FitHitTarget(owned[i].transform, hitTarget);
                        // One fixed hit region covers both rest and raised poses, avoiding hover flicker.
                        hitTarget.size += Vector3.up * HoverLift;
                        hitTarget.center += Vector3.up * (HoverLift * .5f);
                    }
                }
            }
        }

        private void Update()
        {
            AnimateDeliveries();
            UpdateHoverTarget();
            AnimateHover();
        }

        private void UpdateHoverTarget()
        {
            if (Mouse.current != null) InspectPointer(Mouse.current.position.ReadValue());
            else { hoveredSlot = -1; hoveredBell = null; hoverTitle = hoverDescription = null; }
        }

        // Shared mouse/Play-review path; changes inspection only, never OS pointer state.
        public void InspectPointer(Vector2 screenPoint)
        {
            hoveredSlot = -1; hoveredBell = null;
            hoverTitle = hoverDescription = null;
            if (!items || !items.match || !items.match.interaction) return;
            if (!orderSheet) orderSheet = GetComponent<ColaOrderSheet>();
            var interaction = items.match.interaction;
            if (!CanShowHints || !interaction.playerCamera ||
                items.match.State != ColaDuelController.DuelState.Playing || !interaction.InputEnabled) return;
            mousePosition = screenPoint;
            if (BlocksWorldInput(mousePosition)) return;
            var camera = interaction.playerCamera;
            Ray ray = camera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out var hit, 8, ~0, QueryTriggerInteraction.Ignore))
            {
                var cube = hit.collider.GetComponentInParent<ColaItemCube>();
                if (cube && items.ItemAt(cube.Slot, cube.IsBoss).HasValue && !IsSlotArriving(cube.Slot, cube.IsBoss))
                {
                    if (!interaction.IsBusy) { hoveredSlot = cube.Slot; hoveredBoss = cube.IsBoss; }
                    var definition = ColaItemCatalog.Get(items.ItemAt(cube.Slot, cube.IsBoss).Value);
                    hoverTitle = definition.Name; hoverDescription = definition.Effect;
                    return;
                }
                var bellTarget = hit.collider.GetComponent<ColaWaiterBellTarget>();
                if (bellTarget && bellTarget.Bell && bellTarget.Bell.CanHover)
                { hoveredBell = bellTarget.Bell; hoverDescription = hoveredBell.HoverDescription; return; }
                // Item inspection and bell service remain available during the boss turn.
                // Shake instructions are only relevant when the player can act.
                if (!items.CanAct) return;
                if (hit.collider == interaction.bottleCollider || OnTarget(hit.transform, interaction.bottle))
                {
                    hoverTitle = "콜라";
                    int strength = interaction.Model != null ? interaction.Model.SelectedStrength : 0;
                    if (strength > 0)
                    {
                        var modifiers = items.PreviewAction(false);
                        var prediction = interaction.Model.Predict(strength, modifiers.PressureBonus,
                            pressureMultiplier: modifiers.PressureMultiplier);
                        hoverDescription = $"압력 +{prediction.PressureIncrease} · 안전 {ColaItemBalance.ShakeReward(strength, false, modifiers.RewardBonus)}토큰 · 폭발 {ColaItemBalance.ShakeReward(strength, true)}토큰 / 피해 {modifiers.Damage}";
                    }
                    else hoverDescription = "강도를 선택한 뒤 흔드세요.";
                    return;
                }
                var gauge = interaction.gauge;
                if (gauge && OnTarget(hit.transform, gauge.transform) && IsOverGaugeDial(ray, gauge.transform))
                {
                    hoverDescription = $"현재 압력 {gauge.CurrentPressure} / 한계 압력 {gauge.CurrentLimit}";
                }
            }
        }

        private static bool OnTarget(Transform hit, Transform target) => target && (hit == target || hit.IsChildOf(target));

        private static bool IsOverGaugeDial(Ray ray, Transform gauge)
        {
            // Match the round dial and bezel, excluding the lower readout and strength keys.
            var face = new Plane(gauge.TransformDirection(Vector3.back), gauge.TransformPoint(new Vector3(0, .026f, -.066f)));
            if (!face.Raycast(ray, out float distance)) return false;
            Vector3 point = gauge.InverseTransformPoint(ray.GetPoint(distance));
            if (Mathf.Abs(point.x) <= .082f && point.y <= -.112f) return false;
            float x = point.x / .154f, y = (point.y - .026f) / .159f;
            return x * x + y * y <= 1;
        }

        private void EnsureStyles()
        {
            if (label != null) return;
            menuFont = ColaGameFont.Load();
            label = new GUIStyle(GUI.skin.label) { font = menuFont, fontSize = 15, alignment = TextAnchor.MiddleCenter,
                richText = false, fontStyle = FontStyle.Normal, fixedHeight = 0, contentOffset = Vector2.zero, padding = new RectOffset() };
            SetFixedLabelColor(label, new Color(.74f, .75f, .71f));
            detail = new GUIStyle(label) { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleLeft };
            SetFixedLabelColor(detail, Ink);
            hoverText = new GUIStyle(detail) { alignment = TextAnchor.MiddleCenter };
            SetFixedLabelColor(hoverText, new Color(.72f, .73f, .69f));
            hoverNameShadow = new GUIStyle(label);
            hoverTextShadow = new GUIStyle(hoverText);
            SetFixedLabelColor(hoverNameShadow, new Color(0, 0, 0, .8f));
            SetFixedLabelColor(hoverTextShadow, new Color(0, 0, 0, .8f));
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
            if (!Application.IsPlaying(gameObject) || !CanShowHints || Screen.width < 320 || Screen.height < 240) return;
            EnsureStyles();
            Color previousTint = GUI.color, previousContent = GUI.contentColor;
            GUI.color = GUI.contentColor = Color.white;
            float scale = Scale;
            label.fontSize = hoverNameShadow.fontSize = Mathf.RoundToInt(15 * scale);
            detail.fontSize = hoverText.fontSize = hoverTextShadow.fontSize = Mathf.RoundToInt(14 * scale);
            float padding = Mathf.Round(15 * scale);
            float width = Mathf.Round(Screen.width - padding * 2);
            string playerEffects = "내 효과: " + items.EffectsText(false), bossEffects = "보스 효과: " + items.EffectsText(true);
            float playerHeight = Mathf.Ceil(Mathf.Max(detail.lineHeight + 6, detail.CalcHeight(new GUIContent(playerEffects), width) + 4));
            float bossHeight = Mathf.Ceil(Mathf.Max(detail.lineHeight + 6, detail.CalcHeight(new GUIContent(bossEffects), width) + 4));
            float bottom = Screen.height - Mathf.Round(8 * scale);
            float footerTop = bottom - bossHeight - playerHeight - 4;
            if (HasHoverDescription) DrawHoverDescription(scale, footerTop);
            GUI.Label(new Rect(padding, footerTop, width, playerHeight), playerEffects, detail);
            GUI.Label(new Rect(padding, bottom - bossHeight, width, bossHeight), bossEffects, detail);
            GUI.color = previousTint;
            GUI.contentColor = previousContent;
        }

        private void DrawHoverDescription(float scale, float footerTop)
        {
            float maximumWidth = Mathf.Min(Mathf.Max(600 * scale, 460), Screen.width - 32 * scale);
            float contentWidth = Mathf.Max(label.CalcSize(new GUIContent(hoverTitle ?? "")).x,
                hoverText.CalcSize(new GUIContent(hoverDescription)).x);
            float width = Mathf.Ceil(Mathf.Min(maximumWidth, contentWidth + 20));
            float bodyHeight = Mathf.Ceil(Mathf.Max(hoverText.lineHeight + 8, hoverText.CalcHeight(new GUIContent(hoverDescription), width) + 8));
            float titleHeight = string.IsNullOrEmpty(hoverTitle) ? 0 : Mathf.Ceil(label.lineHeight + 8);
            float x = Mathf.Round((Screen.width - width) * .5f);
            float y = Mathf.Round(footerTop - 10 - bodyHeight - titleHeight);
            int depth = GUI.depth;
            bool wasEnabled = GUI.enabled;
            GUI.depth = -15; GUI.enabled = true;
            if (titleHeight > 0)
            {
                GUI.Label(new Rect(x + 1, y + 1, width, titleHeight), hoverTitle, hoverNameShadow);
                GUI.Label(new Rect(x, y, width, titleHeight), hoverTitle, label);
            }
            var body = new Rect(x, y + titleHeight, width, bodyHeight);
            GUI.Label(new Rect(body.x + 1, body.y + 1, body.width, body.height), hoverDescription, hoverTextShadow);
            GUI.Label(body, hoverDescription, hoverText);
            GUI.depth = depth; GUI.enabled = wasEnabled;
        }

        private void OnDisable() => ResetPresentation();

        private void OnDestroy()
        {
            ResetPresentation();
            var itemUse = GetComponent<ColaItemUsePresentation>();
            if (itemUse) itemUse.Cancel();
            if (items)
            {
                items.InventoryChanged -= RefreshInventory;
                items.WaiterOrdered -= DeliverOrder;
                items.PresentationReset -= ResetPresentation;
            }
            if (inventoryRoot) Destroy(inventoryRoot);
            if (bossInventoryRoot) Destroy(bossInventoryRoot);
            modelLibrary?.Dispose();
            foreach (var material in storageMaterials) if (material) Destroy(material);
            foreach (var mesh in storageMeshes) if (mesh) Destroy(mesh);
        }
    }
}
