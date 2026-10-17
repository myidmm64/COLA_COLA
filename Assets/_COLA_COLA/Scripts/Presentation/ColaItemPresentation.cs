using ColaCola.Core;
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
        private Material trayMaterial;
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
            trayMaterial = MakeMaterial("ItemTray_Matte", new Color(.055f, .11f, .10f));
            for (int i = 0; i < materials.Length; i++)
                materials[i] = MakeMaterial("ItemCube_" + (i + 1), Color.HSVToRGB(i / 11f, .40f, .53f));
            inventoryRoot = BuildInventory(false);
            bossInventoryRoot = BuildInventory(true);
        }

        private GameObject BuildInventory(bool boss)
        {
            var root = new GameObject(boss ? "BossItemCubes_Runtime" : "PlayerItemCubes_Runtime") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(items.transform.root, false);
            root.transform.position = boss ? new Vector3(.62f, .913f, .64f) : new Vector3(.62f, .833f, .05f);
            if (boss)
            {
                var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pedestal.name = "BossTrayPedestal";
                pedestal.transform.SetParent(root.transform, false);
                pedestal.transform.localPosition = new Vector3(0, -.04f, 0);
                pedestal.transform.localScale = new Vector3(.59f, .08f, .40f);
                pedestal.GetComponent<MeshRenderer>().sharedMaterial = trayMaterial;
                Destroy(pedestal.GetComponent<Collider>());
            }
            var tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tray.name = "SixSlotTray";
            tray.transform.SetParent(root.transform, false);
            tray.transform.localPosition = new Vector3(0, .01f, 0);
            tray.transform.localScale = new Vector3(.59f, .02f, .40f);
            tray.GetComponent<MeshRenderer>().sharedMaterial = trayMaterial;
            Destroy(tray.GetComponent<Collider>());
            var rearStep = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rearStep.name = "RaisedRearRow";
            rearStep.transform.SetParent(root.transform, false);
            rearStep.transform.localPosition = new Vector3(0, .046f, .10f);
            rearStep.transform.localScale = new Vector3(.59f, .06f, .18f);
            rearStep.GetComponent<MeshRenderer>().sharedMaterial = trayMaterial;
            Destroy(rearStep.GetComponent<Collider>());
            var owned = boss ? bossCubes : cubes;
            for (int i = 0; i < owned.Length; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "ItemSlot_" + (i + 1);
                cube.transform.SetParent(root.transform, false);
                cube.transform.localPosition = new Vector3((i % 3 - 1) * .18f, i / 3 == 0 ? .073f : .133f, i / 3 == 0 ? -.10f : .10f);
                cube.transform.localScale = Vector3.one * .105f;
                cube.AddComponent<ColaItemCube>().Bind(i, boss);
                cube.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
                var number = new GameObject("ItemNumber");
                number.transform.SetParent(cube.transform, false);
                number.transform.localPosition = new Vector3(0, .12f, -.515f);
                var text = number.AddComponent<TextMesh>();
                text.font = cubeFont;
                text.fontSize = 40;
                text.characterSize = .12f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = Ink;
                ColaWorldText.Bind(text);
                owned[i] = cube;
                cube.SetActive(false);
            }
            var ownerLabel = new GameObject("TrayOwner");
            ownerLabel.transform.SetParent(root.transform, false);
            ownerLabel.transform.localPosition = new Vector3(0, .012f, -.205f);
            var ownerText = ownerLabel.AddComponent<TextMesh>();
            ownerText.text = boss ? "BOSS" : "YOU";
            ownerText.font = cubeFont;
            ownerText.fontSize = 40;
            ownerText.characterSize = .025f;
            ownerText.anchor = TextAnchor.MiddleCenter;
            ownerText.color = Ink;
            ColaWorldText.Bind(ownerText);
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
            if (trayMaterial) Destroy(trayMaterial);
            if (menuFont) Destroy(menuFont);
        }
    }
}
