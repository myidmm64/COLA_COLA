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
    public static class ColaItemVerification
    {
        private const string Key = "COLA_COLA.ItemVerification";
        internal const string Output = "output/verification/latest/cola-items/Playmode-validation.txt";
        static ColaItemVerification() { if (SessionState.GetBool(Key, false)) Hook(); }
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
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || Time.frameCount < 4 || SessionState.GetBool(Key + ".Started", false)) return;
            SessionState.SetBool(Key + ".Started", true);
            new GameObject("ItemPlayProbe").AddComponent<ColaItemPlayProbe>().Begin();
        }
        internal static void Complete(string report, Exception failure = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            ProjectVerificationBridge.WriteReport(Output, report + (failure == null ? "" : "\nFAIL: " + failure));
            SessionState.SetBool(Key + ".Passed", failure == null);
            if (failure == null) Debug.Log("COLA_ITEMS_PLAYMODE_PASS\n" + report);
            else Debug.LogException(failure);
            EditorApplication.isPlaying = false;
        }
        private static void Changed(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= Changed;
            if (!SessionState.GetBool(Key + ".Passed", false))
            { if (Application.isBatchMode) EditorApplication.Exit(2); return; }
            // Existing full duel/rig regression also runs in Play, then checks saved-scene bindings in another Play entry.
            ProjectVerificationBridge.Schedule(PlayableDuelVerification.Begin);
        }
    }

    public sealed class ColaItemPlayProbe : MonoBehaviour
    {
        private ColaDuelController match;
        private ShakeSelectionInteraction interaction;
        private ColaItemController items;
        private ColaItemPresentation display;
        private readonly StringBuilder report = new();
        private float playerTravel, bossTravel;
        private Vector3 playerRest, bossRest;
        public void Begin() => StartCoroutine(Guarded(Run()));
        private IEnumerator Guarded(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                object current = null;
                bool more;
                try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
                catch (Exception exception) { ColaItemVerification.Complete(report.ToString(), exception); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested);
                else yield return current;
            }
            ColaItemVerification.Complete(report.ToString());
        }

        private IEnumerator Run()
        {
            Check(Application.isPlaying, "Checks must execute in real Play mode.");
            match = FindFirstObjectByType<ColaDuelController>();
            // Isolate player-item assertions from the separately verified autonomous boss policy.
            match.GetComponent<ColaBossItemAI>().automaticItemUse = false;
            interaction = match.interaction;
            items = match.Items;
            display = items.GetComponent<ColaItemPresentation>();
            Check(items && display && items.InventoryCount == 2 && items.BossInventoryCount == 2 &&
                items.ItemAt(0) == ColaItemId.IceCup && items.ItemAt(1) == ColaItemId.SafetyCap &&
                match.PlayerTokens == 0 && items.CanAct, "Fresh run must start with two items each and zero currency.");
            ColaItemInventoryFixture.Clear(items);
            Check(ColaItemCatalog.All.Count == 11, "Adopted catalog must contain exactly 11 items.");
            int[] prices = { 3, 7, 4, 3, 5, 3, 4, 8, 3, 4, 1 };
            for (int i = 0; i < prices.Length; i++) Check(ColaItemCatalog.All[i].Price == prices[i], "Notion price mismatch.");
            Check(!items.TryPurchase(ColaItemId.IceCup) && items.InventoryCount == 0 && match.PlayerTokens == 0, "Unaffordable order must be atomic.");
            playerRest = interaction.shakeAnimator.playerHand.position;
            bossRest = interaction.shakeAnimator.bossHand.position;
            match.RestartRun(FindSeed(0));
            interaction.BeginBottle(999);
            ClickShake(3);
            Check(!items.TryPurchase(ColaItemId.TipEnvelope), "Busy action must reject ordering.");
            yield return WaitIdle();
            Check(match.PlayerTokens == 3 && match.BossTokens >= 1 && match.BossTokens <= 3 && interaction.CommittedActionCount == 2,
                "Ordinary alternating actions must still earn selected-strength rewards.");
            Check(playerTravel > .05f && bossTravel > .05f, "Both original hands must move during actual gameplay.");
            report.AppendLine("PASS: two starter items each (ice/safety), zero starting tokens; all 11 Notion prices; unaffordable/busy purchase rejection; normal alternating original-hand shakes still award strength tokens.");

            Fixture();
            interaction.BeginBottle(30, 25);
            interaction.SelectStrength(2);
            for (int i = 0; i < 6; i++) Stock(ColaItemId.IceCup);
            int tokens = match.PlayerTokens;
            Check(!items.TryPurchase(ColaItemId.PizzaSlice) && match.PlayerTokens == tokens && items.InventoryCount == 6, "Full inventory must not charge tokens.");
            Check(!display.ToggleOrders() && !display.OrderOpen, "Full inventory must hide and reject the menu.");
            for (int i = 0; i < 6; i++) ClickCube(i);
            Check(display.ToggleOrders() && display.OrderOpen, "Free inventory must allow the fresh-bottle menu.");
            var firstKey = interaction.strengthButtons[0].GetComponent<Collider>();
            Check(!interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(firstKey.bounds.center)), "Open physical order slip must swallow gameplay clicks.");
            display.ToggleOrders();
            Check(interaction.Model.Pressure == 7 && interaction.Model.SelectedStrength == 2 && items.InventoryCount == 0 &&
                interaction.CommittedActionCount == 0 && !interaction.IsBusy && match.PlayerTokens == tokens, "Six physical ice cubes must reduce pressure without resolving a turn or rewarding tokens.");
            interaction.Model.AdjustPressure(-999);
            int ice = Stock(ColaItemId.IceCup);
            Check(!items.TryUseSlot(ice) && items.ItemAt(ice).HasValue && interaction.Model.Pressure == 0, "Unusable ice must not be consumed or go negative.");
            report.AppendLine("PASS: six live Cube slots are in the camera viewport and individually reachable by physics ray; menu blocks world clicks; full inventory is charge-free; ice keeps selection/turn and clamps at zero.");

            Fixture();
            interaction.BeginBottle(20, 8);
            Use(ColaItemId.SafetyCap);
            int duplicate = Stock(ColaItemId.SafetyCap);
            Check(!items.TryUseSlot(duplicate) && items.ItemAt(duplicate).HasValue, "Pending effects must not stack or consume duplicates.");
            Use(ColaItemId.DoubleStamp);
            Use(ColaItemId.TipEnvelope);
            interaction.SelectStrength(3);
            double expected = Math.Pow(11d / 20, interaction.settings.probabilityExponent) - .3d;
            yield return new WaitForSecondsRealtime(.5f);
            Check(Math.Abs(interaction.gauge.DisplayedProbability - expected) < .00001 && interaction.gauge.PredictedPressure == 11,
                "Physical forecast must include the safety modifier.");
            tokens = match.PlayerTokens;
            Check(interaction.ConfirmShake() && Math.Abs(interaction.LastResult.Prediction.BurstProbability - expected) < .00001,
                "Commit and forecast must use identical modified probability.");
            yield return WaitIdle();
            Check(!interaction.LastResult.Burst && match.PlayerTokens == tokens + 6 && !items.SafetyReady && !items.DoubleReady && !items.TipReady,
                "Low final-risk safe shake must double strength reward, give no tip, and consume each one-action player buff once.");
            report.AppendLine("PASS: safety -30 percentage points updates animated gauge and actual roll equally; duplicate armed effects retain their cube; low final risk gives no tip; stamp/insurance/tip consume on next own shake.");

            Fixture();
            interaction.BeginBottle(1);
            Use(ColaItemId.SafetyCap);
            Use(ColaItemId.DoubleStamp);
            Use(ColaItemId.HotSauce);
            tokens = match.PlayerTokens;
            ClickShake(3);
            Check(interaction.LastResult.Prediction.BurstProbability == 1 && interaction.LastResult.Burst, "Pressure limit must override the safety cap.");
            yield return WaitIdle();
            Check(match.PlayerHealth == 1 && match.PlayerTokens == tokens + 10 && !items.HotSauceReady && interaction.Model.Pressure == 0,
                "Hot sauce must inflict two self-damage and stamp must award ten total burst tokens, even after bottle replacement.");
            Use(ColaItemId.PizzaSlice);
            Check(match.PlayerHealth == 2, "Pizza did not heal one HP.");
            Use(ColaItemId.PizzaSlice);
            int pizza = Stock(ColaItemId.PizzaSlice);
            Check(match.PlayerHealth == 3 && !items.TryUseSlot(pizza) && items.ItemAt(pizza).HasValue, "Full-health pizza must remain in inventory.");
            CheckTerminal();
            report.AppendLine("PASS: 100% limit remains fatal with safety; hot sauce adds damage exactly once to the actual burst actor; stamp includes burst 5→10; new bottle removes hot sauce; pizza heals one and retains at maximum HP.");

            Fixture(FindSeed(.70));
            interaction.BeginBottle(20, 14);
            Use(ColaItemId.TipEnvelope);
            Use(ColaItemId.DoubleStamp);
            tokens = match.PlayerTokens;
            ClickShake(1);
            yield return WaitIdle();
            Check(!interaction.LastResult.Burst && interaction.LastResult.Prediction.BurstProbability >= .5 && match.PlayerTokens == tokens + 8,
                "A high-risk safe strength-1 result must give (1+3)*2 tokens with both effects.");
            report.AppendLine("PASS: tip checks actual final probability >=50% and safe outcome; double stamp doubles the complete one-action reward including the tip.");

            Fixture();
            interaction.automaticBossTurns = true;
            interaction.BeginBottle(999);
            Use(ColaItemId.SafetyCap);
            Use(ColaItemId.DoubleStamp);
            Use(ColaItemId.TipEnvelope);
            Use(ColaItemId.CarbonationSyrup);
            Use(ColaItemId.SpicyOrder);
            int bill = Stock(ColaItemId.Bill);
            tokens = match.PlayerTokens;
            ClickCube(bill);
            Check(interaction.IsBossTurn && interaction.IsBusy && interaction.CommittedActionCount == 0 && !items.CanAct &&
                !items.TryPurchase(ColaItemId.IceCup), "Bill must pass without any player action or allow boss-turn ordering.");
            yield return WaitIdle();
            Check(interaction.LastActionWasBoss && interaction.LastResult.Prediction.Strength == 3 && interaction.Model.Pressure == 5 &&
                interaction.LastResult.Prediction.PressureIncrease == 5 && match.BossTokens == 3 && match.PlayerTokens == tokens,
                "Spicy+syrup must make boss select 3, add pressure 5, but earn only selected-strength 3 tokens.");
            Check(!items.SyrupReady && !items.ForceThreeReady && items.SafetyReady && items.DoubleReady && items.TipReady,
                "Boss-only buffs must consume once; passing and boss action must retain own-shake buffs.");
            Use(ColaItemId.QueueTicket);
            tokens = match.PlayerTokens;
            ClickShake(1);
            yield return WaitIdle();
            Check(interaction.CommittedActionCount == 2 && match.PlayerTokens == tokens + 2 && !items.SkipBossReady && !interaction.IsBossTurn,
                "Queue ticket must skip exactly one boss action with no boss reward.");
            ClickShake(1);
            yield return WaitIdle();
            Check(interaction.CommittedActionCount == 4 && interaction.LastActionWasBoss && interaction.LastResult.Prediction.PressureIncrease == interaction.LastResult.Prediction.Strength,
                "Boss must resume normal turns and unmodified pressure after the one-shot skip/syrup.");
            report.AppendLine("PASS: bill passes without player roll/reward; boss-turn input locked; spicy order+syrup use actual +5 but reward 3; own buffs survive passes; ticket skips only one boss turn; later boss pressure returns to normal.");

            Fixture();
            interaction.automaticBossTurns = true;
            interaction.BeginBottle(1);
            Use(ColaItemId.HotSauce);
            int candy = Stock(ColaItemId.FizzCandy);
            tokens = match.PlayerTokens;
            ClickCube(candy);
            Check(interaction.Model.Pressure == 4 && !interaction.Model.HasBurst && interaction.CommittedActionCount == 0 && match.PlayerHealth == 3,
                "Fizz candy crossing the limit must not roll, damage, reward or shake the player immediately.");
            yield return WaitIdle();
            Check(interaction.CommittedActionCount == 1 && interaction.LastActionWasBoss && interaction.LastResult.Burst &&
                match.BossHealth == 1 && match.PlayerHealth == 3 && match.PlayerTokens == tokens && match.BossTokens == 5 && !items.HotSauceReady,
                "Candy must transfer the over-limit bottle; the boss takes the hot-sauce burst damage and own reward only.");
            report.AppendLine("PASS: candy adds four then passes; pressure modification alone never rolls even over limit; next boss shake takes the 2-HP burst and 5-token boss reward.");

            Fixture();
            interaction.automaticBossTurns = true;
            int retained = Stock(ColaItemId.PizzaSlice);
            Use(ColaItemId.SafetyCap);
            Use(ColaItemId.SpicyOrder);
            Use(ColaItemId.Bill);
            interaction.Model.BeginBottle(1);
            // Fixture lowers only this Play-mode boss health so a real boss burst can end the encounter.
            SetBacking("BossHealth", 1);
            tokens = match.PlayerTokens;
            yield return WaitIdle();
            Check(match.State == ColaDuelController.DuelState.Won && !items.CanAct && !items.TryPurchase(ColaItemId.IceCup) &&
                !items.TryUseSlot(retained), "Victory must disable ordering and consumption.");
            Check(match.ContinueToNextBoss(), "Next boss transition did not begin.");
            yield return WaitIdle();
            Check(match.PlayerTokens == tokens && items.ItemAt(retained) == ColaItemId.PizzaSlice &&
                !items.SafetyReady && !items.ForceThreeReady && items.CanAct, "Next boss must carry inventory/currency but clear pending effects.");
            Use(ColaItemId.DoubleStamp);
            interaction.BeginBottle(1);
            ClickShake(1);
            yield return null;
            match.RestartRun(123);
            yield return new WaitForSecondsRealtime(6);
            Check(match.PlayerTokens == 0 && match.PlayerHealth == 3 && items.InventoryCount == 2 && !items.DoubleReady &&
                !interaction.IsBusy && interaction.CommittedActionCount == 0 && items.CanAct, "Restart during animation must cancel old damage/reward and reset inventory/effects.");
            CheckTerminal();
            report.AppendLine("PASS: victory locks all item actions; next boss retains player cubes/tokens and clears effects; restart during a real hand action cancels delayed reward/damage and restores starter inventory.");
            report.AppendLine("Scope: automated actual Unity Play-mode rules, physics input, original-hand motion, physical forecast and terminal state. Test-only currency/health and pre-stocked inventory fixtures used for coverage; production starts at zero. No Game-view visual confirmation, preview renders or screenshots.");
        }

        private void Fixture(int seed = -1)
        {
            match.RestartRun(seed < 0 ? FindSeed(.70) : seed);
            ColaItemInventoryFixture.Clear(items);
            interaction.automaticBossTurns = false;
            SetBacking("PlayerTokens", 200);
            match.terminal.SetPlayerState(match.PlayerHealth, match.playerMaxHealth, match.PlayerTokens, false);
        }
        private void SetBacking(string property, int value) => typeof(ColaDuelController)
            .GetField("<" + property + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(match, value);
        private int FindSeed(double minimumRoll)
        {
            for (int seed = 0; seed < 10000; seed++)
            {
                var random = new System.Random(seed);
                random.Next(interaction.settings.minimumBottleLimit, interaction.settings.maximumBottleLimit + 1);
                if (random.NextDouble() >= minimumRoll) return seed;
            }
            throw new InvalidOperationException("No deterministic fixture seed.");
        }
        private int Stock(ColaItemId id)
        {
            return ColaItemInventoryFixture.Stock(items, id);
        }
        private void Use(ColaItemId id) => ClickCube(Stock(id));
        private void ClickCube(int slot)
        {
            var collider = display.CubeCollider(slot);
            Check(collider, "Purchased item needs a live Cube collider.");
            Physics.SyncTransforms();
            Vector3 point = interaction.playerCamera.WorldToViewportPoint(collider.bounds.center);
            Check(point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1, "Item is outside live camera viewport: " + slot + "/" + point);
            Check(interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(collider.bounds.center)), "Physical cube click failed at slot " + slot);
        }
        private void ClickShake(int strength)
        {
            Physics.SyncTransforms();
            var key = interaction.strengthButtons[strength - 1].GetComponent<Collider>();
            Check(interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(key.bounds.center)) &&
                interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(interaction.bottleCollider.bounds.center)), "Physical strength/bottle input failed.");
        }
        private IEnumerator WaitIdle()
        {
            float started = Time.unscaledTime;
            while (interaction.IsBusy)
            {
                Check(Time.unscaledTime - started < 30, "Turn sequence timed out.");
                var driver = interaction.shakeAnimator;
                if (driver.IsPlaying)
                {
                    Check(driver.ActiveHand == (driver.PlayingBoss ? driver.bossHand : driver.playerHand), "Original hand binding changed.");
                    playerTravel = Mathf.Max(playerTravel, Vector3.Distance(playerRest, driver.playerHand.position));
                    bossTravel = Mathf.Max(bossTravel, Vector3.Distance(bossRest, driver.bossHand.position));
                }
                yield return null;
            }
            CheckTerminal();
        }
        private void CheckTerminal() => Check(match.terminal.PlayerHealth == match.PlayerHealth && match.terminal.BossHealth == match.BossHealth &&
            match.terminal.PlayerTokens == match.PlayerTokens && match.terminal.BossTokens == match.BossTokens, "Physical terminal is out of sync.");
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
