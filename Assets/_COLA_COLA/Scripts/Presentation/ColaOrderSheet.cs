using System.Collections.Generic;
using ColaCola.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColaCola.Presentation
{
    /// <summary>A real clipboard/paper model. Selection and checkout use 3D collider targets, without a Canvas or GUI menu.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaOrderSheet : MonoBehaviour
    {
        private ColaItemController items;
        private GameObject modelRoot;
        private Font font;
        private readonly List<Material> ownedMaterials = new();
        private readonly List<Mesh> ownedMeshes = new();
        private readonly Collider[] purchaseTargets = new Collider[11];
        private readonly Collider[] nameTargets = new Collider[11];
        private readonly Renderer[] purchaseRenderers = new Renderer[11];
        private readonly GameObject[] itemRows = new GameObject[11];
        private readonly Collider[] removeTargets = new Collider[11];
        private readonly TextMesh[] priceTexts = new TextMesh[11];
        private readonly List<ColaItemId> cart = new(ColaItemController.Capacity);
        private Material available, unavailable;
        private Collider openTarget, closeTarget, checkoutTarget;
        private Renderer checkoutRenderer;
        private TextMesh balanceText, statusText, descriptionText, checkoutText;
        private bool returning;
        private readonly Vector3 restPosition = new(.37f, .848f, -.37f);
        private readonly Quaternion restRotation = Quaternion.Euler(90, 0, -8);
        public bool IsOpen { get; private set; }
        public bool IsSettled { get; private set; } = true;
        public bool BlocksGameplay => IsOpen || returning;
        public bool IsVisible => modelRoot && modelRoot.activeInHierarchy;
        public Transform ModelRoot => modelRoot ? modelRoot.transform : null;
        public Collider OpenCollider => openTarget;
        public Collider CloseCollider => closeTarget;
        public Collider CheckoutCollider => checkoutTarget;
        public Collider PurchaseCollider(ColaItemId id) => purchaseTargets[(int)id];
        public Collider RemoveCollider(ColaItemId id) => removeTargets[(int)id];
        public Collider NameCollider(ColaItemId id) => nameTargets[(int)id];
        public string Description => descriptionText ? descriptionText.text : "";
        public int CartCount => cart.Count;
        public int CartTotal
        {
            get { int total = 0; foreach (var id in cart) total += ColaItemCatalog.Get(id).Price; return total; }
        }
        public int CartQuantity(ColaItemId id) { int count = 0; foreach (var entry in cart) if (entry == id) count++; return count; }
        public bool CanAdd(ColaItemId id) => items && items.CanPurchase(id) &&
            items.InventoryCount + cart.Count < ColaItemController.Capacity &&
            CartTotal + ColaItemCatalog.Get(id).Price <= items.match.PlayerTokens;
        public bool CanOpen
        {
            get
            {
                if (!isActiveAndEnabled || !items || !items.CanAct || !items.OrderAvailable()) return false;
                foreach (var id in items.BottleMenu) if (items.CanPurchase(id)) return true;
                return false;
            }
        }

        public void Bind(ColaItemController controller)
        {
            if (items) items.InventoryChanged -= RefreshAvailability;
            items = controller;
            items.InventoryChanged += RefreshAvailability;
            if (!modelRoot) BuildModel();
            IsOpen = returning = false;
            cart.Clear();
            IsSettled = true;
            modelRoot.transform.SetPositionAndRotation(restPosition, restRotation);
            RefreshAvailability();
        }

        public bool Toggle()
        {
            RefreshAvailability();
            if (IsOpen) { Close(); return true; }
            if (!CanOpen || returning) return false;
            IsOpen = true;
            IsSettled = false;
            descriptionText.text = "가격: 담기 / -: 수량 빼기\n아래 주문 확정으로 한 번에 결제";
            return true;
        }

        public void Close()
        {
            IsOpen = false;
            cart.Clear();
            returning = modelRoot && (Vector3.Distance(modelRoot.transform.position, restPosition) > .002f ||
                Quaternion.Angle(modelRoot.transform.rotation, restRotation) > 1);
            IsSettled = !returning;
        }

        public bool HandlePointer(Vector2 screenPoint)
        {
            if (!CanOpen || !IsVisible) return false;
            Physics.SyncTransforms();
            if (!Physics.Raycast(items.match.interaction.playerCamera.ScreenPointToRay(screenPoint), out var hit, 8, ~0, QueryTriggerInteraction.Ignore)) return false;
            var target = hit.collider.GetComponent<ColaOrderTarget>();
            return target && target.transform.IsChildOf(modelRoot.transform) && HandleTarget(target);
        }

        public bool HandleTarget(ColaOrderTarget target)
        {
            if (!CanOpen || !IsVisible || !target || !target.gameObject.activeInHierarchy ||
                !target.transform.IsChildOf(modelRoot.transform)) return false;
            if ((target.Kind == ColaOrderTarget.Action.Buy || target.Kind == ColaOrderTarget.Action.Inspect ||
                target.Kind == ColaOrderTarget.Action.Remove) &&
                !items.IsOnMenu(target.Item)) return false;
            if (target.Kind == ColaOrderTarget.Action.Open) return IsOpen || Toggle();
            if (target.Kind == ColaOrderTarget.Action.Close && IsOpen) { Close(); return true; }
            if (target.Kind == ColaOrderTarget.Action.Inspect && IsOpen && IsSettled)
            { ShowDescription(target.Item); return true; }
            if (!IsOpen || !IsSettled) return false;
            if (target.Kind == ColaOrderTarget.Action.Buy)
            {
                if (!CanAdd(target.Item)) { items.match.SetActionMessage("담을 수 없습니다. 주문 합계와 빈 칸을 확인하세요."); return false; }
                cart.Add(target.Item); Refresh(); return true;
            }
            if (target.Kind == ColaOrderTarget.Action.Remove)
            { bool removed = cart.Remove(target.Item); Refresh(); return removed; }
            if (target.Kind != ColaOrderTarget.Action.Checkout || !items.TryPurchaseBatch(cart)) return false;
            Close(); return true;
        }

        private void Update()
        {
            if (!Application.IsPlaying(gameObject) || !items || !modelRoot) return;
            RefreshAvailability();
            if (!IsVisible) return;
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
            var camera = items.match.interaction.playerCamera.transform;
            Vector3 targetPosition = IsOpen ? camera.TransformPoint(new Vector3(.09f, .015f, .58f)) : restPosition;
            Quaternion targetRotation = IsOpen ? camera.rotation : restRotation;
            float blend = 1 - Mathf.Exp(-16 * Time.unscaledDeltaTime);
            modelRoot.transform.SetPositionAndRotation(Vector3.Lerp(modelRoot.transform.position, targetPosition, blend),
                Quaternion.Slerp(modelRoot.transform.rotation, targetRotation, blend));
            IsSettled = Vector3.Distance(modelRoot.transform.position, targetPosition) < .002f &&
                Quaternion.Angle(modelRoot.transform.rotation, targetRotation) < 1;
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
            if (!IsOpen || !IsSettled || !CanOpen || !IsVisible) return false;
            Physics.SyncTransforms();
            if (Physics.Raycast(items.match.interaction.playerCamera.ScreenPointToRay(screenPoint), out var hit, 8,
                    ~0, QueryTriggerInteraction.Ignore) && hit.collider.TryGetComponent<ColaOrderTarget>(out var target) &&
                target.transform.IsChildOf(modelRoot.transform) &&
                (target.Kind == ColaOrderTarget.Action.Buy || target.Kind == ColaOrderTarget.Action.Inspect ||
                    target.Kind == ColaOrderTarget.Action.Remove))
            { ShowDescription(target.Item); return true; }
            descriptionText.text = "가격: 담기 / -: 수량 빼기\n아래 주문 확정으로 한 번에 결제";
            return false;
        }

        private void ShowDescription(ColaItemId id)
        {
            var definition = ColaItemCatalog.Get(id);
            descriptionText.text = Wrap(definition.Name + " / " + definition.Effect, 27);
        }

        public void RefreshAvailability()
        {
            if (!items || !modelRoot) return;
            bool visible = CanOpen;
            if (!visible)
            {
                // Hide the entire prop, including all hit targets; never leave an invisible input lock.
                IsOpen = returning = false;
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
            bool used = items.ShopUsed();
            bool canOrder = items.OrderAvailable();
            balanceText.text = "TOKENS " + items.match.PlayerTokens.ToString("0000") + "   ITEMS " + items.InventoryCount + "/6";
            statusText.text = used ? "ORDERED  0 / 1" : canOrder ? "ONE ORDER PER COLA" : "ORDERS CLOSED";
            statusText.color = canOrder ? new Color(.13f, .25f, .17f) : new Color(.50f, .13f, .09f);
            if (!IsOpen) descriptionText.text = "COLA_COLA PIZZERIA\n새 콜라가 오면 다시 주문";
            else if (string.IsNullOrEmpty(descriptionText.text) || descriptionText.text.StartsWith("COLA_COLA"))
                descriptionText.text = "가격: 담기 / -: 수량 빼기\n아래 주문 확정으로 한 번에 결제";
            closeTarget.enabled = IsOpen;
            checkoutTarget.enabled = IsVisible && IsOpen && IsSettled && items.CanPurchaseBatch(cart);
            checkoutText.text = "ORDER " + cart.Count + "  /  " + CartTotal + " T";
            checkoutRenderer.sharedMaterial = items.CanPurchaseBatch(cart) ? available : unavailable;
            for (int i = 0; i < purchaseTargets.Length; i++)
            {
                int menuRow = -1;
                for (int slot = 0; slot < items.BottleMenu.Count; slot++)
                    if (items.BottleMenu[slot] == (ColaItemId)i) { menuRow = slot; break; }
                bool offered = menuRow >= 0;
                itemRows[i].SetActive(offered);
                if (offered) itemRows[i].transform.localPosition = new Vector3(0, .103f - menuRow * .032f, 0);
                purchaseTargets[i].enabled = offered && IsVisible && IsOpen && IsSettled;
                nameTargets[i].enabled = offered && IsVisible && IsOpen && IsSettled;
                removeTargets[i].enabled = offered && IsVisible && IsOpen && IsSettled && CartQuantity((ColaItemId)i) > 0;
                purchaseRenderers[i].sharedMaterial = CanAdd((ColaItemId)i) ? available : unavailable;
                int quantity = CartQuantity((ColaItemId)i);
                priceTexts[i].text = ColaItemCatalog.Get((ColaItemId)i).Price + " T" + (quantity > 0 ? " x" + quantity : "");
            }
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
            if (!collider) Destroy(part.GetComponent<Collider>());
            return part;
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
            result.anchor = anchor;
            result.color = color;
            ColaWorldText.Bind(result);
            return result;
        }

        private void BuildModel()
        {
            modelRoot = new GameObject("Pizzeria_OrderClipboard_Runtime") { hideFlags = HideFlags.DontSave };
            modelRoot.transform.SetParent(items.transform.root, false);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Arial" }, 64);
            var board = Surface("Receipt_AgedBoard", new Color(.10f, .16f, .13f));
            var paper = Surface("Receipt_WarmPaper", new Color(.90f, .84f, .68f));
            var metal = Surface("Receipt_Clip", new Color(.24f, .26f, .22f));
            var rule = Surface("Receipt_PrintRules", new Color(.26f, .29f, .21f));
            available = Surface("Receipt_OrderKey", new Color(.17f, .30f, .22f));
            unavailable = Surface("Receipt_DisabledKey", new Color(.27f, .27f, .23f));
            MeshPart("BeveledClipboard", Plate(.318f, .392f, .018f, .014f), board, new Vector3(0, 0, .007f));
            MeshPart("ReceiptPaper", Plate(.294f, .356f, .006f, .0015f), paper, new Vector3(0, 0, -.005f));
            var fold = new Mesh { name = "ReceiptFoldedCorner", hideFlags = HideFlags.DontSave };
            fold.vertices = new[] { new Vector3(.147f, .178f, -.006f), new Vector3(.125f, .178f, -.013f), new Vector3(.147f, .156f, -.013f) };
            fold.triangles = new[] { 0, 1, 2, 2, 1, 0 };
            fold.RecalculateNormals();
            MeshPart("FoldedPaperCorner", fold, paper, Vector3.zero);
            Block("ClipBridge", new Vector3(0, .180f, -.013f), new Vector3(.085f, .018f, .012f), metal, false);
            Block("ClipTab", new Vector3(0, .192f, -.007f), new Vector3(.052f, .012f, .012f), metal, false);
            var box = modelRoot.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0, .006f);
            box.size = new Vector3(.318f, .392f, .020f);
            modelRoot.AddComponent<ColaOrderTarget>().Bind(ColaOrderTarget.Action.Open);
            openTarget = box;
            Color ink = new(.10f, .15f, .11f), keyInk = new(.84f, .82f, .68f);
            Text("ReceiptTitle", "PIZZERIA / ORDER", new Vector3(-.128f, .151f, -.009f), 64, .0025f, ink);
            balanceText = Text("OrderBalance", "", new Vector3(-.128f, .128f, -.009f), 48, .0016f, ink);
            var close = Block("ReturnClipboard", new Vector3(.131f, .165f, -.011f), new Vector3(.025f, .020f, .006f), board, true);
            close.AddComponent<ColaOrderTarget>().Bind(ColaOrderTarget.Action.Close);
            closeTarget = close.GetComponent<Collider>();
            Text("CloseMark", "X", new Vector3(.131f, .165f, -.015f), 48, .002f, keyInk, TextAnchor.MiddleCenter);
            for (int i = 0; i < ColaItemCatalog.All.Count; i++)
            {
                var definition = ColaItemCatalog.All[i];
                var row = new GameObject("ReceiptRow_" + (i + 1));
                row.transform.SetParent(modelRoot.transform, false);
                itemRows[i] = row;
                float y = 0;
                var itemName = Text("OrderItem_" + (i + 1), (i + 1).ToString("00") + "  " + definition.Name,
                    new Vector3(-.128f, y, -.009f), 64, .0022f, ink);
                itemName.transform.SetParent(row.transform, false);
                var nameHit = itemName.gameObject.AddComponent<BoxCollider>();
                nameHit.center = new Vector3(.097f, 0, 0);
                nameHit.size = new Vector3(.194f, .024f, .001f);
                itemName.gameObject.AddComponent<ColaOrderTarget>().Bind(ColaOrderTarget.Action.Inspect, definition.Id);
                nameTargets[i] = nameHit;
                var key = Block("OrderKey_" + (i + 1), new Vector3(.095f, y, -.011f), new Vector3(.037f, .024f, .006f), available, true);
                key.transform.SetParent(row.transform, false);
                key.AddComponent<ColaOrderTarget>().Bind(ColaOrderTarget.Action.Buy, definition.Id);
                purchaseTargets[i] = key.GetComponent<Collider>();
                purchaseRenderers[i] = key.GetComponent<Renderer>();
                priceTexts[i] = Text("OrderPrice_" + (i + 1), definition.Price + " T", new Vector3(.095f, y, -.015f), 48, .0015f,
                    keyInk, TextAnchor.MiddleCenter);
                priceTexts[i].transform.SetParent(row.transform, false);
                var minus = Block("RemoveKey_" + (i + 1), new Vector3(.127f, y, -.011f), new Vector3(.018f, .024f, .006f), unavailable, true);
                minus.transform.SetParent(row.transform, false);
                minus.AddComponent<ColaOrderTarget>().Bind(ColaOrderTarget.Action.Remove, definition.Id);
                removeTargets[i] = minus.GetComponent<Collider>();
                Text("RemoveMark_" + (i + 1), "-", new Vector3(.127f, y, -.015f), 48, .0018f, keyInk,
                    TextAnchor.MiddleCenter).transform.SetParent(row.transform, false);
                Block("PrintRule_" + (i + 1), new Vector3(0, y - .015f, -.006f), new Vector3(.26f, .0005f, .0002f), rule,
                    false).transform.SetParent(row.transform, false);
            }
            descriptionText = Text("ReceiptDescription", "", new Vector3(-.128f, -.102f, -.009f), 48, .0013f, ink);
            var checkout = Block("ConfirmOrder", new Vector3(0, -.145f, -.011f), new Vector3(.19f, .025f, .006f), available, true);
            checkout.AddComponent<ColaOrderTarget>().Bind(ColaOrderTarget.Action.Checkout);
            checkoutTarget = checkout.GetComponent<Collider>(); checkoutRenderer = checkout.GetComponent<Renderer>();
            checkoutText = Text("ConfirmOrderText", "", new Vector3(0, -.145f, -.015f), 48, .0018f, keyInk, TextAnchor.MiddleCenter);
            statusText = Text("OrderAllowanceStamp", "", new Vector3(0, -.171f, -.009f), 48, .0012f, ink, TextAnchor.MiddleCenter);
        }

        private static string Wrap(string text, int length)
        {
            for (int at = length; at < text.Length; at += length + 1) text = text.Insert(at, "\n");
            return text;
        }

        private static Mesh Plate(float width, float height, float corner, float depth)
        {
            var points = new[] { new Vector2(-width / 2 + corner, -height / 2), new Vector2(width / 2 - corner, -height / 2),
                new Vector2(width / 2, -height / 2 + corner), new Vector2(width / 2, height / 2 - corner),
                new Vector2(width / 2 - corner, height / 2), new Vector2(-width / 2 + corner, height / 2),
                new Vector2(-width / 2, height / 2 - corner), new Vector2(-width / 2, -height / 2 + corner) };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            { int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2); }
            for (int i = 0; i < 8; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % 8];
                Vector3 af = new(a.x, a.y, -depth / 2), bf = new(b.x, b.y, -depth / 2);
                Vector3 ab = new(a.x, a.y, depth / 2), bb = new(b.x, b.y, depth / 2);
                Triangle(new Vector3(0, 0, -depth / 2), bf, af);
                Triangle(new Vector3(0, 0, depth / 2), ab, bb);
                Triangle(af, bf, ab); Triangle(bf, bb, ab);
            }
            var mesh = new Mesh { name = "ReceiptBeveledPlate", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDisable() { RefreshAvailability(); }
        private void OnDestroy()
        {
            if (items) items.InventoryChanged -= RefreshAvailability;
            if (modelRoot) Destroy(modelRoot);
            foreach (var material in ownedMaterials) if (material) Destroy(material);
            foreach (var mesh in ownedMeshes) if (mesh) Destroy(mesh);
            if (font) Destroy(font);
        }
    }
}
