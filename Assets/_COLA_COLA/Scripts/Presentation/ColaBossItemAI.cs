using System.Collections;
using ColaCola.Core;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Uses visible state only. Its RNG never reads or advances the bottle's outcome RNG.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaBossItemAI : MonoBehaviour
    {
        public bool automaticItemUse = true;
        [Min(.1f)] public float purchaseDisplaySeconds = .8f;
        [Min(.1f)] public float useDisplaySeconds = .65f;
        private ColaItemController items;
        private System.Random decisions;
        public void Bind(ColaItemController controller) => items = controller;
        public void ResetDecisions(int? seed = null) => decisions = seed.HasValue ? new System.Random(seed.Value ^ 0x719B) : new System.Random();
        private bool Active => automaticItemUse && isActiveAndEnabled && items && items.CanBossAct;
        public int ChosenStrength { get; private set; } = 1;

        /// <summary>Returns 1..3 from public pressure, health, tokens and effects; never samples an outcome.</summary>
        public int ChooseStrength()
        {
            if (!items || !items.match || !items.match.interaction || items.match.interaction.Model == null)
                return ChosenStrength = 1;
            if (decisions == null) ResetDecisions();
            var own = items.PreviewAction(true);
            var opponent = items.PreviewAction(false);
            int pressure = items.match.interaction.Model.Pressure;
            double a = Utility(1, pressure, own, opponent);
            double b = Utility(2, pressure, own, opponent);
            double c = Utility(3, pressure, own, opponent);
            double best = System.Math.Max(a, System.Math.Max(b, c));
            // Later rematches improve judgment, without hidden information or perfect play.
            int laterLevels = System.Math.Max(0, items.match.Level - 3);
            double temperature = items.match.Level <= 1 ? .14 : items.match.Level == 2 ? .10 :
                System.Math.Max(.04, .065 - .004 * laterLevels);
            double exploration = items.match.Level <= 1 ? .06 : System.Math.Max(.015, .025 - .001 * laterLevels);
            if (items.match.Level >= ColaBossTactics.DefenseStage)
            { temperature = ColaBossTactics.StageFiveTemperature; exploration = ColaBossTactics.StageFiveExploration; }
            else if (items.match.Level >= ColaBossTactics.AttackStage)
            { temperature = ColaBossTactics.StageFourTemperature; exploration = ColaBossTactics.StageFourExploration; }
            double w1 = System.Math.Exp((a - best) / temperature) + exploration;
            double w2 = System.Math.Exp((b - best) / temperature) + exploration;
            double w3 = System.Math.Exp((c - best) / temperature) + exploration;
            double ticket = decisions.NextDouble() * (w1 + w2 + w3);
            return ChosenStrength = ticket < w1 ? 1 : ticket < w1 + w2 ? 2 : 3;
        }

        public IEnumerator PrepareTurn(int strength)
        {
            if (decisions == null) ResetDecisions();
            ChosenStrength = Mathf.Clamp(strength, 1, 3);
            bool ordered = false;
            bool completingCombo = false;
            while (Active || ColaFrontEnd.BlocksGameplay)
            {
                while (ColaFrontEnd.BlocksGameplay) yield return null;
                if (!Active) yield break;
                if (items.IsUsingItem || items.IsReceivingItems(true)) { yield return null; continue; }
                items.match.Progression?.FlushPendingGrants();
                // A delivery or pressure-changing item can alter the sensible shake completely.
                ChooseStrength();
                int slot = -1;
                if (items.match.Level >= ColaBossTactics.AttackStage)
                {
                    slot = PlanAttackCombo();
                    if (slot >= 0) completingCombo = true;
                    else if (completingCombo) slot = FindUsable(ColaItemId.Bill);
                }
                float best = .035f;
                bool hasPlan = slot >= 0;
                for (int i = 0; !hasPlan && i < items.CapacityFor(true); i++)
                {
                    var owned = items.ItemAt(i, true);
                    if (!owned.HasValue || !items.CanUseSlot(i, true)) continue;
                    float score = Score(owned.Value) * Variation();
                    if (score > best) { best = score; slot = i; }
                }
                if (slot >= 0)
                {
                    if (!items.TryUseSlot(slot, true)) yield break;
                    yield return new ColaPauseDelay(Mathf.Max(.1f, useDisplaySeconds));
                    continue;
                }
                if (!ordered && TryReturnClutter()) { yield return new ColaPauseDelay(.55f); continue; }
                if (ordered || !items.TryOrderWaiter(true)) yield break;
                ordered = true;
                // Keep the acquired props visible before considering another use.
                yield return new ColaPauseDelay(Mathf.Max(.1f, purchaseDisplaySeconds));
            }
        }

        private float Variation() => .8f + (float)decisions.NextDouble() * .4f;

        private static double HealthWeight(int health) => health <= 1 ? 3 : health == 2 ? 1.7 : health == 3 ? 1.25 : 1;
        private double CoinWeight(int tokens, bool boss = true) => tokens < items.WaiterCostFor(boss) ? .10 :
            tokens < items.WaiterCostFor(boss) * 2 ? .055 : .02;

        private static double ExpectedReward(int strength, double risk, ItemActionModifiers modifiers)
            => (1 - risk) * System.Math.Max(0, ColaItemBalance.ShakeReward(strength, false, modifiers.RewardBonus) - modifiers.RewardPenalty) +
                risk * System.Math.Max(0, ColaItemBalance.ShakeReward(strength, true, modifiers.RewardBonus) - modifiers.RewardPenalty);

        private ShakePrediction OpponentReply(int pressure, ItemActionModifiers opponent, BottlePressureModel evaluation = null)
        {
            var match = items.match;
            var model = evaluation ?? match.interaction.Model;
            var best = model.PredictAtPressure(pressure, 1, opponent.PressureBonus, probabilityReduction: opponent.ProbabilityReduction, pressureMultiplier: opponent.PressureMultiplier, probabilityIncrease: opponent.ProbabilityIncrease);
            double bestValue = double.NegativeInfinity;
            // The reply balances its own health against income, rather than always choosing button 2.
            for (int strength = 1; strength <= 3; strength++)
            {
                var candidate = model.PredictAtPressure(pressure, strength, opponent.PressureBonus,
                    probabilityReduction: opponent.ProbabilityReduction, pressureMultiplier: opponent.PressureMultiplier, probabilityIncrease: opponent.ProbabilityIncrease);
                double gain = ExpectedReward(strength, candidate.BurstProbability, opponent);
                if (match.Level >= ColaBossTactics.DefenseStage && match.Progression != null)
                    gain = (1 - candidate.BurstProbability) * match.Progression.Reward(candidate, false, opponent, false) +
                        candidate.BurstProbability * match.Progression.Reward(candidate, true, opponent, false);
                double value = CoinWeight(match.PlayerTokens, false) * gain -
                    candidate.BurstProbability * opponent.Damage * HealthWeight(match.PlayerHealth);
                if (value > bestValue) { bestValue = value; best = candidate; }
            }
            return best;
        }

        private double Utility(int strength, int pressure, ItemActionModifiers own, ItemActionModifiers opponent, bool passing = false, BottlePressureModel evaluation = null)
        {
            var match = items.match;
            var model = evaluation ?? match.interaction.Model;
            if (passing)
                return OpponentReply(pressure, opponent, model).BurstProbability *
                    opponent.Damage * HealthWeight(match.PlayerHealth) * .7;
            var prediction = model.PredictAtPressure(pressure, strength, own.PressureBonus, probabilityReduction: own.ProbabilityReduction, pressureMultiplier: own.PressureMultiplier, probabilityIncrease: own.ProbabilityIncrease);
            double risk = prediction.BurstProbability;
            var replyModifiers = opponent;
            if (strength == 3 && match.Progression.Has(ColaAbilityId.RoughHands, true))
                replyModifiers = new ItemActionModifiers(opponent.PressureBonus + ColaItemBalance.RoughPressureBonus,
                    opponent.Damage, opponent.RewardBonus, 1, opponent.ProbabilityReduction, opponent.ProbabilityIncrease, opponent.RewardPenalty);
            double enemyRisk = OpponentReply(prediction.PressureAfter, replyModifiers, model).BurstProbability;
            double reward = (1 - risk) * match.Progression.Reward(prediction, false, own, true) +
                risk * match.Progression.Reward(prediction, true, own, true);
            return CoinWeight(match.BossTokens) * reward -
                risk * own.Damage * HealthWeight(match.BossHealth) +
                (1 - risk) * enemyRisk * opponent.Damage * HealthWeight(match.PlayerHealth) * .7;
        }

        private double BestActionUtility(int pressure, ItemActionModifiers own, ItemActionModifiers opponent, BottlePressureModel evaluation = null)
            => System.Math.Max(Utility(1, pressure, own, opponent, evaluation: evaluation), System.Math.Max(
                Utility(2, pressure, own, opponent, evaluation: evaluation), Utility(3, pressure, own, opponent, evaluation: evaluation)));

        private bool HasBill()
        {
            for (int i = 0; i < items.CapacityFor(true); i++)
                if (items.ItemAt(i, true) == ColaItemId.Bill) return true;
            return false;
        }

        private int FindUsable(ColaItemId id)
        {
            for (int i = 0; i < items.CapacityFor(true); i++)
                if (items.ItemAt(i, true) == id && items.CanUseSlot(i, true)) return i;
            return -1;
        }

        private int PlanAttackCombo()
        {
            if (FindUsable(ColaItemId.Bill) < 0) return -1;
            var model = items.match.interaction.Model;
            var own = items.PreviewAction(true);
            var opponent = items.PreviewAction(false);
            double before = BestActionUtility(model.Pressure, own, opponent);
            double passNow = Utility(1, model.Pressure, own, opponent, true);
            int selected = -1;
            double bestImprovement = ColaBossTactics.MinimumAttackImprovement;
            for (int i = 0; i < items.CapacityFor(true); i++)
            {
                var id = items.ItemAt(i, true);
                if (!id.HasValue || !items.CanUseSlot(i, true)) continue;
                int pressure = model.Pressure;
                var target = opponent;
                BottlePressureModel evaluation = null;
                switch (id.Value)
                {
                    case ColaItemId.FizzCandy:
                    case ColaItemId.SaltPacket:
                        int added = id.Value == ColaItemId.FizzCandy ? ColaItemBalance.CandyIncrease : ColaItemBalance.SaltIncrease;
                        target = new ItemActionModifiers(opponent.PressureBonus + added, opponent.Damage, opponent.RewardBonus, 1,
                            opponent.ProbabilityReduction, opponent.ProbabilityIncrease, opponent.RewardPenalty);
                        break;
                    case ColaItemId.BottleOpener:
                        evaluation = new BottlePressureModel(Mathf.Max(1, model.Limit - ColaItemBalance.OpenerReduction), model.ProbabilityExponent, pressure)
                            { ForceBurst = model.ForceBurst };
                        break;
                    case ColaItemId.SpicyOrder:
                        target = new ItemActionModifiers(opponent.PressureBonus, opponent.Damage, opponent.RewardBonus, 1,
                            opponent.ProbabilityReduction, opponent.ProbabilityIncrease + ColaItemBalance.SpicyProbabilityIncrease, opponent.RewardPenalty);
                        break;
                    default: continue;
                }
                double passAfter = Utility(1, pressure, own, target, true, evaluation);
                double improvement = passAfter - passNow;
                if (passAfter - before < ColaBossTactics.MinimumComboGain || improvement <= bestImprovement) continue;
                bestImprovement = improvement;
                selected = i;
            }
            return selected;
        }

        private bool ShouldSaveDefense(ColaItemId id, ItemActionModifiers own)
        {
            if (items.match.Level < ColaBossTactics.DefenseStage ||
                (id != ColaItemId.IceCup && id != ColaItemId.SafetyCap && id != ColaItemId.CoolingPack)) return false;
            var model = items.match.interaction.Model;
            var prediction = model.Predict(ChosenStrength, own.PressureBonus, own.ProbabilityReduction, own.PressureMultiplier, own.ProbabilityIncrease);
            int pressure = model.Pressure;
            var defense = own;
            if (id == ColaItemId.IceCup) pressure = Mathf.Max(0, pressure - ColaItemBalance.IceReduction);
            else if (id == ColaItemId.SafetyCap)
                defense = new ItemActionModifiers(own.PressureBonus, own.Damage, own.RewardBonus, 1,
                    own.ProbabilityReduction + ColaItemBalance.SafetyProbabilityReduction, own.ProbabilityIncrease, own.RewardPenalty);
            else
                defense = new ItemActionModifiers(own.PressureBonus - ColaItemBalance.CoolingReduction, own.Damage, own.RewardBonus, 1,
                    own.ProbabilityReduction, own.ProbabilityIncrease, own.RewardPenalty);
            var after = model.PredictAtPressure(pressure, ChosenStrength, defense.PressureBonus, defense.ProbabilityReduction,
                defense.PressureMultiplier, defense.ProbabilityIncrease);
            return prediction.BurstProbability < ColaBossTactics.DefenseRiskThreshold ||
                prediction.BurstProbability - after.BurstProbability < ColaBossTactics.MinimumDefenseReduction;
        }

        private float Score(ColaItemId id)
        {
            var match = items.match;
            var model = match.interaction.Model;
            var own = items.PreviewAction(true);
            var opponent = items.PreviewAction(false);
            if (ShouldSaveDefense(id, own)) return 0;
            int pressure = model.Pressure;
            double before = BestActionUtility(pressure, own, opponent);
            double value;
            switch (id)
            {
                case ColaItemId.PizzaSlice:
                    return (float)(HealthWeight(match.BossHealth) * 1.1);
                case ColaItemId.IceCup:
                    return (float)(BestActionUtility(Mathf.Max(0, pressure - ColaItemBalance.IceReduction), own, opponent) - before);
                case ColaItemId.FizzCandy:
                case ColaItemId.SaltPacket:
                    int added = id == ColaItemId.SaltPacket ? ColaItemBalance.SaltIncrease : ColaItemBalance.CandyIncrease;
                    opponent = new ItemActionModifiers(opponent.PressureBonus + added, opponent.Damage, opponent.RewardBonus, 1,
                        opponent.ProbabilityReduction, opponent.ProbabilityIncrease, opponent.RewardPenalty);
                    break;
                case ColaItemId.QueueTicket:
                    return (float)(ColaItemBalance.TicketTokens * CoinWeight(match.BossTokens)) + (items.InventoryCount > 0 ? .10f + Mathf.Min(3, items.InventoryCount) * .035f : 0);
                case ColaItemId.Bill:
                    return (float)(Utility(1, pressure, own, opponent, true) - before);
                case ColaItemId.SpicyOrder:
                    opponent = new ItemActionModifiers(opponent.PressureBonus, opponent.Damage, opponent.RewardBonus, 1,
                        opponent.ProbabilityReduction, opponent.ProbabilityIncrease + ColaItemBalance.SpicyProbabilityIncrease, opponent.RewardPenalty);
                    break;
                case ColaItemId.SafetyCap:
                    own = new ItemActionModifiers(own.PressureBonus, own.Damage, own.RewardBonus, 1,
                        own.ProbabilityReduction + ColaItemBalance.SafetyProbabilityReduction, own.ProbabilityIncrease, own.RewardPenalty);
                    break;
                case ColaItemId.CoolingPack:
                    own = new ItemActionModifiers(own.PressureBonus - ColaItemBalance.CoolingReduction, own.Damage, own.RewardBonus, 1,
                        own.ProbabilityReduction, own.ProbabilityIncrease, own.RewardPenalty);
                    break;
                case ColaItemId.HotSauce:
                    own = new ItemActionModifiers(own.PressureBonus, own.Damage + ColaItemBalance.HotDamageBonus, own.RewardBonus, 1,
                        own.ProbabilityReduction, own.ProbabilityIncrease, own.RewardPenalty);
                    opponent = new ItemActionModifiers(opponent.PressureBonus, opponent.Damage + ColaItemBalance.HotDamageBonus, opponent.RewardBonus, 1,
                        opponent.ProbabilityReduction, opponent.ProbabilityIncrease, opponent.RewardPenalty);
                    break;
                case ColaItemId.DoubleStamp:
                    own = new ItemActionModifiers(own.PressureBonus, own.Damage, own.RewardBonus + ColaItemBalance.StampRewardBonus, 1,
                        own.ProbabilityReduction, own.ProbabilityIncrease, own.RewardPenalty);
                    break;
                case ColaItemId.BottleOpener:
                    var opened = new BottlePressureModel(Mathf.Max(1, model.Limit - ColaItemBalance.OpenerReduction), model.ProbabilityExponent, pressure)
                        { ForceBurst = model.ForceBurst };
                    value = BestActionUtility(pressure, own, opponent, opened) - before;
                    if (HasBill())
                        value = System.Math.Max(value, Utility(1, pressure, own, opponent, true, opened) - before + .03);
                    return (float)value;
                case ColaItemId.Straw:
                    return (float)(ColaItemBalance.StrawTokens * CoinWeight(match.BossTokens) +
                        System.Math.Min(ColaItemBalance.StrawTokens, match.PlayerTokens) * CoinWeight(match.PlayerTokens, false) * .5);
                case ColaItemId.SodaWater:
                    value = BestActionUtility(model.Limit / 2, own, opponent) - before;
                    if (HasBill())
                        value = System.Math.Max(value, Utility(1, model.Limit / 2, own, opponent, true) - before + .03);
                    return (float)value;
                case ColaItemId.CoinCup:
                    return (float)(ColaItemBalance.CoinCupTokens * CoinWeight(match.BossTokens));
                case ColaItemId.TokenPunch:
                    opponent = new ItemActionModifiers(opponent.PressureBonus, opponent.Damage, opponent.RewardBonus, 1,
                        opponent.ProbabilityReduction, opponent.ProbabilityIncrease, opponent.RewardPenalty + ColaItemBalance.PunchTokens);
                    return (float)(BestActionUtility(pressure, own, opponent) - before + .08 * System.Math.Min(3, ColaItemBalance.PunchTokens));
                case ColaItemId.FreshLid:
                    var resealed = new BottlePressureModel(Mathf.Min(999, model.Limit + ColaItemBalance.LidIncrease), model.ProbabilityExponent, pressure)
                        { ForceBurst = model.ForceBurst };
                    return (float)(BestActionUtility(pressure, own, opponent, resealed) - before);
                case ColaItemId.ExchangeCoupon:
                    return RerollValue();
                case ColaItemId.SwapTongs:
                    return SwapValue();
                default: return 0;
            }
            return (float)(BestActionUtility(pressure, own, opponent) - before);
        }

        private bool TryReturnClutter()
        {
            if (items.BossInventoryCount < items.CapacityFor(true) - 1 ||
                items.CapacityFor(true) - items.BossInventoryCount >= items.WaiterCountFor(true) ||
                !items.match.CanAffordTokens(items.WaiterCostFor(true), true)) return false;
            int chosen = -1; float lowest = .6f;
            for (int i = 0; i < items.CapacityFor(true); i++)
            {
                var id = items.ItemAt(i, true);
                if (id.HasValue && HeldValue(id.Value) < lowest) { chosen = i; lowest = HeldValue(id.Value); }
            }
            return chosen >= 0 && items.TryReturnItem(chosen, true);
        }

        private float SwapValue()
        {
            float value = 0; int count = 0;
            for (int i = 0; i < items.CapacityFor(); i++)
            { var id = items.ItemAt(i); if (id.HasValue) { value += HeldValue(id.Value); count++; } }
            return count > 0 ? .16f + value / count * .12f : .14f;
        }

        private float RerollValue() => .13f;

        private float HeldValue(ColaItemId id)
            => id switch
            {
                ColaItemId.PizzaSlice => items.match.BossHealth < items.match.BossMaxHealth ? 1.2f : .15f,
                ColaItemId.SafetyCap => items.PreviewAction(true).ProbabilityIncrease > 0 ? 1.1f : .65f,
                ColaItemId.Bill => items.match.interaction.Model.Pressure > items.match.interaction.Model.Limit / 2 ? 1.3f : .5f,
                _ => .8f
            };
    }
}
