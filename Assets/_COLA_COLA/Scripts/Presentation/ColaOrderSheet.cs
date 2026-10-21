using System.Collections.Generic;
using System.Text;
using ColaCola.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColaCola.Presentation
{
    /// <summary>Debug-only free item test clipboard, opened with E; absent from the normal tabletop.</summary>
    [DisallowMultipleComponent]
    public sealed partial class ColaOrderSheet : MonoBehaviour
    {
        private ColaItemController items;
        private GameObject modelRoot;
        private Font font;
        private Texture2D paperTexture;
        private readonly List<Material> ownedMaterials = new();
        private readonly List<Mesh> ownedMeshes = new();
        private readonly List<Renderer> detailedPrint = new();
        private readonly Collider[] purchaseTargets = new Collider[ColaItemCatalog.All.Count];
        private readonly Collider[] nameTargets = new Collider[ColaItemCatalog.All.Count];
        private readonly Renderer[] purchaseRenderers = new Renderer[ColaItemCatalog.All.Count];
        private readonly GameObject[] itemRows = new GameObject[ColaItemCatalog.All.Count];
        private readonly Collider[] removeTargets = new Collider[ColaItemCatalog.All.Count];
        private readonly TextMesh[] priceTexts = new TextMesh[ColaItemCatalog.All.Count];
        private readonly TextMesh[] itemNameTexts = new TextMesh[ColaItemCatalog.All.Count];
        private readonly TextMesh[] numberTexts = new TextMesh[ColaItemCatalog.All.Count];
        private readonly Renderer[] rowRenderers = new Renderer[ColaItemCatalog.All.Count];
        private readonly Renderer[] removeRenderers = new Renderer[ColaItemCatalog.All.Count];
        private readonly TextMesh[] removeMarks = new TextMesh[ColaItemCatalog.All.Count];
        private readonly List<ColaItemId> cart = new(ColaItemController.Capacity);
        private Material available, unavailable, selectedKey, checkoutReady, rowSelected;
        private Collider openTarget, closeTarget, checkoutTarget;
        private Renderer checkoutRenderer, descriptionPanel, descriptionAccent;
        private TextMesh balanceText, inventoryText, descriptionText, descriptionTitle, checkoutText, titleText;
        private bool returning;
        private float scrollPosition;
        private int EntryCount => IsProgressionPage ? progressionEntries.Count : ColaItemCatalog.All.Count;
        private int FirstVisible => Mathf.Clamp(Mathf.RoundToInt(scrollPosition), 0, Mathf.Max(0, EntryCount - ColaItemController.MenuSize));
        private GUIStyle quantityStyle;
        private readonly Vector3 restPosition = new(.37f, .848f, -.37f);
        private readonly Quaternion restRotation = Quaternion.Euler(90, 0, -8);
        private Vector3 HeldLocalPosition => IsRecordPage ? new Vector3(0, 0, .58f) : new Vector3(.09f, .015f, .58f);
        public bool IsOpen { get; private set; }
        public bool IsSettled { get; private set; } = true;
        public bool BlocksGameplay => IsOpen || returning;
        public bool IsVisible => modelRoot && modelRoot.activeInHierarchy;
        public Transform ModelRoot => modelRoot ? modelRoot.transform : null;
        public Collider OpenCollider => openTarget;
        public Collider CloseCollider => closeTarget;
        public Collider CheckoutCollider => checkoutTarget;
        public Collider PurchaseCollider(ColaItemId id) => LookupTarget(purchaseTargets, id);
        public Collider RemoveCollider(ColaItemId id) => LookupTarget(removeTargets, id);
        public Collider NameCollider(ColaItemId id) => LookupTarget(nameTargets, id);
        private static Collider LookupTarget(Collider[] targets, ColaItemId id)
            => ColaItemCatalog.Contains(id) ? targets[ColaItemCatalog.IndexOf(id)] : null;
        public string Description => descriptionText ? descriptionText.text : "";
        public int CartCount => cart.Count;
        public int CartTotal => 0;
        public int CartQuantity(ColaItemId id) { int count = 0; foreach (var entry in cart) if (entry == id) count++; return count; }
        public bool CanAdd(ColaItemId id) => items && items.CanPurchase(id) &&
            items.InventoryCount + cart.Count < items.CapacityFor();
        public void ClearCart() { cart.Clear(); Refresh(); }
        public bool SetCartQuantity(ColaItemId id, int quantity)
        {
            if (!IsOpen || !CanOpen || !ColaItemCatalog.Contains(id)) return false;
            int current = CartQuantity(id);
            int maximum = Mathf.Max(0, items.CapacityFor() - items.InventoryCount - cart.Count + current);
            quantity = Mathf.Clamp(quantity, 0, maximum);
            if (quantity == current) return false;
            cart.RemoveAll(entry => entry == id);
            for (int i = 0; i < quantity; i++) cart.Add(id);
            Refresh(); return true;
        }
        public bool CanOpen
        {
            get
            {
                if (ColaFrontEnd.BlocksGameplay) return false;
                if (IsProgressionPage) return ProgressionPageAvailable;
                if (!isActiveAndEnabled || !items || !items.CanAct || !items.OrderAvailable()) return false;
                foreach (var definition in ColaItemCatalog.All) if (items.CanPurchase(definition.Id)) return true;
                return false;
            }
        }

        public void Bind(ColaItemController controller)
        {
            if (items) items.InventoryChanged -= RefreshAvailability;
            items = controller;
            items.InventoryChanged += RefreshAvailability;
            if (!modelRoot) BuildModel();
            BuildRecordTabs();
            Page = SheetPage.Items;
            IsOpen = returning = false;
            recordEntering = false;
            cart.Clear();
            scrollPosition = 0;
            IsSettled = true;
            modelRoot.transform.SetPositionAndRotation(restPosition, restRotation);
            RefreshAvailability();
        }

        public bool Toggle()
        {
            if (IsProgressionPage) Close();
            Page = SheetPage.Items;
            RefreshAvailability();
            if (IsOpen) { Close(); return true; }
            if (!CanOpen || returning) return false;
            IsOpen = true;
            IsSettled = true;
            var camera = items.match.interaction.playerCamera.transform;
            modelRoot.transform.SetPositionAndRotation(camera.TransformPoint(new Vector3(.09f, .015f, .58f)), camera.rotation);
            ClearDescription();
            RefreshAvailability();
            ColaTabletopAudio.Cue(items.match.interaction, "MenuOpen", modelRoot.transform.position, .55f);
            return true;
        }

        public void Close()
        {
            if (IsOpen && items && items.match)
                ColaTabletopAudio.Cue(items.match.interaction, "MenuClose", modelRoot.transform.position, .50f);
            IsOpen = false;
            recordEntering = false;
            if (IsProgressionPage && items && items.match && items.match.Progression != null)
                items.match.Progression.JournalOpen = false;
            cart.Clear();
            returning = false;
            IsSettled = true;
            if (modelRoot) modelRoot.SetActive(false);
        }

        public bool HandlePointer(Vector2 screenPoint)
        {
            if (!CanOpen || !IsVisible) return false;
            if (IsProgressionPage && ControlRect(.136f, .106f, .148f, -.067f).Contains(new Vector2(screenPoint.x, Screen.height - screenPoint.y))) return true;
            if (!IsProgressionPage && OverSlider(screenPoint)) return true;
            Physics.SyncTransforms();
            if (!Physics.Raycast(items.match.interaction.playerCamera.ScreenPointToRay(screenPoint), out var hit, 8, ~0, QueryTriggerInteraction.Ignore)) return false;
            var target = hit.collider.GetComponent<ColaOrderTarget>();
            return target && target.transform.IsChildOf(modelRoot.transform) && HandleTarget(target);
        }

        public bool HandleTarget(ColaOrderTarget target)
        {
            if (IsProgressionPage) return HandleProgressionTarget(target);
            if (!CanOpen || !IsVisible || !target || !target.gameObject.activeInHierarchy ||
                !target.transform.IsChildOf(modelRoot.transform)) return false;
            if (target.Kind == ColaOrderTarget.Action.Open) return IsOpen || Toggle();
            if (target.Kind == ColaOrderTarget.Action.Close && IsOpen) { Close(); return true; }
            if (target.Kind == ColaOrderTarget.Action.Inspect && IsOpen && IsSettled)
            { ShowDescription(target.Item); return true; }
            if (!IsOpen || !IsSettled) return false;
            if (target.Kind == ColaOrderTarget.Action.Buy)
            {
                if (!CanAdd(target.Item))
                {
                    items.match.SetActionMessage("담을 수 없습니다. 주문 합계와 빈 칸을 확인하세요.");
                    ColaTabletopAudio.Cue(items.match.interaction, "Blocked", modelRoot.transform.position, .45f);
                    return false;
                }
                cart.Add(target.Item); Refresh();
                ColaTabletopAudio.Cue(items.match.interaction, "Cart", modelRoot.transform.position, .50f);
                return true;
            }
            if (target.Kind == ColaOrderTarget.Action.Remove)
            {
                bool removed = cart.Remove(target.Item); Refresh();
                if (removed) ColaTabletopAudio.Cue(items.match.interaction, "Cart", modelRoot.transform.position, .40f);
                return removed;
            }
            if (target.Kind != ColaOrderTarget.Action.Checkout || !items.TryPurchaseBatch(cart)) return false;
            ColaTabletopAudio.Cue(items.match.interaction, "Tokens", modelRoot.transform.position, .55f);
            Close(); return true;
        }

        private void Update()
        {
            if (ColaFrontEnd.BlocksGameplay || ColaFrontEnd.ConsumedEscapeThisFrame) return;
            if (!Application.IsPlaying(gameObject) || !items || !modelRoot) return;
            SyncProgressionPage();
            RefreshAvailability();
            if (!IsVisible) return;
            if (IsOpen && Page != SheetPage.AbilityChoice && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
            if (IsOpen && Mouse.current != null && Mathf.Abs(Mouse.current.scroll.ReadValue().y) > .01f)
            {
                scrollPosition = Mathf.Clamp(FirstVisible - Mathf.Sign(Mouse.current.scroll.ReadValue().y),
                    0, Mathf.Max(0, EntryCount - ColaItemController.MenuSize));
                ClearDescription();
            }
            var camera = items.match.interaction.playerCamera.transform;
            Vector3 targetPosition = IsOpen ? camera.TransformPoint(HeldLocalPosition) : restPosition;
            Quaternion targetRotation = IsOpen ? camera.rotation : restRotation;
            if (!AdvanceRecordEntrance(items.match.interaction.playerCamera))
            {
                float blend = 1 - Mathf.Exp(-16 * ColaGameClock.Delta);
                modelRoot.transform.SetPositionAndRotation(Vector3.Lerp(modelRoot.transform.position, targetPosition, blend),
                    Quaternion.Slerp(modelRoot.transform.rotation, targetRotation, blend));
                IsSettled = Vector3.Distance(modelRoot.transform.position, targetPosition) < .002f &&
                    Quaternion.Angle(modelRoot.transform.rotation, targetRotation) < 1;
            }
            if (IsOpen && IsSettled)
                modelRoot.transform.SetPositionAndRotation(targetPosition, targetRotation);
            if (!IsOpen && IsSettled)
            {
                returning = false;
                modelRoot.transform.SetPositionAndRotation(restPosition, restRotation);
            }
            Refresh();
            if (IsOpen && Mouse.current != null) HoverPointer(Mouse.current.position.ReadValue());
        }

        public bool HoverPointer(Vector2 screenPoint)
        {
            if (IsProgressionPage) return HoverProgression(screenPoint);
            if (!IsOpen || !IsSettled || !CanOpen || !IsVisible) return false;
            Physics.SyncTransforms();
            if (Physics.Raycast(items.match.interaction.playerCamera.ScreenPointToRay(screenPoint), out var hit, 8,
                    ~0, QueryTriggerInteraction.Ignore) && hit.collider.TryGetComponent<ColaOrderTarget>(out var target) &&
                target.transform.IsChildOf(modelRoot.transform) &&
                (target.Kind == ColaOrderTarget.Action.Buy || target.Kind == ColaOrderTarget.Action.Inspect ||
                    target.Kind == ColaOrderTarget.Action.Remove))
            { ShowDescription(target.Item); return true; }
            ClearDescription();
            return false;
        }

        public void ShowDescription(ColaItemId id)
        {
            var definition = ColaItemCatalog.Get(id);
            SetBoardDescription(definition.Name, items.EffectDescription(id));
        }

        private void SetBoardDescription(string name, string description)
        {
            descriptionTitle.text = name ?? "";
            descriptionText.text = description ?? "";
            descriptionTitle.transform.localPosition = new Vector3(-.122f, -.091f, -.009f);
            descriptionText.transform.localPosition = new Vector3(-.122f, -.108f, -.009f);
            descriptionTitle.characterSize = .0028f;
            descriptionText.characterSize = .0027f;
            RefreshDescriptionVisibility();
        }

        private void RefreshDescriptionVisibility()
        {
            bool visible = IsOpen && IsSettled && !string.IsNullOrEmpty(descriptionText.text);
            descriptionTitle.GetComponent<Renderer>().enabled = visible;
            descriptionText.GetComponent<Renderer>().enabled = visible;
            descriptionPanel.enabled = descriptionAccent.enabled = false;
        }

        private void ClearDescription()
        {
            descriptionTitle.text = "";
            descriptionText.text = "";
            descriptionPanel.enabled = descriptionAccent.enabled = false;
            if (IsProgressionPage) { recordName = recordDescription = ""; inspectedEntry = -1; }
        }

        public void RefreshAvailability()
        {
            if (!items || !modelRoot) return;
            bool visible = CanOpen && IsOpen;
            if (!visible)
            {
                // Hide the entire prop, including all hit targets; never leave an invisible input lock.
                IsOpen = returning = false;
                recordEntering = false;
                cart.Clear();
                IsSettled = true;
                modelRoot.transform.SetPositionAndRotation(restPosition, restRotation);
            }
            modelRoot.SetActive(visible);
            Refresh();
        }

        private void Refresh()
        {
            if (!items || !modelRoot) return;
            if (IsProgressionPage) { RefreshProgression(); return; }
            SetRecordTabsVisible(false);
            // Fine world text aliases at the tabletop's grazing angle. Only draw it while held for reading.
            foreach (var renderer in detailedPrint) if (renderer) renderer.enabled = IsOpen;
            titleText.text = ColaLocalization.Get("ui.test_menu", "테스트 메뉴") + "  " + ColaItemCatalog.All.Count;
            titleText.characterSize = IsOpen ? .0030f : .006f;
            titleText.anchor = IsOpen ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            titleText.transform.localPosition = IsOpen ? new Vector3(-.128f, .141f, -.009f) : new Vector3(0, .025f, -.009f);
            titleText.transform.localRotation = IsOpen ? Quaternion.Euler(0, 0, -1.2f) : Quaternion.identity;
            balanceText.text = ColaLocalization.Get("ui.tokens_label", "토큰") + "  " + items.match.PlayerTokens.ToString("00");
            inventoryText.text = ColaLocalization.Get("ui.items_label", "아이템") + "  " + items.InventoryCount + " / " + items.CapacityFor();
            if (!IsOpen) ClearDescription();
            RefreshDescriptionVisibility();
            closeTarget.enabled = IsOpen;
            checkoutTarget.enabled = IsVisible && IsOpen && IsSettled && items.CanPurchaseBatch(cart);
            checkoutText.text = cart.Count == 0 ? ColaLocalization.Get("ui.free_claim", "무료 받기") : string.Format(ColaLocalization.Get("ui.free_count", "무료 받기   {0}개"), cart.Count);
            checkoutRenderer.sharedMaterial = items.CanPurchaseBatch(cart) ? checkoutReady : unavailable;
            for (int i = 0; i < purchaseTargets.Length; i++)
            {
                var definition = ColaItemCatalog.All[i];
                int menuRow = i - FirstVisible;
                bool offered = menuRow >= 0 && menuRow < ColaItemController.MenuSize;
                itemRows[i].SetActive(offered);
                if (offered)
                {
                    itemRows[i].transform.localPosition = new Vector3(0, .093f - menuRow * .029f, 0);
                    numberTexts[i].text = (i + 1).ToString();
                }
                purchaseTargets[i].enabled = false;
                nameTargets[i].enabled = offered && IsVisible && IsOpen && IsSettled;
                removeTargets[i].enabled = false;
                int quantity = CartQuantity(definition.Id);
                purchaseRenderers[i].sharedMaterial = CanAdd(definition.Id) ? quantity > 0 ? selectedKey : available : unavailable;
                removeRenderers[i].sharedMaterial = quantity > 0 ? available : unavailable;
                rowRenderers[i].enabled = IsOpen && quantity > 0;
                itemNameTexts[i].text = definition.Name;
                itemNameTexts[i].characterSize = .0023f;
                priceTexts[i].text = ColaLocalization.Get("ui.free", "무료") + (quantity > 0 ? " x" + quantity : "");
                purchaseRenderers[i].enabled = removeRenderers[i].enabled = false;
                priceTexts[i].GetComponent<Renderer>().enabled = false;
                if (removeMarks[i]) removeMarks[i].GetComponent<Renderer>().enabled = false;
            }
        }

        // Project controls onto the held clipboard. Sliders select quantities; scrolling exposes all 20 kinds.
        private Rect ControlRect(float left, float top, float right, float bottom)
        {
            var camera = items.match.interaction.playerCamera;
            Vector3 a = camera.WorldToScreenPoint(modelRoot.transform.TransformPoint(new Vector3(left, top, -.016f)));
            Vector3 b = camera.WorldToScreenPoint(modelRoot.transform.TransformPoint(new Vector3(right, bottom, -.016f)));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Screen.height - Mathf.Max(a.y, b.y),
                Mathf.Max(a.x, b.x), Screen.height - Mathf.Min(a.y, b.y));
        }

        private bool OverSlider(Vector2 screenPoint)
        {
            if (!IsOpen) return false;
            Vector2 point = new(screenPoint.x, Screen.height - screenPoint.y);
            if (ControlRect(.136f, .106f, .148f, -.067f).Contains(point)) return true;
            for (int row = 0; row < ColaItemController.MenuSize; row++)
            {
                float y = .093f - row * .029f;
                if (ControlRect(.035f, y + .012f, .133f, y - .012f).Contains(point)) return true;
            }
            return false;
        }

        private void OnGUI()
        {
            DrawReadablePrint();
            if (IsProgressionPage) { DrawProgressionOverlay(); return; }
            if (!Application.IsPlaying(gameObject) || !IsOpen || !IsSettled || !items || !CanOpen || !IsVisible) return;
            quantityStyle ??= new GUIStyle(GUI.skin.label) { font = font, alignment = TextAnchor.MiddleRight };
            quantityStyle.normal.textColor = new Color(.15f, .20f, .15f);
            int oldDepth = GUI.depth; GUI.depth = -20;
            Color oldBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(.45f, .43f, .30f);
            var scroll = ControlRect(.136f, .106f, .148f, -.067f);
            scrollPosition = GUI.VerticalScrollbar(scroll, scrollPosition, ColaItemController.MenuSize, 0, ColaItemCatalog.All.Count);
            int start = FirstVisible;
            for (int row = 0; row < ColaItemController.MenuSize && start + row < ColaItemCatalog.All.Count; row++)
            {
                var id = ColaItemCatalog.All[start + row].Id;
                int quantity = CartQuantity(id);
                int maximum = Mathf.Max(0, items.CapacityFor() - items.InventoryCount - cart.Count + quantity);
                float y = .093f - row * .029f;
                var rect = ControlRect(.035f, y + .011f, .116f, y - .011f);
                int selected = Mathf.RoundToInt(GUI.HorizontalSlider(rect, quantity, 0, Mathf.Max(1, maximum)));
                if (SetCartQuantity(id, selected)) ShowDescription(id);
                quantityStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(rect.height * .55f), 10, 22);
                GUI.Label(ControlRect(.115f, y + .012f, .132f, y - .012f), CartQuantity(id).ToString(), quantityStyle);
            }
            GUI.backgroundColor = oldBackground; GUI.depth = oldDepth;
        }

        private Material Surface(string name, Color color)
        {
            // The near-camera paper must stay readable without depending on the room's horror lighting.
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name, hideFlags = HideFlags.DontSave };
            material.SetColor("_BaseColor", color);
            ownedMaterials.Add(material);
            return material;
        }

        private GameObject MeshPart(string name, Mesh mesh, Material material, Vector3 position)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(modelRoot.transform, false);
            part.transform.localPosition = position;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            ownedMeshes.Add(mesh);
            return part;
        }

        private GameObject Block(string name, Vector3 position, Vector3 size, Material material, bool collider)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(modelRoot.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (name.StartsWith("OrderKey_") || name.StartsWith("RemoveKey_") || name.StartsWith("Print") ||
                name == "ConfirmOrder" || name == "ReturnClipboard") detailedPrint.Add(part.GetComponent<MeshRenderer>());
            if (!collider) Destroy(part.GetComponent<Collider>());
            return part;
        }

        private GameObject Key(string name, Vector3 position, Vector2 size, Material material)
        {
            var key = MeshPart(name, Plate(size.x, size.y, .0015f, .0016f), material, position);
            var hit = key.AddComponent<BoxCollider>();
            hit.size = new Vector3(size.x, size.y, .004f);
            detailedPrint.Add(key.GetComponent<MeshRenderer>());
            return key;
        }

        private TextMesh Text(string name, string text, Vector3 position, int size, float characterSize, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var part = new GameObject(name, typeof(TextMesh));
            part.transform.SetParent(modelRoot.transform, false);
            part.transform.localPosition = position;
            var result = part.GetComponent<TextMesh>();
            result.font = font;
            result.fontSize = size;
            result.characterSize = characterSize;
            result.text = text;
            printedLabels.Add(result);
            result.anchor = anchor;
            result.color = color;
            ColaWorldText.Bind(result);
            if (name != "ReceiptTitle") detailedPrint.Add(part.GetComponent<MeshRenderer>());
            return result;
        }

        private void BuildModel()
        {
            modelRoot = new GameObject("Pizzeria_OrderClipboard_Runtime") { hideFlags = HideFlags.DontSave };
            modelRoot.transform.SetParent(items.transform.root, false);
            font = ColaGameFont.Load();
            var board = Surface("Receipt_AgedBoard", new Color(.16f, .17f, .12f));
            var paper = Surface("Receipt_WarmPaper", new Color(.92f, .87f, .73f));
            paperTexture = MakePaperTexture();
            paper.SetTexture("_BaseMap", paperTexture);
            var metal = Surface("Receipt_Clip", new Color(.29f, .28f, .22f));
            var wornEdge = Surface("Receipt_BoardWear", new Color(.28f, .27f, .20f));
            var tape = Surface("Receipt_MaskingTape", new Color(.69f, .64f, .46f));
            var rule = Surface("Receipt_PrintRules", new Color(.48f, .44f, .33f));
            var red = Surface("Receipt_TomatoInk", new Color(.40f, .14f, .095f));
            var chip = Surface("Receipt_BalanceChip", new Color(.84f, .80f, .65f));
            var detailPaper = Surface("Receipt_DetailPanel", new Color(.89f, .83f, .67f));
            available = Surface("Receipt_OrderKey", new Color(.23f, .25f, .19f));
            unavailable = Surface("Receipt_DisabledKey", new Color(.44f, .43f, .34f));
            selectedKey = Surface("Receipt_AddedKey", new Color(.39f, .19f, .12f));
            checkoutReady = red;
            rowSelected = Surface("Receipt_RowAdded", new Color(.85f, .78f, .60f));
            MeshPart("BeveledClipboard", Plate(.318f, .392f, .018f, .014f), board, new Vector3(0, 0, .007f));
            MeshPart("ReceiptPaper", PaperSheet(), paper, new Vector3(0, 0, -.005f));
            foreach (int side in new[] { -1, 1 })
                Block("BoardEdgeWear_" + side, new Vector3(side * .153f, side * .145f, -.0005f),
                    new Vector3(.004f, .029f, .0005f), wornEdge, false);
            var topTape = Block("ReceiptTopTape", new Vector3(-.127f, .176f, -.0075f), new Vector3(.035f, .011f, .001f), tape, false);
            topTape.transform.localRotation = Quaternion.Euler(0, 0, -12);
            var bottomTape = Block("ReceiptBottomTape", new Vector3(.129f, -.175f, -.0075f), new Vector3(.033f, .010f, .001f), tape, false);
            bottomTape.transform.localRotation = Quaternion.Euler(0, 0, 14);
            var fold = new Mesh { name = "ReceiptFoldedCorner", hideFlags = HideFlags.DontSave };
            fold.vertices = new[] { new Vector3(.147f, .178f, -.006f), new Vector3(.125f, .178f, -.013f), new Vector3(.147f, .156f, -.013f) };
            fold.triangles = new[] { 0, 1, 2, 2, 1, 0 };
            fold.RecalculateNormals();
            MeshPart("FoldedPaperCorner", fold, paper, Vector3.zero);
            Block("ClipBridge", new Vector3(0, .180f, -.013f), new Vector3(.085f, .018f, .012f), metal, false);
            Block("ClipTab", new Vector3(0, .192f, -.007f), new Vector3(.052f, .012f, .012f), metal, false);
            Block("ClipWornEdge", new Vector3(-.018f, .173f, -.0195f), new Vector3(.027f, .002f, .001f), wornEdge, false);
            var box = modelRoot.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0, .006f);
            box.size = new Vector3(.318f, .392f, .020f);
            modelRoot.AddComponent<ColaOrderTarget>().Bind(ColaOrderTarget.Action.Open);
            openTarget = box;
            Color ink = new(.10f, .15f, .11f), keyInk = new(.96f, .91f, .77f), redInk = new(.40f, .14f, .095f);
            var brand = Text("RestaurantBrand", "PIZZERIA", new Vector3(-.128f, .164f, -.009f), 48, .0022f, redInk);
            brand.transform.localRotation = Quaternion.Euler(0, 0, -1.2f);
            titleText = Text("ReceiptTitle", "MENU", new Vector3(-.128f, .141f, -.009f), 64, .0030f, ink);
            titleText.fontStyle = FontStyle.Normal;
            balanceText = Text("OrderBalance", "", new Vector3(-.122f, .119f, -.009f), 48, .0027f, ink);
            inventoryText = Text("OrderInventory", "", new Vector3(.038f, .119f, -.009f), 48, .0027f, ink);
            Block("PrintHeaderRule", new Vector3(0, .107f, -.007f), new Vector3(.260f, .002f, .001f), red, false);
            for (int row = 0; row < 2; row++)
                for (int column = 0; column < 6; column++)
                    Block("PrintHeaderCheck_" + row + "_" + column, new Vector3(.055f + column * .009f, .155f - row * .0065f, -.007f),
                        new Vector3(.0085f, .006f, .001f), (row + column) % 2 == 0 ? red : chip, false);
            for (int i = 0; i < 18; i++)
                Block("PrintPerforation_" + i, new Vector3(-.140f, -.154f + i * .018f, -.007f),
                    new Vector3(.0024f, .0032f, .001f), board, false);
            var close = Key("ReturnClipboard", new Vector3(.131f, .166f, -.012f), new Vector2(.022f, .022f), board);
            close.AddComponent<ColaOrderTarget>().Bind(ColaOrderTarget.Action.Close);
            closeTarget = close.GetComponent<Collider>();
            Text("CloseMark", "X", new Vector3(.131f, .166f, -.015f), 48, .0028f, keyInk, TextAnchor.MiddleCenter);
            for (int i = 0; i < ColaItemCatalog.All.Count; i++)
            {
                var definition = ColaItemCatalog.All[i];
                var row = new GameObject("ReceiptRow_" + (i + 1));
                row.transform.SetParent(modelRoot.transform, false);
                itemRows[i] = row;
                float y = 0;
                var background = Block("PrintRow_" + i, new Vector3(0, y, -.0067f), new Vector3(.270f, .027f, .001f), rowSelected, false);
                background.transform.SetParent(row.transform, false); rowRenderers[i] = background.GetComponent<Renderer>();
                var badge = Block("PrintNumberBadge_" + i, new Vector3(-.118f, y, -.0085f), new Vector3(.022f, .020f, .001f), red, false);
                badge.transform.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? -1.8f : 1.2f);
                badge.transform.SetParent(row.transform, false);
                numberTexts[i] = Text("MenuNumber_" + i, "", new Vector3(-.118f, y, -.010f), 48, .0030f, keyInk, TextAnchor.MiddleCenter);
                numberTexts[i].transform.SetParent(row.transform, false);
                var itemName = Text("OrderItem_" + (i + 1), definition.Name,
                    new Vector3(-.098f, y, -.009f), 64, .0023f, ink);
                itemNameTexts[i] = itemName;
                itemName.transform.SetParent(row.transform, false);
                var nameHit = itemName.gameObject.AddComponent<BoxCollider>();
                nameHit.center = new Vector3(.067f, 0, 0);
                nameHit.size = new Vector3(.194f, .024f, .001f);
                itemName.gameObject.AddComponent<ColaOrderTarget>().Bind(ColaOrderTarget.Action.Inspect, definition.Id);
                nameTargets[i] = nameHit;
                var key = Key("OrderKey_" + (i + 1), new Vector3(.092f, y, -.012f), new Vector2(.041f, .024f), available);
                key.transform.SetParent(row.transform, false);
                key.AddComponent<ColaOrderTarget>().Bind(ColaOrderTarget.Action.Buy, definition.Id);
                purchaseTargets[i] = key.GetComponent<Collider>();
                purchaseRenderers[i] = key.GetComponent<Renderer>();
                priceTexts[i] = Text("OrderPrice_" + (i + 1), definition.Price + " T", new Vector3(.092f, y, -.015f), 48, .0034f,
                    keyInk, TextAnchor.MiddleCenter);
                priceTexts[i].transform.SetParent(row.transform, false);
                var minus = Key("RemoveKey_" + (i + 1), new Vector3(.127f, y, -.012f), new Vector2(.019f, .024f), unavailable);
                minus.transform.SetParent(row.transform, false);
                minus.AddComponent<ColaOrderTarget>().Bind(ColaOrderTarget.Action.Remove, definition.Id);
                removeTargets[i] = minus.GetComponent<Collider>();
                removeRenderers[i] = minus.GetComponent<Renderer>();
                removeMarks[i] = Text("RemoveMark_" + (i + 1), "-", new Vector3(.127f, y, -.015f), 48, .0028f, keyInk, TextAnchor.MiddleCenter);
                removeMarks[i].transform.SetParent(row.transform, false);
                Block("PrintRule_" + (i + 1), new Vector3(0, y - .014f, -.0075f), new Vector3(.26f, .0004f, .0002f), rule,
                    false).transform.SetParent(row.transform, false);
            }
            descriptionPanel = Block("PrintDetailsPanel", new Vector3(0, -.104f, -.0068f), new Vector3(.270f, .066f, .001f), detailPaper, false).GetComponent<Renderer>();
            descriptionAccent = Block("PrintDetailsAccent", new Vector3(-.134f, -.104f, -.0075f), new Vector3(.002f, .066f, .001f), red, false).GetComponent<Renderer>();
            descriptionTitle = Text("ReceiptDescriptionTitle", "", new Vector3(-.122f, -.081f, -.009f), 48, .0027f, redInk);
            descriptionText = Text("ReceiptDescription", "", new Vector3(-.122f, -.093f, -.009f), 48, .0028f, ink, TextAnchor.UpperLeft);
            descriptionText.lineSpacing = .95f;
            var checkout = Key("ConfirmOrder", new Vector3(0, -.154f, -.012f), new Vector2(.254f, .027f), unavailable);
            checkout.AddComponent<ColaOrderTarget>().Bind(ColaOrderTarget.Action.Checkout);
            checkoutTarget = checkout.GetComponent<Collider>(); checkoutRenderer = checkout.GetComponent<Renderer>();
            checkoutText = Text("ConfirmOrderText", "", new Vector3(0, -.154f, -.015f), 48, .0030f, keyInk, TextAnchor.MiddleCenter);
        }

        private static string Wrap(string text, int length)
        {
            var result = new StringBuilder();
            int used = 0;
            foreach (string word in text.Split(' '))
            {
                if (used > 0)
                {
                    if (used + 1 + word.Length > length) { result.Append(" \n"); used = 0; }
                    else { result.Append(' '); used++; }
                }
                result.Append(word); used += word.Length;
            }
            return result.ToString();
        }

        private static Texture2D MakePaperTexture()
        {
            // Fixed paper wear never consumes gameplay randomness or changes between frames.
            const int width = 256, height = 320;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, true)
            {
                name = "Receipt_PaperGrain_Runtime", hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[width * height];
            var wear = new System.Random(18467);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float u = x / (width - 1f), v = y / (height - 1f);
                    float edge = 1 - Mathf.Clamp01(Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v)) / .07f);
                    float fold = 1 - Mathf.Clamp01(Mathf.Abs(v - .49f) / .009f);
                    float ringDistance = new Vector2((u - .96f) * .83f, v - .20f).magnitude;
                    float ring = 1 - Mathf.Clamp01(Mathf.Abs(ringDistance - .10f) / .009f);
                    float shade = 1 - (float)wear.NextDouble() * .035f - edge * .06f - fold * .018f - ring * .065f;
                    pixels[y * width + x] = new Color(shade, shade - .005f, shade - .018f);
                }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Mesh PaperSheet()
        {
            var edge = new List<Vector2>();
            for (int i = 0; i <= 18; i++)
            {
                float nick = i == 0 || i == 18 ? 0 : (i * 7 % 5) * .00035f;
                edge.Add(new Vector2(Mathf.Lerp(-.141f, .141f, i / 18f), -.178f + nick));
            }
            edge.AddRange(new[] { new Vector2(.147f, -.172f), new Vector2(.147f, .172f),
                new Vector2(.141f, .178f), new Vector2(-.141f, .178f),
                new Vector2(-.147f, .172f), new Vector2(-.147f, -.172f) });
            return PolygonPlate(edge, .0015f, .294f, .356f, "Receipt_WornPaperMesh");
        }

        private static Mesh Plate(float width, float height, float corner, float depth)
        {
            var points = new[] { new Vector2(-width / 2 + corner, -height / 2), new Vector2(width / 2 - corner, -height / 2),
                new Vector2(width / 2, -height / 2 + corner), new Vector2(width / 2, height / 2 - corner),
                new Vector2(width / 2 - corner, height / 2), new Vector2(-width / 2 + corner, height / 2),
                new Vector2(-width / 2, height / 2 - corner), new Vector2(-width / 2, -height / 2 + corner) };
            return PolygonPlate(points, depth, width, height, "ReceiptBeveledPlate");
        }

        private static Mesh PolygonPlate(IReadOnlyList<Vector2> points, float depth, float width, float height, string name)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            { int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2); }
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Count];
                Vector3 af = new(a.x, a.y, -depth / 2), bf = new(b.x, b.y, -depth / 2);
                Vector3 ab = new(a.x, a.y, depth / 2), bb = new(b.x, b.y, depth / 2);
                Triangle(new Vector3(0, 0, -depth / 2), bf, af);
                Triangle(new Vector3(0, 0, depth / 2), ab, bb);
                Triangle(af, bf, ab); Triangle(bf, bb, ab);
            }
            var uv = new List<Vector2>(vertices.Count);
            foreach (var vertex in vertices) uv.Add(new Vector2(vertex.x / width + .5f, vertex.y / height + .5f));
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDisable() { RefreshAvailability(); }
        private void OnDestroy()
        {
            if (items) items.InventoryChanged -= RefreshAvailability;
            if (modelRoot) Destroy(modelRoot);
            foreach (var material in ownedMaterials) if (material) Destroy(material);
            foreach (var mesh in ownedMeshes) if (mesh) Destroy(mesh);
            if (paperTexture) Destroy(paperTexture);
        }
    }
}
