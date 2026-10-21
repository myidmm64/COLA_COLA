using System;
using System.Collections.Generic;
using ColaCola.Core;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Run-only passives and locally persisted achievements. Uses its own reward RNG.</summary>
    public sealed class ColaRunProgression
    {
        private const string SaveKey = "COLA_COLA.Achievements.v1";
        [Serializable] private sealed class SaveData
        {
            public List<int> unlocked = new();
            public int usedItems;
        }
        private readonly ColaDuelController match;
        private readonly HashSet<ColaAbilityId> owned = new();
        private readonly HashSet<ColaAbilityId> bossOwned = new();
        private sealed class PassiveState
        {
            public bool ServiceAvailable = true, FirstBottleOrder = true;
            public int PendingCoupons, PendingRandom, RecyclingUses;
        }
        private readonly PassiveState[] passives = { new(), new() };
        private PassiveState For(bool boss) => passives[boss ? 1 : 0];
        private readonly List<ColaAbilityId> choices = new(3);
        private readonly HashSet<ColaAchievementId> unlocked = new();
        private readonly Queue<string> notices = new();
        private SaveData save;
        private System.Random random;
        private bool pending;
        private int stageBursts, stageOrders, stageItemUses, stageStrengthMask;
        private int runOrders, safeStreak, exchanges;
        private float noticeUntil;
        private string notice;
        public int BurstCount { get; private set; }
        public int AbilityCount => owned.Count;
        public int UnlockedCount => unlocked.Count;
        public bool DebugAssisted { get; private set; }
        public bool ChoiceReady { get; private set; }
        public bool JournalOpen { get; set; }
        public bool BlocksGameplay => pending || JournalOpen;
        public bool HasPendingChoice => pending;
        public bool FirstOrderUsed => stageOrders > 0;
        public IReadOnlyList<ColaAbilityId> Choices => choices;
        public bool Has(ColaAbilityId id, bool boss = false) => (boss ? bossOwned : owned).Contains(id);
        public int BossAbilityCount => bossOwned.Count;
        public bool IsUnlocked(ColaAchievementId id) => unlocked.Contains(id);
        public int PlayerCapacity => CapacityFor(false);
        public int CapacityFor(bool boss) => ColaItemController.Capacity + (Has(ColaAbilityId.ExtraStorage, boss) ? 2 : 0);
        public int BellCost => BellCostFor(false);
        public int BellCostFor(bool boss) => Math.Max(1, ColaItemController.WaiterCost -
            (Has(ColaAbilityId.RegularDiscount, boss) ? 1 : 0) -
            (Has(ColaAbilityId.PrepaidContract, boss) && For(boss).FirstBottleOrder ? ColaItemBalance.PrepaidDiscount : 0));
        public int BellCount => BellCountFor(false);
        public int BellCountFor(bool boss) => ColaItemController.WaiterItemCount +
            (Has(ColaAbilityId.FirstOrderService, boss) && For(boss).ServiceAvailable && match.Items &&
                CapacityFor(boss) - match.Items.InventoryCountFor(boss) > ColaItemController.WaiterItemCount ? 1 : 0);
        public int SaltBonus => 0;
        public int CoolingBonus => 0;
        public double PassiveProbabilityReduction => ProbabilityReductionFor(false);
        public double ProbabilityReductionFor(bool boss)
        {
            if ((boss ? match.BossHealth : match.PlayerHealth) > ColaItemBalance.LowHealthThreshold) return 0;
            return (Has(ColaAbilityId.CoolingExpert, boss) ? ColaItemBalance.LowHealthProbabilityReduction : 0) +
                (Has(ColaAbilityId.CrisisManagement, boss) ? ColaItemBalance.CrisisProbabilityReduction : 0);
        }
        public int SavingsBonus => SavingsBonusFor(false);
        public int SavingsBonusFor(bool boss) => Has(ColaAbilityId.HotDeal, boss) &&
            (boss ? match.BossTokens : match.PlayerTokens) >= ColaItemBalance.SavingsTokenThreshold ? ColaItemBalance.SavingsRewardBonus : 0;

        public ColaRunProgression(ColaDuelController owner)
        {
            match = owner;
            try { save = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey, "")); }
            catch (ArgumentException) { save = null; }
            save ??= new SaveData();
            save.unlocked ??= new List<int>();
            foreach (int id in save.unlocked)
                if (Enum.IsDefined(typeof(ColaAchievementId), id)) unlocked.Add((ColaAchievementId)id);
        }

        public void ResetRun(int? seed)
        {
            owned.Clear(); bossOwned.Clear(); CancelSelection(); JournalOpen = false;
            BurstCount = runOrders = safeStreak = exchanges = 0;
            for (int i = 0; i < passives.Length; i++) passives[i] = new PassiveState();
            DebugAssisted = false;
            random = seed.HasValue ? new System.Random(seed.Value ^ 0x75B2) : new System.Random();
            notices.Clear(); notice = null;
        }

        public void BeginBoss()
        {
            stageBursts = stageOrders = stageItemUses = stageStrengthMask = 0;
            bossOwned.Clear();
            for (int i = 0; i < ColaBossTactics.StartingAbilitiesForStage(match.Level); i++)
                bossOwned.Add(ColaBossTactics.AbilityAt(i));
            // Player pending deliveries persist between bosses; enemy deliveries never do.
            passives[1] = new PassiveState();
            CancelSelection(); JournalOpen = false;
            for (int actor = 0; actor < 2; actor++)
            {
                bool boss = actor == 1;
                For(boss).ServiceAvailable = true;
                if (Has(ColaAbilityId.TidyCoupon, boss)) For(boss).PendingCoupons++;
            }
        }

        public void OnBottleStarted()
        {
            for (int actor = 0; actor < 2; actor++)
            {
                bool boss = actor == 1;
                For(boss).FirstBottleOrder = true;
                if (Has(ColaAbilityId.RegularSupply, boss)) For(boss).PendingRandom += ColaItemBalance.SupplyItemCount;
            }
        }

        public void MarkDebugAssisted() => DebugAssisted = true;
        public bool DebugGrantAbility(ColaAbilityId id)
        {
            if (!match.DebugToolsAvailable || !Enum.IsDefined(typeof(ColaAbilityId), id)) return false;
            MarkDebugAssisted();
            if (!owned.Add(id)) return false;
            OnAbilityAcquired(id);
            match.Items.RefreshCapacity(); match.interaction.RefreshPrediction();
            return true;
        }

        public bool DebugSetAchievement(ColaAchievementId id, bool earned)
        {
            if (!match.DebugToolsAvailable || !Enum.IsDefined(typeof(ColaAchievementId), id)) return false;
            MarkDebugAssisted();
            bool changed = earned ? unlocked.Add(id) : unlocked.Remove(id);
            if (!changed) return false;
            save.unlocked.RemoveAll(value => value == (int)id);
            if (earned) save.unlocked.Add((int)id);
            Persist(); return true;
        }

        public void DebugSetAllAchievements(bool earned)
        {
            if (!match.DebugToolsAvailable) return;
            MarkDebugAssisted();
            foreach (var entry in ColaProgressionCatalog.Achievements)
            {
                if (earned) { if (unlocked.Add(entry.Id)) save.unlocked.Add((int)entry.Id); }
                else { unlocked.Remove(entry.Id); save.unlocked.RemoveAll(value => value == (int)entry.Id); }
            }
            Persist();
        }

        public void CancelSelection() { pending = ChoiceReady = false; choices.Clear(); }

        public int Reward(ShakePrediction prediction, bool burst, ItemActionModifiers modifiers, bool boss)
        {
            int reward = ColaItemBalance.ShakeReward(prediction.Strength, burst, modifiers.RewardBonus);
            if (burst) return Math.Max(0, reward + (Has(ColaAbilityId.BreakagePay, boss) ? 2 : 0) +
                (Has(ColaAbilityId.AccidentSubsidy, boss) ? ColaItemBalance.AccidentTokenBonus : 0) - modifiers.RewardPenalty);
            if (prediction.Strength == 1 && Has(ColaAbilityId.LightHand, boss)) reward++;
            if (prediction.Strength == 3 && Has(ColaAbilityId.BoldChoice, boss)) reward += ColaItemBalance.HighAbilityReward;
            if (prediction.Strength == 2 && Has(ColaAbilityId.SaltyTaste, boss)) reward += ColaItemBalance.MediumAbilityReward;
            if (prediction.BurstProbability >= ColaItemBalance.DangerPayThreshold && Has(ColaAbilityId.DangerPay, boss))
                reward += ColaItemBalance.DangerPayBonus;
            return Math.Max(0, reward - modifiers.RewardPenalty);
        }

        public void OnShake(ShakeResult result, bool boss, ItemActionModifiers modifiers)
        {
            if (boss && modifiers.RewardPenalty > 0 && Reward(result.Prediction, result.Burst, modifiers, true) == 0)
                Unlock(ColaAchievementId.EmptyWallet);
            if (!result.Burst && result.Prediction.Strength == 3 && Has(ColaAbilityId.RoughHands, boss))
                match.Items.AddIncomingPressure(!boss, ColaItemBalance.RoughPressureBonus);
            if (!boss)
            {
                stageStrengthMask |= 1 << (result.Prediction.Strength - 1);
                if (result.Burst) { stageBursts++; safeStreak = 0; }
                else
                {
                    safeStreak++;
                    if (safeStreak >= 5) Unlock(ColaAchievementId.SafeStreak);
                    if (result.Prediction.BurstProbability >= .75) Unlock(ColaAchievementId.DaringSip);
                    if (result.Prediction.PressureAfter == result.Prediction.Limit - 1) Unlock(ColaAchievementId.PaperThin);
                    if (modifiers.ProbabilityIncrease > 0) Unlock(ColaAchievementId.DoubleTension);
                }
            }
            if (result.Burst) BurstCount++;
            bool won = match.State == ColaDuelController.DuelState.Won || match.State == ColaDuelController.DuelState.Completed;
            if (won) OnBossDefeated(boss && result.Burst && modifiers.Damage > 1);
            if (match.State == ColaDuelController.DuelState.Lost || match.State == ColaDuelController.DuelState.Completed)
            { CancelSelection(); return; }
            if (result.Burst && BurstCount % 3 == 0 && owned.Count < ColaProgressionCatalog.Abilities.Count) pending = true;
        }

        public void ReadySelection()
        {
            if (!pending) return;
            var pool = new List<ColaAbilityId>();
            foreach (var definition in ColaProgressionCatalog.Abilities)
                if (!Has(definition.Id)) pool.Add(definition.Id);
            choices.Clear();
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            { int index = random.Next(pool.Count); choices.Add(pool[index]); pool.RemoveAt(index); }
            ChoiceReady = choices.Count > 0;
            if (!ChoiceReady) CancelSelection();
            JournalOpen = false;
        }

        public bool ChooseAbility(ColaAbilityId id)
        {
            if (!ChoiceReady || !choices.Contains(id) || !owned.Add(id)) return false;
            CancelSelection();
            OnAbilityAcquired(id);
            match.Items.RefreshCapacity();
            match.interaction.RefreshPrediction();
            if (owned.Count >= 4) Unlock(ColaAchievementId.AbilityCollector);
            return true;
        }

        public void OnBell(bool boss, int deliveredCount = ColaItemController.WaiterItemCount)
        {
            For(boss).FirstBottleOrder = false;
            if (Has(ColaAbilityId.FirstOrderService, boss) && deliveredCount > ColaItemController.WaiterItemCount) For(boss).ServiceAvailable = false;
            if (boss) return;
            stageOrders++; runOrders++;
            Unlock(ColaAchievementId.FirstOrder);
            if (runOrders >= 5) Unlock(ColaAchievementId.BellRegular);
        }

        private void OnAbilityAcquired(ColaAbilityId id)
        {
            if (id == ColaAbilityId.RecyclingCredit) For(false).RecyclingUses = 0;
            if (id == ColaAbilityId.FirstOrderService) For(false).ServiceAvailable = true;
            if (id == ColaAbilityId.TidyCoupon) For(false).PendingCoupons++;
            FlushPendingGrants();
        }

        // Deliver outside item-use events so a new coupon cannot replace the prop
        // that the item presentation is about to detach from the consumed slot.
        public void FlushPendingGrants()
        {
            if (!Application.IsPlaying(match.gameObject) || ColaFrontEnd.BlocksGameplay || BlocksGameplay || !match.Items || !match.interaction ||
                (match.State != ColaDuelController.DuelState.Playing && match.State != ColaDuelController.DuelState.Won)) return;
            match.Items.FlushReplacements();
            if (match.Items.IsUsingItem || (match.interaction.IsBusy && !match.interaction.BossItemPhase)) return;
            for (int actor = 0; actor < 2; actor++)
            {
                bool boss = actor == 1;
                if (match.Items.IsReceivingItems(boss)) continue;
                var state = For(boss);
                while (state.PendingCoupons > 0 && match.Items.TryGrantPassiveItem(ColaItemId.ExchangeCoupon, boss)) state.PendingCoupons--;
                while (state.PendingRandom > 0 && match.Items.TryGrantPassiveRandomItem(boss)) state.PendingRandom--;
            }
        }
        public void ClearPendingItemGrants()
        { foreach (var state in passives) state.PendingCoupons = state.PendingRandom = 0; }

        public void OnItemUsed(ColaItemId id, bool boss, bool fromExchange, int pressureBefore, int limitBefore)
        {
            if (Has(ColaAbilityId.RecyclingCredit, boss) && ++For(boss).RecyclingUses % 3 == 0) match.GrantPassiveTokens(1, boss);
            if (boss) return;
            stageItemUses++;
            if (id == ColaItemId.ExchangeCoupon && ++exchanges >= 3) Unlock(ColaAchievementId.ExchangeRegular);
            if (fromExchange) Unlock(ColaAchievementId.MenuExchange);
            if (id == ColaItemId.IceCup && pressureBefore >= limitBefore * .75f) Unlock(ColaAchievementId.EmergencyCooling);
            if (id == ColaItemId.SafetyCap) Unlock(ColaAchievementId.SafetyCheck);
            if (DebugAssisted) return;
            int before = save.usedItems;
            save.usedItems |= ColaItemCatalog.UsageBitFor(id);
            int count = 0;
            foreach (var item in ColaItemCatalog.All) if ((save.usedItems & ColaItemCatalog.UsageBitFor(item.Id)) != 0) count++;
            if (count >= 5) Unlock(ColaAchievementId.MenuExplorer);
            if (count >= ColaItemCatalog.All.Count) Unlock(ColaAchievementId.FullCourse);
            if (before != save.usedItems) Persist();
        }

        private void OnBossDefeated(bool hotBurst)
        {
            Unlock((ColaAchievementId)(match.Level - 1));
            if (stageBursts == 0) Unlock(ColaAchievementId.AccidentFree);
            if (match.PlayerHealth == 1) Unlock(ColaAchievementId.LastSip);
            if (stageOrders == 0) Unlock(ColaAchievementId.SelfSufficient);
            if (stageItemUses == 0) Unlock(ColaAchievementId.HeadOn);
            if (stageStrengthMask == 1) Unlock(ColaAchievementId.GentleShakes);
            if (stageStrengthMask == 4) Unlock(ColaAchievementId.StrongShakes);
            if (hotBurst) Unlock(ColaAchievementId.SpicyVictory);
        }

        private void Unlock(ColaAchievementId id)
        {
            if (DebugAssisted || !unlocked.Add(id)) return;
            save.unlocked.Add((int)id);
            notices.Enqueue(ColaLocalization.Get("ui.achievements", "업적") + " · " + ColaProgressionCatalog.Achievements[(int)id].Name);
            Persist();
        }

        private void Persist()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        public string CurrentNotice
        {
            get
            {
                if (ColaGameClock.Time >= noticeUntil)
                { notice = notices.Count > 0 ? notices.Dequeue() : null; noticeUntil = ColaGameClock.Time + 3; }
                return notice;
            }
        }
    }
}
