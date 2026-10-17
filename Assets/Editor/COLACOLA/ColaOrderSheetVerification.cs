using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using ColaCola.Core;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColaCola.Editor
{
    [InitializeOnLoad]
    public static class ColaOrderSheetVerification
    {
        private const string Key = "COLA_COLA.OrderSheetVerification";
        private const string Output = "output/verification/latest/order-sheet/Playmode-validation.txt";
        static ColaOrderSheetVerification() { if (SessionState.GetBool(Key, false)) Hook(); }
        public static void Begin()
        {
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".Started", false);
            SessionState.SetBool(Key + ".Passed", false);
            Hook();
            EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        private static void Hook()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || Time.frameCount < 4 || SessionState.GetBool(Key + ".Started", false)) return;
            SessionState.SetBool(Key + ".Started", true);
            new GameObject("OrderSheetPlayProbe").AddComponent<ColaOrderSheetPlayProbe>().Begin();
        }
        internal static void Complete(string report, Exception failure = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            ProjectVerificationBridge.WriteReport(Output, report + (failure == null ? "" : "\nFAIL: " + failure));
            SessionState.SetBool(Key + ".Passed", failure == null);
            if (failure == null) Debug.Log("COLA_ORDER_SHEET_PLAYMODE_PASS\n" + report);
            else Debug.LogException(failure);
            EditorApplication.isPlaying = false;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Changed;
            if (!SessionState.GetBool(Key + ".Passed", false))
            { if (Application.isBatchMode) EditorApplication.Exit(2); return; }
            ProjectVerificationBridge.Schedule(ColaBossItemVerification.Begin);
        }
    }

    public sealed class ColaOrderSheetPlayProbe : MonoBehaviour
    {
        private ColaDuelController match;
        private ShakeSelectionInteraction interaction;
        private ColaItemController items;
        private ColaOrderSheet sheet;
        private readonly StringBuilder report = new();
        private bool ruptureHeard;
        public void Begin() => StartCoroutine(Guarded(Run()));
        private IEnumerator Guarded(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                bool more; object current = null;
                try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
                catch (Exception exception) { ColaOrderSheetVerification.Complete(report.ToString(), exception); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested);
                else yield return current;
            }
            ColaOrderSheetVerification.Complete(report.ToString());
        }

        private IEnumerator Run()
        {
            Check(Application.isPlaying, "Use actual Play mode.");
            match = FindAnyObjectByType<ColaDuelController>();
            interaction = match.interaction; items = match.Items; sheet = match.GetComponent<ColaOrderSheet>();
            Check(items.InventoryCount == 2 && items.BossInventoryCount == 2, "Keep two starter items each.");
            Reset(0); yield return null; CheckMenu();
            Check(!sheet.IsVisible && !sheet.Toggle() && !sheet.BlocksGameplay, "Hide unaffordable menu.");
            Credit(false, 100);
            string menu = MenuSignature();
            Check(Click(sheet.OpenCollider), "Physical pickup failed."); yield return WaitSheet(true);
            CheckView(sheet.OpenCollider, true);
            foreach (var id in items.BottleMenu)
            {
                var effect = ColaItemCatalog.Get(id).Effect;
                Check(sheet.HoverPointer(ScreenPoint(sheet.NameCollider(id))) && sheet.Description.Replace("\n", "").Contains(effect), "Name explanation failed.");
                Check(sheet.HoverPointer(ScreenPoint(sheet.PurchaseCollider(id))) && sheet.Description.Replace("\n", "").Contains(effect), "Price explanation failed.");
                Check(Click(sheet.NameCollider(id)) && sheet.CartCount == 0, "Name must only inspect.");
            }
            CheckWorldMaterials();
            var first = items.BottleMenu[0]; var second = items.BottleMenu[1];
            int pairPrice = ColaItemCatalog.Get(first).Price + ColaItemCatalog.Get(second).Price;
            Check(!Click(sheet.CheckoutCollider), "Empty cart cannot order.");
            Check(Click(sheet.PurchaseCollider(first)) && Click(sheet.PurchaseCollider(first)) && Click(sheet.PurchaseCollider(second)), "Cart quantity addition failed.");
            CheckView(sheet.CheckoutCollider);
            Check(sheet.CartCount == 3 && sheet.CartQuantity(first) == 2 && match.PlayerTokens == 100 && items.InventoryCount == 0 && !items.ShopUsed(), "Adding must reserve neither tokens nor the order.");
            Check(Click(sheet.RemoveCollider(first)) && sheet.CartCount == 2 && sheet.CartQuantity(first) == 1 && sheet.CartTotal == pairPrice, "Physical minus key failed.");
            Check(Click(sheet.CloseCollider), "Cancel failed."); yield return WaitSheet(false);
            Check(sheet.CartCount == 0 && match.PlayerTokens == 100 && !items.ShopUsed() && MenuSignature() == menu, "Cancel must clear the cart without consuming order or rerolling.");
            Check(Click(sheet.OpenCollider), "Reopen failed."); yield return WaitSheet(true);
            Check(!interaction.SelectStrength(1) && !interaction.ConfirmShake(), "Held paper must lock cola input.");
            Check(Click(sheet.CloseCollider), "Second cancel failed."); yield return WaitSheet(false);
            report.AppendLine("PASS: six stable random offers; live name/price explanations; price keys add quantities, physical minus removes one, empty checkout rejects; cancel clears cart without any payment/order consumption or menu reroll; held paper blocks cola input.");

            interaction.automaticBossTurns = false; interaction.BeginBottle(999); menu = MenuSignature();
            int bottle = interaction.BottleNumber;
            for (int action = 0; action < 2; action++)
            { Check(interaction.SelectStrength(1) && interaction.ConfirmShake(), "Safe action failed."); yield return WatchAction(false); }
            Check(interaction.PassPlayerTurn(), "Voluntary pass failed."); yield return WaitIdle();
            Check(items.OrderAvailable() && !items.ShopUsed() && sheet.IsVisible && interaction.BottleNumber == bottle && MenuSignature() == menu,
                "Safe actions and passing must preserve the unused same-bottle order.");
            first = items.BottleMenu[0]; second = items.BottleMenu[1];
            pairPrice = ColaItemCatalog.Get(first).Price + ColaItemCatalog.Get(second).Price;
            int before = match.PlayerTokens;
            Check(Click(sheet.OpenCollider), "Late ordering pickup failed."); yield return WaitSheet(true);
            Check(Click(sheet.PurchaseCollider(first)) && Click(sheet.PurchaseCollider(second)) && Click(sheet.CheckoutCollider), "Late multi-item checkout failed.");
            yield return WaitSheet(false);
            Check(items.InventoryCount == 2 && items.ItemAt(0) == first && items.ItemAt(1) == second &&
                match.PlayerTokens == before - pairPrice && items.ShopUsed() && !sheet.IsVisible && !sheet.BlocksGameplay,
                "One checkout must charge exact sum once and deliver both items.");
            Check(!items.TryPurchase(first) && !items.TryPurchaseBatch(new[] { first, second }) && !sheet.Toggle(), "A completed order cannot be repeated.");
            Check(interaction.SelectStrength(1) && interaction.ConfirmShake(), "Post-order action failed."); yield return WatchAction(false);
            Check(items.ShopUsed() && !sheet.IsVisible, "Further turns cannot replenish a completed order.");
            report.AppendLine("PASS: the player can wait for later turns on the same bottle, then purchase multiple items in one atomic checkout; exact total debited once; subsequent actions cannot restore a completed order.");

            Reset(100); first = items.BottleMenu[0];
            ColaItemId omitted = default;
            foreach (var definition in ColaItemCatalog.All) if (!items.IsOnMenu(definition.Id)) { omitted = definition.Id; break; }
            Check(!items.TryPurchaseBatch(new[] { first, omitted }) && !items.TryPurchaseBatch(new[] { (ColaItemId)999 }) &&
                !items.TryPurchaseBatch(null) && !items.TryPurchaseBatch(new ColaItemId[0]) &&
                match.PlayerTokens == 100 && items.InventoryCount == 0 && !items.ShopUsed(), "Invalid batches must be atomic and leave the opportunity unused.");
            first = items.BottleMenu[0]; second = items.BottleMenu[1]; pairPrice = ColaItemCatalog.Get(first).Price + ColaItemCatalog.Get(second).Price;
            Check(Click(sheet.OpenCollider), "Budget pickup failed."); yield return WaitSheet(true);
            Check(Click(sheet.PurchaseCollider(first)) && Click(sheet.PurchaseCollider(second)), "Budget cart setup failed.");
            Credit(false, pairPrice - 1);
            Check(!Click(sheet.CheckoutCollider) && !items.TryPurchaseBatch(new[] { first, second }) &&
                items.InventoryCount == 0 && !items.ShopUsed() && sheet.CartCount == 2, "Insufficient full-cart budget must never partially charge or deliver.");
            Check(Click(sheet.RemoveCollider(second)) && Click(sheet.CheckoutCollider), "Reducing cart to affordable total failed.");
            yield return WaitSheet(false);
            Check(match.PlayerTokens == ColaItemCatalog.Get(second).Price - 1 && items.InventoryCount == 1, "Reduced-cart debit mismatch.");

            Reset(100); first = items.BottleMenu[0];
            Check(Click(sheet.OpenCollider), "Capacity pickup failed."); yield return WaitSheet(true);
            for (int i = 0; i < 6; i++) Check(Click(sheet.PurchaseCollider(first)), "Same-item quantity addition failed.");
            Check(!Click(sheet.PurchaseCollider(first)) && sheet.CartCount == 6 && match.PlayerTokens == 100, "Cart must reject a seventh item without charging.");
            int sixCost = 6 * ColaItemCatalog.Get(first).Price;
            Check(Click(sheet.CheckoutCollider), "Six-item checkout failed."); yield return WaitSheet(false);
            Check(items.InventoryCount == 6 && match.PlayerTokens == 100 - sixCost, "Six-item batch did not fill exactly six slots.");
            interaction.BeginBottle(999, 12);
            Check(!sheet.IsVisible && items.OrderAvailable(), "Full inventory must hide the menu without spending the fresh order.");
            report.AppendLine("PASS: invalid/off-menu/null/empty batches and insufficient total budget never partially charge, deliver or consume the order; minus edits repair an unaffordable cart; repeated item quantities are supported; six-slot capacity enforced before payment.");

            Reset(100); Credit(true, 100); interaction.BeginBottle(999);
            first = items.BottleMenu[0]; second = items.BottleMenu[1];
            pairPrice = ColaItemCatalog.Get(first).Price + ColaItemCatalog.Get(second).Price;
            Check(interaction.PassPlayerTurn() && items.CanBossAct, "Boss order phase failed.");
            Check(!sheet.IsVisible && items.TryPurchaseBatch(new[] { first, second }, true) &&
                items.BossInventoryCount == 2 && items.LastBossOrderCount == 2 && items.LastBossOrderCost == pairPrice &&
                items.BossPurchaseCount == 1 && match.BossTokens == 100 - pairPrice && match.PlayerTokens == 100 && interaction.PassBossTurn(),
                "Boss batch must have an independent single order and account.");
            yield return WaitIdle();
            Check(items.OrderAvailable() && items.ShopUsed(true) && sheet.IsVisible, "Passing through the boss turn must keep the player's unused order available.");
            report.AppendLine("PASS: boss supports multiple items in one checkout, exactly one order counter and exact aggregate spending; accounts/opportunities remain independent; player's menu returns on its later turn.");

            Reset(100); Credit(true, 100);
            for (int slot = 0; slot < 6; slot++)
            { ColaItemInventoryFixture.Stock(items, ColaItemId.IceCup); ColaItemInventoryFixture.Stock(items, ColaItemId.IceCup, true); }
            CheckStorageModels();
            interaction.BeginBottle(999, 12); Check(items.TryUseSlot(0), "Free-slot test failed.");
            yield return null;
            Check(sheet.IsVisible, "A free slot must restore an unused order later on the same bottle.");
            Check(Click(sheet.OpenCollider), "Invalidation pickup failed."); yield return WaitSheet(true);
            Check(Click(sheet.PurchaseCollider(items.BottleMenu[0])), "Invalidation cart setup failed.");
            Credit(false, 0); yield return null;
            Check(!sheet.IsVisible && !sheet.BlocksGameplay && sheet.CartCount == 0 && items.OrderAvailable(), "An unavailable held menu must cancel unpaid cart and release input.");
            Credit(false, 100); match.enabled = false; yield return null;
            Check(!sheet.IsVisible && !sheet.BlocksGameplay, "Disabled match must hide the menu.");
            match.enabled = true; yield return null;
            Check(items.InventoryCount == 2 && items.BossInventoryCount == 2 && !sheet.IsVisible, "Restart must restore starters and clear the zero-token menu.");
            report.AppendLine("PASS: custom teal serving tray with front U-handle and red delivery crate with metal handles/pizza emblem replace owner text; both retain six lined compartments/raised rear shelves; all 12 actual cube centers are visible and unobstructed by solid storage colliders; invalidation cancels unpaid cart without input lock.");

            Reset(100); var variants = new HashSet<string>();
            for (int draw = 0; draw < 32; draw++) { interaction.BeginBottle(999); CheckMenu(); variants.Add(MenuSignature()); }
            Check(variants.Count > 1, "New bottles must vary offers.");
            var random = new System.Random(Seed()); random.Next(interaction.settings.minimumBottleLimit, interaction.settings.maximumBottleLimit + 1);
            double roll = random.NextDouble(); interaction.automaticBossTurns = false;
            Check(interaction.SelectStrength(1) && interaction.ConfirmShake() && interaction.LastResult.Roll == roll, "Menu draws must not advance explosion RNG.");
            yield return WatchAction(false);
            foreach (var definition in ColaItemCatalog.All)
            {
                Reset(100); ColaItemInventoryFixture.EnsureMenu(items, definition.Id); sheet.RefreshAvailability();
                Check(Click(sheet.OpenCollider), "Catalog pickup failed."); yield return WaitSheet(true);
                Check(Click(sheet.PurchaseCollider(definition.Id)) && Click(sheet.CheckoutCollider), "Actual catalog checkout failed: " + definition.Name);
                yield return WaitSheet(false);
                Check(items.ItemAt(0) == definition.Id && match.PlayerTokens == 100 - definition.Price, "Catalog binding mismatch.");
            }
            Reset(100); interaction.automaticBossTurns = false; interaction.BeginBottle(1);
            first = items.BottleMenu[0]; second = items.BottleMenu[1];
            Check(Click(sheet.OpenCollider), "Burst setup pickup failed."); yield return WaitSheet(true);
            Check(Click(sheet.PurchaseCollider(first)) && Click(sheet.PurchaseCollider(second)) && Click(sheet.CheckoutCollider), "Burst setup checkout failed.");
            yield return WaitSheet(false); bottle = interaction.BottleNumber;
            Check(interaction.SelectStrength(1) && interaction.ConfirmShake() && interaction.LastResult.Burst, "Guaranteed burst failed."); yield return WatchAction(true);
            Check(ruptureHeard && interaction.BottleNumber == bottle + 1 && items.OrderAvailable() && sheet.IsVisible &&
                items.InventoryCount == 2 && sheet.CartCount == 0, "Actual replacement must refresh order/menu and preserve both stored items.");
            CheckMenu();
            report.AppendLine("PASS: 32 distinct-subset checks and independent outcome RNG; all 11 catalog item/price bindings via real cart/checkout rays; actual burst refreshes order and six offers, preserves inventory and rupture sound; no shake-loop audio.");
            report.AppendLine("Scope: actual Unity Play-mode state, procedural storage meshes/materials/positions, viewport and solid-collider occlusion rays, real menu/cart input, payment/effects and hand/audio state. No Game-view visual confirmation or preview capture.");
        }

        private void CheckStorageModels()
        {
            var display = items.GetComponent<ColaItemPresentation>();
            for (int actor = 0; actor < 2; actor++)
            {
                bool boss = actor == 1; var root = display.StorageRoot(boss);
                var shell = root.Find(boss ? "DeliveryCrateShell" : "ServingTrayShell");
                Check(shell && shell.GetComponent<MeshFilter>().sharedMesh.vertexCount > 24 &&
                    root.Find(boss ? "PizzaSliceEmblem" : "ServingTrayHandle"), "Distinct storage geometry is missing.");
                var color = shell.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
                Check(boss ? color.r > color.g : color.g > color.r, "Owner storage palette mismatch.");
                foreach (var text in root.GetComponentsInChildren<TextMesh>(true))
                    Check(text.text != "BOSS" && text.text != "YOU", "Owner lettering must be replaced by the model.");
                for (int slot = 0; slot < 6; slot++)
                {
                    Check(root.Find("CompartmentLiner_" + slot), "Six physical compartments must remain.");
                    var cube = display.CubeCollider(slot, boss); Physics.SyncTransforms(); CheckView(cube);
                    Check(Physics.Raycast(interaction.playerCamera.ScreenPointToRay(ScreenPoint(cube)), out var hit, 8,
                        ~0, QueryTriggerInteraction.Ignore) && hit.collider == cube,
                        "Storage occludes slot " + boss + "/" + slot + ": " + (hit.collider ? hit.collider.name : "none"));
                }
            }
        }
        private string MenuSignature() => string.Join(",", items.BottleMenu);
        private void CheckMenu()
        {
            var unique = new HashSet<ColaItemId>(items.BottleMenu);
            Check(items.BottleMenu.Count == 6 && unique.Count == 6, "A bottle must offer exactly six distinct items.");
            int visibleNames = 0;
            foreach (var definition in ColaItemCatalog.All)
            {
                bool onMenu = unique.Contains(definition.Id);
                var name = sheet.NameCollider(definition.Id);
                var key = sheet.PurchaseCollider(definition.Id);
                Check(name.gameObject.activeInHierarchy == (sheet.IsVisible && onMenu) &&
                    key.gameObject.activeInHierarchy == (sheet.IsVisible && onMenu), "Rendered rows disagree with available offers.");
                if (name.gameObject.activeInHierarchy) visibleNames++;
                if (!onMenu) Check(!name.enabled && !key.enabled, "Omitted rows must not have enabled input targets.");
            }
            Check(visibleNames == (sheet.IsVisible ? 6 : 0), "Unexpected visible row count: " + visibleNames);
        }
        private void Reset(int tokens)
        {
            match.RestartRun(Seed());
            match.GetComponent<ColaBossItemAI>().automaticItemUse = false;
            ColaItemInventoryFixture.Clear(items);
            Credit(false, tokens); Credit(true, 0);
        }
        private int Seed()
        {
            for (int seed = 0; seed < 100000; seed++)
            {
                var random = new System.Random(seed);
                random.Next(interaction.settings.minimumBottleLimit, interaction.settings.maximumBottleLimit + 1);
                if (random.NextDouble() > .9 && random.NextDouble() > .9) return seed;
            }
            throw new InvalidOperationException("No safe seed.");
        }
        private void Credit(bool boss, int amount)
        {
            typeof(ColaDuelController).GetField(boss ? "<BossTokens>k__BackingField" : "<PlayerTokens>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(match, amount);
            match.terminal.SetPlayerState(match.PlayerHealth, match.playerMaxHealth, match.PlayerTokens, false);
            match.terminal.SetBossState(match.BossHealth, match.BossMaxHealth, match.BossTokens, false);
            sheet.RefreshAvailability();
        }
        private bool Click(Collider target)
        {
            Physics.SyncTransforms();
            return target && target.enabled && target.gameObject.activeInHierarchy &&
                interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(target.bounds.center));
        }
        private Vector2 ScreenPoint(Collider target)
        {
            Physics.SyncTransforms();
            return interaction.playerCamera.WorldToScreenPoint(target.bounds.center);
        }
        private void CheckWorldMaterials()
        {
            var paper = sheet.ModelRoot.Find("ReceiptPaper").GetComponent<Renderer>().sharedMaterial;
            Check(paper.shader.name == "Universal Render Pipeline/Unlit" && paper.renderQueue < 2500 &&
                paper.GetColor("_BaseColor").grayscale > .8f, "Paper must be bright opaque unlit, with no HDR bloom emission.");
            int labels = 0;
            foreach (var glyph in FindObjectsByType<ColaWorldText>(FindObjectsInactive.Include))
            {
                var text = glyph.GetComponent<TextMesh>();
                var material = glyph.GetComponent<Renderer>().sharedMaterial;
                Check(material.shader.name == "COLA_COLA/WorldText" && material.GetFloat("_ZTest") == 4 &&
                    !UnityEditor.ShaderUtil.ShaderHasError(material.shader) &&
                    (!glyph.gameObject.activeInHierarchy || material.mainTexture == text.font.material.mainTexture),
                    "World glyph must respect opaque depth and current dynamic atlas: " + glyph.name);
                labels++;
            }
            Check(labels >= 30, "World lettering bindings missing.");
        }
        private IEnumerator WaitSheet(bool open)
        {
            float deadline = Time.unscaledTime + 4;
            while (!sheet.IsSettled || sheet.IsOpen != open || (!open && sheet.BlocksGameplay))
            { Check(Time.unscaledTime < deadline, "Order model transition timed out."); yield return null; }
            yield return null;
        }
        private IEnumerator WaitIdle()
        {
            float deadline = Time.unscaledTime + 20;
            while (interaction.IsBusy) { Check(Time.unscaledTime < deadline, "Turn timeout."); yield return null; }
        }
        private IEnumerator WatchAction(bool burst)
        {
            float deadline = Time.unscaledTime + 20;
            while (interaction.IsBusy)
            {
                foreach (var audio in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
                {
                    bool colaSource = audio.name == "Fluid and PET strain" || audio.name == "Rupture and pressure tail" || audio.name == "Cap table impact";
                    if (!colaSource) continue;
                    Check(!audio.clip || audio.clip.name != "COLA_Shake", "Removed shake audio was generated or bound.");
                    if (!burst) Check(!audio.isPlaying, "A safe shake must not play cola sound: " + audio.name);
                    // PlayOneShot intentionally does not populate AudioSource.clip.
                    if (burst && audio.name == "Rupture and pressure tail" && audio.isPlaying) ruptureHeard = true;
                }
                Check(Time.unscaledTime < deadline, "Audio/action probe timed out.");
                yield return null;
            }
        }
        private void CheckView(Collider collider, bool allCorners = false)
        {
            var bounds = collider.bounds;
            int count = allCorners ? 8 : 1;
            for (int i = 0; i < count; i++)
            {
                Vector3 world = allCorners ? bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)) : bounds.center;
                Vector3 point = interaction.playerCamera.WorldToViewportPoint(world);
                Check(point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1, "Order model/button outside live viewport: " + collider.name + "/" + point);
            }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
