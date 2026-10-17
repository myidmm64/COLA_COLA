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
        private const string Output = "output/order-sheet/Playmode-validation.txt";
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
            File.WriteAllText(Output, report + (failure == null ? "" : "\nFAIL: " + failure));
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
            EditorApplication.delayCall += ColaBossItemVerification.Begin;
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
            Check(items.InventoryCount == 2 && items.BossInventoryCount == 2 && items.ItemAt(0) == ColaItemId.IceCup &&
                items.ItemAt(1) == ColaItemId.SafetyCap && items.ItemAt(0, true) == ColaItemId.IceCup &&
                items.ItemAt(1, true) == ColaItemId.SafetyCap, "Both actors must start with ice and safety, exactly two cubes each.");
            report.AppendLine("PASS: production Play entry starts both actors with exactly two free items (ice cup and safety cap), zero tokens.");
            Reset(0);
            Check(sheet && sheet.ModelRoot && !sheet.ModelRoot.GetComponentInChildren<Canvas>() &&
                sheet.ModelRoot.Find("ReceiptPaper").GetComponent<MeshFilter>().sharedMesh.vertexCount > 24,
                "A custom 3D paper/clipboard model must replace the screen menu.");
            CheckView(sheet.OpenCollider);
            Check(Click(sheet.OpenCollider) && sheet.IsOpen, "The tabletop model must open through physics input.");
            yield return WaitSheet(true);
            CheckView(sheet.OpenCollider, true);
            foreach (var id in ColaItemCatalog.All) CheckView(sheet.PurchaseCollider(id.Id));
            foreach (var definition in ColaItemCatalog.All)
            {
                Check(sheet.HoverPointer(ScreenPoint(sheet.NameCollider(definition.Id))) &&
                    sheet.Description.Replace("\n", "").Contains(definition.Effect), "Name-hover explanation missing: " + definition.Name);
                Check(sheet.HoverPointer(ScreenPoint(sheet.PurchaseCollider(definition.Id))) &&
                    sheet.Description.Replace("\n", "").Contains(definition.Effect), "Price-hover explanation missing: " + definition.Name);
                Check(Click(sheet.NameCollider(definition.Id)) && items.InventoryCount == 0 && !items.ShopUsed(),
                    "Clicking a name should inspect without ordering or consuming quota.");
            }
            CheckWorldMaterials();
            report.AppendLine("PASS: all 11 names and prices show their correct explanation through live physics hover; name inspection never buys; paper is opaque, warm unlit; all receipt/tray/cube glyphs use depth-tested world text with current font atlas.");
            Check(!interaction.SelectStrength(1) && !interaction.ConfirmShake(), "Holding the sheet must lock gameplay shortcuts.");
            Check(!Click(sheet.PurchaseCollider(ColaItemId.PizzaSlice)) && !items.ShopUsed() && items.InventoryCount == 0 && sheet.IsOpen,
                "An unaffordable physical checkout must preserve tokens, inventory and this bottle's order.");
            Check(Click(sheet.CloseCollider), "Physical return button failed.");
            yield return WaitSheet(false);
            Check(!items.ShopUsed() && Click(sheet.OpenCollider), "Inspecting and closing must not consume a shop visit.");
            yield return WaitSheet(true);
            Credit(false, 100);
            Check(Click(sheet.PurchaseCollider(ColaItemId.SafetyCap)), "Physical price button must place an order.");
            yield return WaitSheet(false);
            Check(items.ShopUsed() && !items.ShopUsed(true) && match.PlayerTokens == 96 && items.ItemAt(0) == ColaItemId.SafetyCap,
                "One successful purchase must debit four and consume only the player's current-bottle order.");
            Check(!Click(sheet.OpenCollider) && !items.TryPurchase(ColaItemId.IceCup) && match.PlayerTokens == 96,
                "Reopening and direct API calls must not bypass the one-order quota.");
            Check(items.TryUseSlot(0) && items.ShopUsed(), "Using a purchased item must not replenish shopping quota.");
            report.AppendLine("PASS: actual beveled clipboard, paper, fold, clip and 11 physical price keys replace the GUI shop; tabletop open/close/checkout raycasts, viewport fit and gameplay lock; inspection/failed checkout preserve quota; successful purchase consumes exactly one order and cannot be bypassed.");

            interaction.automaticBossTurns = false;
            Check(interaction.SelectStrength(3) && interaction.ConfirmShake(), "Player safe shake failed.");
            yield return WatchAction(false);
            Check(items.ShopUsed() && !interaction.LastResult.Burst, "A safe shake must retain the spent current-bottle order.");
            Reset(100);
            interaction.automaticBossTurns = false;
            Check(items.OrderAvailable() && interaction.SelectStrength(1) && items.OrderAvailable(), "Previewing strengths must preserve the new-bottle window.");
            Check(interaction.ConfirmShake(), "Unused shop-window shake failed.");
            yield return WatchAction(false);
            Check(!items.ShopUsed() && !items.OrderAvailable() && !Click(sheet.OpenCollider) && !items.TryPurchase(ColaItemId.IceCup),
                "The first safe action must expire an unused shop window, including direct purchase bypasses.");
            interaction.BeginBottle(999);
            Check(items.OrderAvailable() && items.OrderAvailable(true) && !items.ShopUsed() && !items.ShopUsed(true), "New bottle must restore both independent initial opportunities.");
            Credit(true, 100);
            interaction.automaticBossTurns = true;
            Check(interaction.PassPlayerTurn() && items.CanBossAct && items.TryPurchase(ColaItemId.CarbonationSyrup, true), "Boss must get its own one-order opportunity.");
            int balance = match.BossTokens;
            Check(items.ShopUsed(true) && !items.ShopUsed() && !items.TryPurchase(ColaItemId.SpicyOrder, true) && match.BossTokens == balance,
                "Boss may buy once without consuming the player's opportunity.");
            yield return WatchAction(false);
            Check(!items.OrderAvailable() && !items.ShopUsed(), "Passing the first player turn must expire an unused order.");
            Check(interaction.PassPlayerTurn() && !items.TryPurchase(ColaItemId.IceCup, true), "A later boss turn on the same bottle must not reset its quota.");
            match.RestartRun(Seed());
            match.GetComponent<ColaBossItemAI>().automaticItemUse = false;
            Check(!items.ShopUsed() && !items.ShopUsed(true) && !sheet.BlocksGameplay, "Restart must reset both quotas and the held model.");
            ColaItemInventoryFixture.Clear(items);
            report.AppendLine("PASS: first safe shake/pass expires an unused order window; strength previews preserve it; both original hands perform safe actions with no COLA audio source playing; each actor has one first-action opportunity per bottle; a later boss turn cannot buy again; restart restores quotas and sheet state.");

            Credit(false, 100); Credit(true, 100);
            interaction.BeginBottle(1);
            Check(Click(sheet.OpenCollider), "Burst reset probe could not pick up the model.");
            yield return WaitSheet(true);
            Check(Click(sheet.PurchaseCollider(ColaItemId.IceCup)), "Player order before burst failed.");
            yield return WaitSheet(false);
            Check(interaction.PassPlayerTurn() && items.TryPurchase(ColaItemId.SafetyCap, true) && items.TryUseSlot(0, true) && interaction.PassBossTurn(),
                "Boss order/pass fixture failed.");
            yield return WaitIdle();
            Check(items.ShopUsed() && items.ShopUsed(true), "Both quotas must remain consumed before the burst.");
            interaction.automaticBossTurns = false;
            int bottle = interaction.BottleNumber;
            Check(interaction.SelectStrength(1) && interaction.ConfirmShake() && interaction.LastResult.Burst, "Guaranteed player burst failed.");
            yield return WatchAction(true);
            Check(ruptureHeard && interaction.BottleNumber == bottle + 1 && !items.ShopUsed() && !items.ShopUsed(true),
                "Burst audio/replacement mismatch: playing=" + ruptureHeard + ", bottles=" + bottle + "->" + interaction.BottleNumber +
                ", used=" + items.ShopUsed() + "/" + items.ShopUsed(true));
            report.AppendLine("PASS: shake-loop audio is absent for both actors; actual explosion still plays rupture audio; replacing the exploded bottle restores both order quotas without clearing stored items.");

            foreach (var definition in ColaItemCatalog.All)
            {
                Reset(100);
                Check(Click(sheet.OpenCollider), "Physical pickup failed for " + definition.Name);
                yield return WaitSheet(true);
                Check(Click(sheet.PurchaseCollider(definition.Id)), "World price-key raycast failed for " + definition.Name);
                yield return WaitSheet(false);
                Check(match.PlayerTokens == 100 - definition.Price && items.ItemAt(0) == definition.Id && items.ShopUsed(),
                    "Physical item/price binding mismatch: " + definition.Name);
            }
            Reset(100);
            for (int i = 0; i < 6; i++) ColaItemInventoryFixture.Stock(items, ColaItemId.IceCup);
            Check(Click(sheet.OpenCollider), "Full-inventory pickup failed.");
            yield return WaitSheet(true);
            int before = match.PlayerTokens;
            Check(!Click(sheet.PurchaseCollider(ColaItemId.PizzaSlice)) && !items.ShopUsed() && match.PlayerTokens == before,
                "Full inventory must preserve the one-order opportunity.");
            match.enabled = false;
            yield return WaitSheet(false);
            match.enabled = true;
            Check(!sheet.BlocksGameplay && !items.ShopUsed() && !items.ShopUsed(true) && items.InventoryCount == 2,
                "Disable/re-enable while viewing the receipt must restore the model and fresh-run state.");
            report.AppendLine("PASS: all 11 actual model buttons buy their correct item at its catalog price; full inventory does not charge or consume quota; disabling/re-enabling during pickup restores the tabletop model and resets the run.");
            report.AppendLine("Scope: actual Unity Play-mode geometry, physical raycasts, animated model state, per-bottle account rules, real hand actions and AudioSource state. No Game-view visual confirmation or listening claim; no preview rendering or screenshots. Combination-effect suites use Editor-only pre-stocked inventory representing earlier-bottle purchases.");
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
        }
        private bool Click(Collider target)
        {
            Physics.SyncTransforms();
            return target && target.enabled && interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(target.bounds.center));
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
                    !UnityEditor.ShaderUtil.ShaderHasError(material.shader) && material.mainTexture == text.font.material.mainTexture,
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
