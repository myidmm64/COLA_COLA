using System.Collections;
using System.Collections.Generic;
using ColaCola.Core;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Uses public pressure/health only. Its RNG never reads or advances the bottle's outcome RNG.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaBossItemAI : MonoBehaviour
    {
        public bool automaticItemUse = true;
        [Range(1, 3)] public int maximumUsesPerTurn = 2;
        [Range(1, 6)] public int maximumItemsPerOrder = 3;
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
            while (Active && used < Mathf.Clamp(maximumUsesPerTurn, 1, 3))
            {
                int slot = -1;
                float best = 0;
                for (int i = 0; i < ColaItemController.Capacity; i++)
                {
                    var owned = items.ItemAt(i, true);
                    if (!owned.HasValue || !items.CanUseSlot(i, true)) continue;
                    float score = Score(owned.Value, strength) * Variation();
                    if (score > best) { best = score; slot = i; }
                }
                if (slot >= 0)
                {
                    if (!items.TryUseSlot(slot, true)) yield break;
                    used++;
                    yield return new WaitForSecondsRealtime(Mathf.Max(.1f, useDisplaySeconds));
                    continue;
                }
                if (!items.OrderAvailable(true) || SavingForRecovery(strength)) yield break;
                var order = new List<ColaItemId>();
                int budget = items.match.BossTokens;
                int capacity = Mathf.Min(maximumItemsPerOrder, ColaItemController.Capacity - items.BossInventoryCount);
                while (order.Count < capacity)
                {
                    ColaItemId? choice = null;
                    best = 0;
                    foreach (var id in items.BottleMenu)
                    {
                        if (order.Contains(id) || !items.CanPurchase(id, true) || ColaItemCatalog.Get(id).Price > budget) continue;
                        float score = Score(id, strength) * Variation();
                        if (score > best) { best = score; choice = id; }
                    }
                    if (!choice.HasValue) break;
                    order.Add(choice.Value);
                    budget -= ColaItemCatalog.Get(choice.Value).Price;
                }
                if (order.Count == 0 || !items.TryPurchaseBatch(order, true)) yield break;
                // Keep the acquired Cube visible before it is consumed, so the player can follow the decision.
                yield return new WaitForSecondsRealtime(Mathf.Max(.1f, purchaseDisplaySeconds));
            }
        }

        private float Variation() => .8f + (float)decisions.NextDouble() * .4f;

        private bool SavingForRecovery(int strength)
        {
            if (items.match.BossHealth >= items.match.BossMaxHealth ||
                items.match.BossTokens >= ColaItemCatalog.Get(ColaItemId.PizzaSlice).Price) return false;
            var own = items.PreviewAction(true);
            return items.match.interaction.Model.Predict(strength, own.PressureBonus, own.ProbabilityReduction).BurstProbability < .35;
        }

        private float Score(ColaItemId id, int strength)
        {
            var match = items.match;
            var model = match.interaction.Model;
            var own = items.PreviewAction(true);
            var opponent = items.PreviewAction(false);
            double risk = model.Predict(strength, own.PressureBonus, own.ProbabilityReduction).BurstProbability;
            switch (id)
            {
                case ColaItemId.PizzaSlice:
                    return match.BossHealth < match.BossMaxHealth ? 100 + (match.BossMaxHealth - match.BossHealth) * 10 : 0;
                case ColaItemId.IceCup:
                    return model.Pressure >= 3 && risk >= .18 && model.Pressure - 3 + strength + own.PressureBonus < model.Limit ? 25 + (float)risk * 30 : 0;
                case ColaItemId.FizzCandy:
                    return risk >= .4 ? 40 + (float)risk * 30 : 0;
                case ColaItemId.CarbonationSyrup:
                    return opponent.PressureBonus == 0 && risk < .55 ? 24 : 0;
                case ColaItemId.QueueTicket:
                    return !items.HasSkip(false) && risk < .22 ? 20 : 0;
                case ColaItemId.Bill:
                    return risk >= .55 ? 40 + (float)risk * 35 : 0;
                case ColaItemId.SpicyOrder:
                    return items.ForcedStrength(false) == 0 && risk < .55 ? 23 : 0;
                case ColaItemId.SafetyCap:
                    return own.ProbabilityReduction == 0 && risk >= .25 && risk < 1 ? 60 + (float)risk * 15 : 0;
                case ColaItemId.HotSauce:
                    return !items.HotSauceReady && risk < .25 && match.PlayerHealth <= 2 ? 28 : 0;
                case ColaItemId.DoubleStamp:
                    return own.RewardMultiplier == 1 && risk >= .5 && match.BossHealth > own.Damage ? 25 : 0;
                case ColaItemId.TipEnvelope:
                    return !own.SafeTip && risk >= .5 && risk < .8 ? 20 : 0;
                default: return 0;
            }
        }
    }
}
