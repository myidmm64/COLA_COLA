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
        [Range(1, 3)] public int maximumUsesPerTurn = 2;
        [Min(.1f)] public float purchaseDisplaySeconds = .8f;
        [Min(.1f)] public float useDisplaySeconds = .65f;
        private ColaItemController items;
        private System.Random decisions;
        public void Bind(ColaItemController controller) => items = controller;
        public void ResetDecisions(int? seed = null) => decisions = seed.HasValue ? new System.Random(seed.Value ^ 0x719B) : new System.Random();
        private bool Active => automaticItemUse && isActiveAndEnabled && items && items.CanBossAct;

        public IEnumerator PrepareTurn(int strength)
        {
            if (decisions == null) ResetDecisions();
            int used = 0;
            bool ordered = false;
            while (Active && used < Mathf.Clamp(maximumUsesPerTurn, 1, 3))
            {
                if (items.IsUsingItem || items.IsReceivingItems(true)) { yield return null; continue; }
                int slot = -1;
                float best = .01f;
                for (int i = 0; i < ColaItemController.Capacity; i++)
                {
                    var owned = items.ItemAt(i, true);
                    if (!owned.HasValue || !items.CanUseSlot(i, true)) continue;
                    float score = Score(owned.Value, strength, Mathf.Clamp(maximumUsesPerTurn, 1, 3) - used) * Variation();
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

        private static double HealthWeight(int health) => health <= 1 ? 2.4 : health == 2 ? 1.4 : 1;
        private double CoinWeight => items.match.BossTokens < ColaItemController.WaiterCost ? .12 :
            items.match.BossTokens < ColaItemController.WaiterCost * 2 ? .065 : .025;

        private double Utility(int strength, int pressure, ItemActionModifiers own, ItemActionModifiers opponent, bool passing = false)
        {
            var match = items.match;
            var model = match.interaction.Model;
            if (passing)
                return model.PredictAtPressure(pressure, 2, opponent.PressureBonus, pressureMultiplier: opponent.PressureMultiplier).BurstProbability *
                    opponent.Damage * HealthWeight(match.PlayerHealth) * .55;
            var prediction = model.PredictAtPressure(pressure, strength, own.PressureBonus, pressureMultiplier: own.PressureMultiplier);
            double risk = prediction.BurstProbability;
            double gain = (1 - risk) * (strength + own.RewardBonus) + risk * (5 + own.RewardBonus);
            double enemyRisk = model.PredictAtPressure(prediction.PressureAfter, 2, opponent.PressureBonus,
                pressureMultiplier: opponent.PressureMultiplier).BurstProbability;
            return CoinWeight * gain - risk * own.Damage * HealthWeight(match.BossHealth) +
                (1 - risk) * enemyRisk * opponent.Damage * HealthWeight(match.PlayerHealth) * .55;
        }

        private bool HasBill()
        {
            for (int i = 0; i < ColaItemController.Capacity; i++)
                if (items.ItemAt(i, true) == ColaItemId.Bill) return true;
            return false;
        }

        private float Score(ColaItemId id, int strength, int remainingUses)
        {
            var match = items.match;
            var model = match.interaction.Model;
            var own = items.PreviewAction(true);
            var opponent = items.PreviewAction(false);
            int pressure = model.Pressure;
            double before = Utility(strength, pressure, own, opponent);
            double value;
            switch (id)
            {
                case ColaItemId.PizzaSlice:
                    return (float)(HealthWeight(match.BossHealth) * 1.1);
                case ColaItemId.IceCup:
                    return (float)(Utility(strength, Mathf.Max(0, pressure - ColaItemBalance.IceReduction), own, opponent) - before);
                case ColaItemId.FizzCandy:
                    value = Utility(strength, pressure + ColaItemBalance.CandyIncrease, own, opponent) - before;
                    if (remainingUses >= 2 && HasBill())
                        value = System.Math.Max(value, Utility(strength, pressure + ColaItemBalance.CandyIncrease, own, opponent, true) - before + .03);
                    return (float)value;
                case ColaItemId.CarbonationSyrup:
                    opponent = new ItemActionModifiers(opponent.PressureBonus + ColaItemBalance.SyrupBonus, opponent.Damage, opponent.RewardBonus, opponent.PressureMultiplier);
                    break;
                case ColaItemId.QueueTicket:
                    return .10f + Mathf.Min(3, items.InventoryCount) * .035f;
                case ColaItemId.Bill:
                    return (float)(Utility(strength, pressure, own, opponent, true) - before);
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
            return (float)(Utility(strength, pressure, own, opponent) - before);
        }
    }
}
