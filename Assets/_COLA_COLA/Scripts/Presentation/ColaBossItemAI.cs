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
        [Range(1, 2)] public int maximumUsesPerTurn = 2;
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
            int used = 0;
            bool ordered = false;
            int budget = Mathf.Clamp(maximumUsesPerTurn, 1, 2);
            while (Active && used < budget)
            {
                if (items.IsUsingItem || items.IsReceivingItems(true)) { yield return null; continue; }
                // A delivery or pressure-changing item can alter the sensible shake completely.
                ChooseStrength();
                int slot = -1;
                float best = .035f;
                for (int i = 0; i < ColaItemController.Capacity; i++)
                {
                    var owned = items.ItemAt(i, true);
                    if (!owned.HasValue || !items.CanUseSlot(i, true)) continue;
                    float score = Score(owned.Value, budget - used) * Variation();
                    if (score > best) { best = score; slot = i; }
                }
                if (slot >= 0)
                {
                    if (!items.TryUseSlot(slot, true)) yield break;
                    used++;
                    yield return new WaitForSecondsRealtime(Mathf.Max(.1f, useDisplaySeconds));
                    continue;
                }
                if (ordered || !items.TryOrderWaiter(true)) yield break;
                ordered = true;
                // Keep the acquired props visible before considering another use.
                yield return new WaitForSecondsRealtime(Mathf.Max(.1f, purchaseDisplaySeconds));
            }
        }

        private float Variation() => .8f + (float)decisions.NextDouble() * .4f;

        private static double HealthWeight(int health) => health <= 1 ? 3 : health == 2 ? 1.7 : health == 3 ? 1.25 : 1;
        private static double CoinWeight(int tokens) => tokens < ColaItemController.WaiterCost ? .10 :
            tokens < ColaItemController.WaiterCost * 2 ? .055 : .02;

        private static double ExpectedReward(int strength, double risk, ItemActionModifiers modifiers)
            => (1 - risk) * ColaItemBalance.ShakeReward(strength, false, modifiers.RewardBonus) +
                risk * ColaItemBalance.ShakeReward(strength, true, modifiers.RewardBonus);

        private ShakePrediction OpponentReply(int pressure, ItemActionModifiers opponent)
        {
            var match = items.match;
            var model = match.interaction.Model;
            var best = model.PredictAtPressure(pressure, 1, opponent.PressureBonus, pressureMultiplier: opponent.PressureMultiplier);
            double bestValue = double.NegativeInfinity;
            // The reply balances its own health against income, rather than always choosing button 2.
            for (int strength = 1; strength <= 3; strength++)
            {
                var candidate = model.PredictAtPressure(pressure, strength, opponent.PressureBonus,
                    pressureMultiplier: opponent.PressureMultiplier);
                double value = CoinWeight(match.PlayerTokens) * ExpectedReward(strength, candidate.BurstProbability, opponent) -
                    candidate.BurstProbability * opponent.Damage * HealthWeight(match.PlayerHealth);
                if (value > bestValue) { bestValue = value; best = candidate; }
            }
            return best;
        }

        private double Utility(int strength, int pressure, ItemActionModifiers own, ItemActionModifiers opponent, bool passing = false)
        {
            var match = items.match;
            var model = match.interaction.Model;
            if (passing)
                return OpponentReply(pressure, opponent).BurstProbability *
                    opponent.Damage * HealthWeight(match.PlayerHealth) * .7;
            var prediction = model.PredictAtPressure(pressure, strength, own.PressureBonus, pressureMultiplier: own.PressureMultiplier);
            double risk = prediction.BurstProbability;
            double enemyRisk = OpponentReply(prediction.PressureAfter, opponent).BurstProbability;
            return CoinWeight(match.BossTokens) * ExpectedReward(strength, risk, own) -
                risk * own.Damage * HealthWeight(match.BossHealth) +
                (1 - risk) * enemyRisk * opponent.Damage * HealthWeight(match.PlayerHealth) * .7;
        }

        private double BestActionUtility(int pressure, ItemActionModifiers own, ItemActionModifiers opponent)
            => System.Math.Max(Utility(1, pressure, own, opponent), System.Math.Max(
                Utility(2, pressure, own, opponent), Utility(3, pressure, own, opponent)));

        private bool HasBill()
        {
            for (int i = 0; i < ColaItemController.Capacity; i++)
                if (items.ItemAt(i, true) == ColaItemId.Bill) return true;
            return false;
        }

        private float Score(ColaItemId id, int remainingUses)
        {
            var match = items.match;
            var model = match.interaction.Model;
            var own = items.PreviewAction(true);
            var opponent = items.PreviewAction(false);
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
                    value = BestActionUtility(pressure + ColaItemBalance.CandyIncrease, own, opponent) - before;
                    if (remainingUses >= 2 && HasBill())
                        value = System.Math.Max(value, Utility(1, pressure + ColaItemBalance.CandyIncrease, own, opponent, true) - before + .03);
                    return (float)value;
                case ColaItemId.CarbonationSyrup:
                    opponent = new ItemActionModifiers(opponent.PressureBonus + ColaItemBalance.SyrupBonus, opponent.Damage, opponent.RewardBonus, opponent.PressureMultiplier);
                    break;
                case ColaItemId.QueueTicket:
                    return .10f + Mathf.Min(3, items.InventoryCount) * .035f;
                case ColaItemId.Bill:
                    return (float)(Utility(1, pressure, own, opponent, true) - before);
                case ColaItemId.SpicyOrder:
                    opponent = new ItemActionModifiers(opponent.PressureBonus, opponent.Damage, opponent.RewardBonus, ColaItemBalance.SpicyMultiplier);
                    break;
                case ColaItemId.SafetyCap:
                    own = new ItemActionModifiers(own.PressureBonus, Mathf.Max(0, own.Damage - ColaItemBalance.ShieldReduction), own.RewardBonus, own.PressureMultiplier);
                    break;
                case ColaItemId.HotSauce:
                    own = new ItemActionModifiers(own.PressureBonus, own.Damage + ColaItemBalance.HotDamageBonus, own.RewardBonus, own.PressureMultiplier);
                    opponent = new ItemActionModifiers(opponent.PressureBonus, opponent.Damage + ColaItemBalance.HotDamageBonus, opponent.RewardBonus, opponent.PressureMultiplier);
                    break;
                case ColaItemId.DoubleStamp:
                    own = new ItemActionModifiers(own.PressureBonus, own.Damage, own.RewardBonus + ColaItemBalance.StampRewardBonus, own.PressureMultiplier);
                    break;
                default: return 0;
            }
            return (float)(BestActionUtility(pressure, own, opponent) - before);
        }
    }
}
