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
    public static class ColaBossItemVerification
    {
        private const string Key = "COLA_COLA.BossItemVerification";
        private const string Output = "output/verification/latest/boss-items/Playmode-validation.txt";
        static ColaBossItemVerification() { if (SessionState.GetBool(Key, false)) Hook(); }
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
            new GameObject("BossItemPlayProbe").AddComponent<ColaBossItemPlayProbe>().Begin();
        }
        internal static void Complete(string report, Exception failure = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            ProjectVerificationBridge.WriteReport(Output, report + (failure == null ? "" : "\nFAIL: " + failure));
            SessionState.SetBool(Key + ".Passed", failure == null);
            if (failure == null) Debug.Log("COLA_BOSS_ITEMS_PLAYMODE_PASS\n" + report);
            else Debug.LogException(failure);
            EditorApplication.isPlaying = false;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= Changed;
            if (!SessionState.GetBool(Key + ".Passed", false))
            { if (Application.isBatchMode) EditorApplication.Exit(2); return; }
            if (ProjectVerificationBridge.FullRun) ProjectVerificationBridge.Schedule(ColaBurstVerification.Begin);
            else ProjectVerificationBridge.Schedule(ColaItemVerification.Begin);
        }
    }

    public sealed class ColaBossItemPlayProbe : MonoBehaviour
    {
        private ColaDuelController match;
        private ShakeSelectionInteraction interaction;
        private ColaItemController items;
        private ColaBossItemAI ai;
        private ColaItemPresentation display;
        private readonly StringBuilder report = new();
        private readonly List<ShakeResult> bossResults = new();
        private int playerResults, spent, purchasesSeen;
        private bool acquiredCubeSeen;

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
                catch (Exception exception) { ColaBossItemVerification.Complete(report.ToString(), exception); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested);
                else yield return current;
            }
            ColaBossItemVerification.Complete(report.ToString());
        }

        private IEnumerator Run()
        {
            Check(Application.isPlaying, "Boss-item checks must run in Play mode.");
            match = FindFirstObjectByType<ColaDuelController>();
            interaction = match.interaction;
            items = match.Items;
            ai = match.GetComponent<ColaBossItemAI>();
            display = match.GetComponent<ColaItemPresentation>();
            Check(ai && ai.automaticItemUse && items.BossInventoryCount == 2 && match.BossTokens == 0 && !items.CanBossAct,
                "Production must enable boss AI with two starter items and zero earned tokens.");
            Check(!items.TryPurchase(ColaItemId.IceCup, true) && !items.TryUseSlot(0, true), "Boss item actions must reject the player's phase.");

            Fixture();
            for (int i = 0; i < 6; i++) Stock(ColaItemId.IceCup, false);
            BeginBoss(999, 25);
            for (int i = 0; i < 6; i++) Stock(ColaItemId.IceCup, true);
            int balance = match.BossTokens;
            Check(!items.TryPurchase(ColaItemId.PizzaSlice, true) && balance == match.BossTokens, "Full boss inventory must not charge.");
            for (int actor = 0; actor < 2; actor++)
                for (int slot = 0; slot < 6; slot++) CheckCube(slot, actor == 1);
            Check(items.TryUseSlot(0, true) && interaction.Model.Pressure == 22 && interaction.CommittedActionCount == 0,
                "Boss ice must lower pressure without rolling or ending the boss turn.");
            yield return WaitIdle();
            var bossCube = display.CubeCollider(1, true);
            Check(!interaction.HandlePointer(interaction.playerCamera.WorldToScreenPoint(bossCube.bounds.center)) &&
                items.InventoryCount == 6 && items.BossInventoryCount == 5, "Player must not consume or steal a boss cube.");
            report.AppendLine("PASS: both independent six-slot inventories use the same prices/capacity; all 12 occupied Cube centers are in the live viewport and individually reachable by physics ray; boss Ice -3 retains its turn; full inventory is charge-free; player cannot use enemy cubes.");

            Fixture();
            BeginBoss(20, 8);
            Use(ColaItemId.CarbonationSyrup);
            Use(ColaItemId.SpicyOrder);
            Use(ColaItemId.SafetyCap);
            int duplicate = Stock(ColaItemId.SafetyCap, true);
            Check(!items.TryUseSlot(duplicate, true) && items.ItemAt(duplicate, true).HasValue, "Boss duplicate armed effects must retain their cube.");
            Use(ColaItemId.DoubleStamp);
            Use(ColaItemId.TipEnvelope);
            balance = match.BossTokens;
            int strength = interaction.PlannedBossStrength;
            double expected = Math.Max(0, Math.Pow((8d + strength) / 20, interaction.settings.probabilityExponent) - .3);
            yield return WaitIdle();
            Check(!bossResults[0].Burst && Math.Abs(bossResults[0].Prediction.BurstProbability - expected) < .000001 &&
                match.BossTokens == balance + strength * 2 && items.PreviewAction(true).RewardMultiplier == 1 && !items.PreviewAction(true).SafeTip,
                "Boss safety/stamp/tip must affect only its next own shake, with no low-risk tip.");
            interaction.automaticBossTurns = false;
            Check(interaction.SelectStrength(1) && interaction.Model.SelectedStrength == 3, "Boss spicy order must force player selection to 3.");
            var playerPrediction = interaction.Model.Predict(3, 2);
            yield return new WaitForSecondsRealtime(.5f);
            Check(interaction.gauge.PredictedPressure == playerPrediction.PressureAfter &&
                Math.Abs(interaction.gauge.DisplayedProbability - playerPrediction.BurstProbability) < .000001,
                "Player gauge must include the boss's forced strength and +2 syrup.");
            Check(interaction.ConfirmShake() && interaction.LastResult.Prediction.Strength == 3 && interaction.LastResult.Prediction.PressureIncrease == 5,
                "Boss attack must add pressure five while preserving selected-strength reward.");
            yield return WaitIdle();
            Check(items.ForcedStrength(false) == 0 && items.PreviewAction(false).PressureBonus == 0, "Player-targeted debuffs must consume exactly once.");
            report.AppendLine("PASS: boss syrup/spicy order target the player (forced 3, actual +5, matching animated forecast); boss safety -30%p, double reward and conditional tip are independent; duplicate effects cannot stack; one-shot effects expire on the correct actor's action.");

            Fixture();
            BeginBoss(20, 14);
            Use(ColaItemId.TipEnvelope);
            Use(ColaItemId.DoubleStamp);
            balance = match.BossTokens;
            strength = interaction.PlannedBossStrength;
            yield return WaitIdle();
            Check(!bossResults[0].Burst && bossResults[0].Prediction.BurstProbability >= .5 && match.BossTokens == balance + (strength + 3) * 2,
                "Boss high-risk safe tip must combine with its own double stamp.");
            Fixture();
            BeginBoss(1);
            Use(ColaItemId.HotSauce);
            Use(ColaItemId.SafetyCap);
            Use(ColaItemId.DoubleStamp);
            balance = match.BossTokens;
            yield return WaitIdle();
            Check(match.BossHealth == 1 && match.PlayerHealth == 3 && match.BossTokens == balance + 10 && !items.HotSauceReady,
                "Boss 100% burst must ignore safety, take two hot-sauce damage, get ten own tokens and reset bottle effects.");
            BeginBoss(999);
            Use(ColaItemId.PizzaSlice);
            Use(ColaItemId.PizzaSlice);
            int pizza = Stock(ColaItemId.PizzaSlice, true);
            Check(match.BossHealth == 3 && !items.TryUseSlot(pizza, true) && items.ItemAt(pizza, true).HasValue, "Boss pizza heals one, clamps and retains at full HP.");
            yield return WaitIdle();
            report.AppendLine("PASS: boss tip qualifies on final >=50% safe chance and doubles the full reward; hot sauce damages the actual exploding boss for two; the limit stays 100%; boss burst pays 10 with stamp; boss pizza heals one and preserves a full-HP slice.");

            Fixture();
            BeginBoss(20, 16);
            Use(ColaItemId.HotSauce);
            Use(ColaItemId.FizzCandy);
            balance = match.BossTokens;
            Check(interaction.Model.Pressure == 20 && !interaction.Model.HasBurst && interaction.CommittedActionCount == 0 && interaction.BossPassedTurn,
                "Boss candy must add four and pass without rolling, damage or reward.");
            yield return WaitIdle();
            Check(match.BossTokens == balance && playerResults == 0 && bossResults.Count == 0 && items.CanAct, "Boss pass must return player control without rewards.");
            interaction.automaticBossTurns = false;
            Shake(1);
            yield return WaitIdle();
            Check(match.PlayerHealth == 1 && match.BossHealth == 3 && match.PlayerTokens == 205, "Transferred over-limit hot-sauce bottle must hurt and reward only the player.");
            Fixture();
            BeginBoss(999);
            Use(ColaItemId.SafetyCap);
            Use(ColaItemId.Bill);
            balance = match.BossTokens;
            yield return WaitIdle();
            Check(bossResults.Count == 0 && match.BossTokens == balance && items.PreviewAction(true).ProbabilityReduction == .3,
                "Boss bill must pass for no reward and preserve its next-shake buff.");
            report.AppendLine("PASS: boss candy transfers pressure without a roll/reward, then player takes the actual hot-sauce burst; boss bill skips its own shake and preserves next-own-action buffs.");

            Fixture();
            BeginBoss(999);
            Use(ColaItemId.QueueTicket);
            yield return WaitIdle();
            Check(bossResults.Count == 2 && playerResults == 0 && !items.HasSkip(false) && items.CanAct,
                "Boss ticket must skip one player turn, run exactly one extra boss action and return control.");
            report.AppendLine("PASS: boss ticket runs two actual original-hand boss actions while skipping exactly one player turn, then restores player input without an extra player roll/reward.");

            Fixture();
            BeginBoss(20, 8);
            yield return WaitIdle();
            double unmodifiedRoll = bossResults[0].Roll;
            Fixture();
            SetBacking("BossTokens", 4);
            SyncTerminal();
            ai.automaticItemUse = true;
            BeginBoss(20, 8);
            yield return WaitIdle();
            Check(items.BossPurchaseCount == 1 && items.BossUseCount == 1 && acquiredCubeSeen &&
                match.BossTokens + spent == 4 + bossResults[0].Prediction.Strength && bossResults[0].Roll == unmodifiedRoll && match.PlayerTokens == 200,
                "Autonomous purchase/use must show a cube, debit only boss tokens, respect one-purchase budget and leave bottle RNG unchanged.");
            report.AppendLine("PASS: autonomous boss buys and uses a useful affordable item with a visible acquisition interval; only its earned account is debited; one purchase per turn; independent item-choice RNG leaves the actual bottle roll unchanged.");

            Fixture();
            SetBacking("BossHealth", 2);
            SetBacking("BossTokens", 7);
            SyncTerminal();
            ai.automaticItemUse = true;
            BeginBoss(999);
            yield return WaitIdle();
            Check(items.BossPurchaseCount == 0 && match.BossTokens >= 8 && match.BossHealth == 2,
                "Injured low-risk boss must save the last token for healing rather than drain its account on cheaper items.");
            balance = match.BossTokens;
            BeginBoss(999);
            yield return WaitIdle();
            Check(items.LastBossUsed == ColaItemId.PizzaSlice && match.BossHealth == 3 &&
                match.BossTokens == balance - 8 + bossResults[1].Prediction.Strength,
                "After earning the missing currency the boss must actually order/use pizza for eight tokens and heal itself.");
            Fixture();
            SetBacking("BossTokens", 5);
            SyncTerminal();
            ai.automaticItemUse = true;
            BeginBoss(1);
            yield return WaitIdle();
            Check(items.LastBossUsed == ColaItemId.Bill && match.BossTokens == 0 && match.BossHealth == 3 && bossResults.Count == 0 && items.CanAct,
                "An affordable automatic bill must avoid a known 100% burst by paying five and passing without a shake/reward.");
            report.AppendLine("PASS: injured low-risk boss banks seven tokens, earns the missing currency, then autonomously pays eight to heal; at a known 100% risk a five-token boss buys/uses a bill and returns player control without rolling or rewarding itself.");

            Fixture();
            ai.automaticItemUse = true;
            UsePlayer(ColaItemId.QueueTicket);
            interaction.BeginBottle(999);
            Shake(1);
            yield return WaitIdle();
            Check(bossResults.Count == 0 && items.BossPurchaseCount == 0 && match.BossTokens == 200, "Skipping a boss turn must skip its shopping/AI too.");
            Check(items.OrderAvailable(true), "A skipped boss turn must preserve its unused same-bottle order.");
            interaction.BeginBottle(999);
            ColaItemInventoryFixture.EnsureMenu(items, ColaItemId.CarbonationSyrup);
            Shake(1);
            float deadline = Time.unscaledTime + 20;
            while (items.BossPurchaseCount == 0 && Time.unscaledTime < deadline) yield return null;
            Check(items.LastBossOrderCount > 0 && items.BossInventoryCount == items.LastBossOrderCount && interaction.BossItemPhase,
                "Cancellation probe must stop during actual visible batch acquisition.");
            match.RestartRun(5);
            yield return new WaitForSecondsRealtime(3);
            Check(items.BossInventoryCount == 2 && items.InventoryCount == 2 && items.BossPurchaseCount == 0 && match.BossTokens == 0 &&
                match.PlayerTokens == 0 && !interaction.IsBusy && !interaction.BossItemPhase && !items.HasSkip(false), "Restart must cancel delayed AI use, items, coins, notices and turn flags.");
            report.AppendLine("PASS: player's skip suppresses boss AI/spending while preserving its unused same-bottle order; restart during the batch purchase display delay cancels all pending boss decisions and restores starter inventories/effects/accounts.");

            Fixture();
            int retained = Stock(ColaItemId.IceCup, false);
            BeginBoss(1);
            Stock(ColaItemId.PizzaSlice, true);
            SetBacking("BossHealth", 1);
            yield return WaitIdle();
            Check(match.State == ColaDuelController.DuelState.Won && !items.CanBossAct && !items.TryUseSlot(0, true), "Defeated boss must never use/heal/order again.");
            Check(match.ContinueToNextBoss() && items.BossInventoryCount == 2 && match.BossTokens == 0 && items.ItemAt(retained) == ColaItemId.IceCup,
                "Next boss must restore its starter stock and clear tokens but keep player inventory.");

            match.RestartRun(FindSeed());
            Observe();
            ai.automaticItemUse = true;
            Check(match.BossTokens == 0 && match.PlayerTokens == 0, "Final economy probe must start with real zero balances.");
            for (int turn = 0; turn < 6 && items.BossPurchaseCount == 0; turn++)
            {
                interaction.BeginBottle(999);
                Shake(1);
                yield return WaitIdle();
            }
            int earned = 0;
            foreach (var result in bossResults) earned += result.Burst ? 5 : result.Prediction.Strength;
            Check(items.BossPurchaseCount > 0 && items.BossUseCount > 0 && earned == match.BossTokens + spent && acquiredCubeSeen,
                "A real zero-start boss must earn, afford, buy and use an item with no injected/free currency.");
            report.AppendLine("PASS: defeated boss is locked; next encounter restores boss starter stock and clears its account; a fresh zero-token run earns rewards then autonomously buys/uses items on later new bottles with exact income/spending accounting.");
            report.AppendLine("Scope: actual Unity Play-mode state, two-owner effects, physical cube visibility/occlusion rays, real original-hand turn sequences and terminal checks. Coverage fixtures use Editor-only credit/HP and inventory setup; final economy probe uses zero starting currency. No Game-view visual confirmation or preview rendering.");
        }

        private void Fixture()
        {
            match.RestartRun(FindSeed());
            ai.automaticItemUse = false;
            ColaItemInventoryFixture.Clear(items);
            SetBacking("PlayerTokens", 200);
            SetBacking("BossTokens", 200);
            SyncTerminal();
            Observe();
        }
        private void Observe()
        {
            bossResults.Clear(); playerResults = spent = purchasesSeen = 0; acquiredCubeSeen = false;
            interaction.ShakeResolved -= Resolved;
            interaction.ShakeResolved += Resolved;
            items.InventoryChanged -= Changed;
            items.InventoryChanged += Changed;
        }
        private void Resolved(ShakeResult result)
        {
            if (interaction.LastActionWasBoss) bossResults.Add(result);
            else playerResults++;
        }
        private void Changed()
        {
            if (items.BossPurchaseCount <= purchasesSeen) return;
            purchasesSeen = items.BossPurchaseCount;
            spent += items.LastBossOrderCost;
            for (int i = 0; i < 6; i++) if (items.ItemAt(i, true).HasValue && display.CubeCollider(i, true)) acquiredCubeSeen = true;
        }
        private void BeginBoss(int limit, int pressure = 0)
        {
            interaction.BeginBottle(limit, pressure);
            // Controlled offers for effect/AI fixtures; the final zero-token economy probe uses unfiltered random menus.
            ColaItemInventoryFixture.EnsureMenu(items, ColaItemId.PizzaSlice, ColaItemId.Bill,
                ColaItemId.SafetyCap, ColaItemId.CarbonationSyrup);
            Check(interaction.PassPlayerTurn() && items.CanBossAct && interaction.BossItemPhase, "Boss preparation window did not open.");
        }
        private int Stock(ColaItemId id, bool boss)
        {
            return ColaItemInventoryFixture.Stock(items, id, boss);
        }
        private void Use(ColaItemId id) { int slot = Stock(id, true); Check(items.TryUseSlot(slot, true), "Boss use failed: " + id); }
        private void UsePlayer(ColaItemId id) { int slot = Stock(id, false); Check(items.TryUseSlot(slot), "Player use failed: " + id); }
        private void Shake(int strength) => Check(interaction.SelectStrength(strength) && interaction.ConfirmShake(), "Player shake input rejected.");
        private IEnumerator WaitIdle()
        {
            float deadline = Time.unscaledTime + 35;
            while (interaction.IsBusy)
            {
                Check(Time.unscaledTime < deadline, "Boss turn sequence timed out.");
                if (interaction.shakeAnimator.IsPlaying)
                {
                    Check(!items.CanBossAct && !items.TryPurchase(ColaItemId.TipEnvelope, true), "Boss cannot order during an actual hand action.");
                    Check(interaction.shakeAnimator.ActiveHand == (interaction.shakeAnimator.PlayingBoss ? interaction.shakeAnimator.bossHand : interaction.shakeAnimator.playerHand), "Original actor hand changed.");
                }
                yield return null;
            }
            Check(match.terminal.PlayerHealth == match.PlayerHealth && match.terminal.BossHealth == match.BossHealth &&
                match.terminal.PlayerTokens == match.PlayerTokens && match.terminal.BossTokens == match.BossTokens, "Physical terminal is stale.");
        }
        private void CheckCube(int slot, bool boss)
        {
            var collider = display.CubeCollider(slot, boss);
            Check(collider && collider.GetComponent<ColaItemCube>().IsBoss == boss, "Cube ownership binding missing.");
            Physics.SyncTransforms();
            var camera = interaction.playerCamera;
            var point = camera.WorldToViewportPoint(collider.bounds.center);
            Check(point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1, "Cube center outside camera viewport: " + boss + "/" + slot);
            Check(Physics.Raycast(camera.ScreenPointToRay(camera.WorldToScreenPoint(collider.bounds.center)), out var hit, 8, ~0, QueryTriggerInteraction.Ignore) && hit.collider == collider,
                "Cube is occluded by another prop: " + boss + "/" + slot + " hit=" + (hit.collider ? hit.collider.name : "none"));
        }
        private int FindSeed()
        {
            for (int seed = 0; seed < 10000; seed++)
            {
                var random = new System.Random(seed);
                random.Next(interaction.settings.minimumBottleLimit, interaction.settings.maximumBottleLimit + 1);
                if (random.NextDouble() > .99) return seed;
            }
            throw new InvalidOperationException("No safe fixture seed.");
        }
        private void SetBacking(string name, int value) => typeof(ColaDuelController).GetField("<" + name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(match, value);
        private void SyncTerminal()
        {
            match.terminal.SetPlayerState(match.PlayerHealth, match.playerMaxHealth, match.PlayerTokens, false);
            match.terminal.SetBossState(match.BossHealth, match.BossMaxHealth, match.BossTokens, false);
        }
        private void OnDestroy() { if (interaction) interaction.ShakeResolved -= Resolved; if (items) items.InventoryChanged -= Changed; }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
