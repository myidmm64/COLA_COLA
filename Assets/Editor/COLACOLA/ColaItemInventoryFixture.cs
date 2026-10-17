using System;
using System.Reflection;
using ColaCola.Core;
using ColaCola.Presentation;
using UnityEngine;

namespace ColaCola.Editor
{
    /// <summary>Editor-only stock earned across earlier bottles. It does not simulate a current-bottle purchase.</summary>
    public static class ColaItemInventoryFixture
    {
        public static void EnsureMenu(ColaItemController items, params ColaItemId[] required)
        {
            if (!Application.isPlaying || items.match.interaction.IsBusy)
                throw new InvalidOperationException("Menu setup requires an idle Play-mode interaction.");
            var interaction = items.match.interaction;
            int limit = interaction.Model.Limit, pressure = interaction.Model.Pressure;
            for (int attempt = 0; attempt < 2048; attempt++)
            {
                bool found = true;
                foreach (var id in required) if (!items.IsOnMenu(id)) { found = false; break; }
                if (found) return;
                interaction.BeginBottle(limit, pressure);
            }
            throw new InvalidOperationException("No matching random-menu fixture was found.");
        }

        public static void Clear(ColaItemController items)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Inventory fixtures require Play mode.");
            foreach (string field in new[] { "inventory", "bossInventory" })
            {
                var owned = (ColaItemId?[])typeof(ColaItemController).GetField(field,
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(items);
                Array.Clear(owned, 0, owned.Length);
            }
            typeof(ColaItemPresentation).GetMethod("RefreshInventory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(items.GetComponent<ColaItemPresentation>(), null);
        }

        public static int Stock(ColaItemController items, ColaItemId id, bool boss = false)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Inventory fixtures require Play mode.");
            var inventory = (ColaItemId?[])typeof(ColaItemController).GetField(boss ? "bossInventory" : "inventory",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(items);
            int slot = Array.FindIndex(inventory, item => !item.HasValue);
            var balance = typeof(ColaDuelController).GetField(boss ? "<BossTokens>k__BackingField" : "<PlayerTokens>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            int tokens = (int)balance.GetValue(items.match), cost = ColaItemCatalog.Get(id).Price;
            if (slot < 0 || tokens < cost) throw new InvalidOperationException("Insufficient fixture stock capacity/budget.");
            inventory[slot] = id;
            balance.SetValue(items.match, tokens - cost);
            typeof(ColaItemPresentation).GetMethod("RefreshInventory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(items.GetComponent<ColaItemPresentation>(), null);
            items.match.terminal.SetPlayerState(items.match.PlayerHealth, items.match.playerMaxHealth, items.match.PlayerTokens, false);
            items.match.terminal.SetBossState(items.match.BossHealth, items.match.BossMaxHealth, items.match.BossTokens, false);
            return slot;
        }
    }
}
