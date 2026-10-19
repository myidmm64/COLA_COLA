using System;
using System.Collections.Generic;
using System.Text;
using ColaCola.Core;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Purchases, eight physical inventory slots and one-use effects for the single-player duel.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaItemController : MonoBehaviour
    {
        public const int Capacity = 8;
        public const int MenuSize = 6;
        public const int WaiterCost = 6;
        public const int WaiterItemCount = 2;
        public ColaDuelController match;
        private readonly List<ColaItemId> bottleMenu = new(MenuSize);
        private IReadOnlyList<ColaItemId> menuView;
        private System.Random menuRandom;
        private System.Random itemRandom;
        public IReadOnlyList<ColaItemId> BottleMenu => menuView ??= bottleMenu.AsReadOnly();
        public bool IsOnMenu(ColaItemId id) => bottleMenu.Contains(id);
        private readonly ColaItemId?[] inventory = new ColaItemId?[Capacity];
        private readonly ColaItemId?[] bossInventory = new ColaItemId?[Capacity];
        private sealed class Effects
        {
            public bool Stamp, Syrup, Spicy;
            public void Clear() { Stamp = Syrup = Spicy = false; }
        }
        private readonly Effects playerEffects = new(), bossEffects = new();
        private bool bottleHot;
        private bool playerShopUsed, bossShopUsed;
        public bool ShopUsed(bool boss = false) => boss ? bossShopUsed : playerShopUsed;
        public bool OrderAvailable(bool boss = false)
            => !boss && match && match.ItemTestMenuEnabled;
        public event Action InventoryChanged;
        public event Action<ColaItemId, int, bool> ItemUsed;
        public event Action<int[], bool> WaiterOrdered;
        public event Action PresentationReset;
        private ColaItemUsePresentation usePresentation;
        private ColaItemPresentation inventoryPresentation;
        public bool IsUsingItem => usePresentation && usePresentation.IsPlaying;
        public bool IsReceivingItems(bool boss = false) => inventoryPresentation && inventoryPresentation.IsReceivingItems(boss);
        internal void CancelItemUsePresentation() { if (usePresentation) usePresentation.Cancel(); }
        public bool SafetyReady => false; // Retired damage shield; the cap now clears pressure penalties immediately.
        public bool StampReady => playerEffects.Stamp;
        public bool SpicyReady => bossEffects.Spicy;
        // Old Editor diagnostics still compile; current effects have no tip/force/skip state.
        public bool DoubleReady => StampReady;
        public bool TipReady => false;
        public bool SyrupReady => bossEffects.Syrup;
        public bool ForceThreeReady => false;
        public bool SkipBossReady => false;
        public bool HotSauceReady => bottleHot;
        public bool HasHotSaucePenalty(bool boss) => bottleHot;
        public string BossNotice { get; private set; } = "";
        public float BossNoticeUntil { get; private set; }
        public int BossPurchaseCount { get; private set; }
        public int LastBossOrderCost { get; private set; }
        public int LastBossOrderCount { get; private set; }
        public int BossUseCount { get; private set; }
        public ColaItemId? LastBossUsed { get; private set; }
        public ColaItemId? LastBossPurchased { get; private set; }
        public int InventoryCount => InventoryCountFor(false);
        public int BossInventoryCount => InventoryCountFor(true);
        private bool MatchActive => Application.IsPlaying(gameObject) && isActiveAndEnabled && match && match.isActiveAndEnabled &&
            match.State == ColaDuelController.DuelState.Playing && match.interaction && match.interaction.isActiveAndEnabled &&
            match.interaction.InputEnabled;
        public bool CanAct => MatchActive && !match.interaction.IsBusy && !match.interaction.IsBossTurn;
        public bool CanBossAct => MatchActive && match.interaction.IsBossTurn && match.interaction.BossItemPhase &&
            !match.interaction.BossPassedTurn && match.interaction.automaticBossTurns;
        public bool CanActFor(bool boss) => boss ? CanBossAct : CanAct;
        private ColaItemId?[] InventoryFor(bool boss) => boss ? bossInventory : inventory;
        private Effects EffectsFor(bool boss) => boss ? bossEffects : playerEffects;
        public int InventoryCountFor(bool boss)
        {
            int count = 0;
            foreach (var item in InventoryFor(boss)) if (item.HasValue) count++;
            return count;
        }

        public static ColaItemController EnsureFor(ColaDuelController match)
        {
            var items = match.GetComponent<ColaItemController>();
            if (!items) items = match.gameObject.AddComponent<ColaItemController>();
            items.match = match;
            match.interaction.items = items;
            var display = match.GetComponent<ColaItemPresentation>();
            if (!display) display = match.gameObject.AddComponent<ColaItemPresentation>();
            items.inventoryPresentation = display;
            display.Bind(items);
            var itemUse = match.GetComponent<ColaItemUsePresentation>();
            if (!itemUse) itemUse = match.gameObject.AddComponent<ColaItemUsePresentation>();
            items.usePresentation = itemUse;
            itemUse.Bind(items, display);
            var sheet = match.GetComponent<ColaOrderSheet>();
            if (!sheet) sheet = match.gameObject.AddComponent<ColaOrderSheet>();
            sheet.Bind(items);
            var bell = match.GetComponent<ColaWaiterBell>();
            if (!bell) bell = match.gameObject.AddComponent<ColaWaiterBell>();
            bell.Bind(items);
            var ai = match.GetComponent<ColaBossItemAI>();
            if (!ai) ai = match.gameObject.AddComponent<ColaBossItemAI>();
            ai.Bind(items);
            return items;
        }

        public ColaItemId? ItemAt(int slot, bool boss = false) => slot >= 0 && slot < Capacity ? InventoryFor(boss)[slot] : null;

        public bool CanPurchase(ColaItemId id, bool boss = false)
        {
            return ColaItemCatalog.Contains(id) && CanActFor(boss) && OrderAvailable(boss) && InventoryCountFor(boss) < Capacity;
        }

        public bool TryPurchase(ColaItemId id, bool boss = false)
            => TryPurchaseBatch(new[] { id }, boss);

        public bool CanPurchaseBatch(IReadOnlyList<ColaItemId> order, bool boss = false)
        {
            if (!CanActFor(boss) || !OrderAvailable(boss) || order == null || order.Count == 0 ||
                order.Count > Capacity - InventoryCountFor(boss)) return false;
            foreach (var id in order)
            {
                if (!ColaItemCatalog.Contains(id)) return false;
            }
            return true;
        }

        public bool TryPurchaseBatch(IReadOnlyList<ColaItemId> order, bool boss = false)
        {
            if (!CanPurchaseBatch(order, boss))
                return Reject("테스트 메뉴 사용 여부, 내 턴과 빈 칸을 확인하세요.", boss);
            // Snapshot before notifying the presentation: successful checkout clears its cart.
            var purchase = new ColaItemId[order.Count];
            const int total = 0; // Test-menu grants never spend the real wallet.
            for (int i = 0; i < purchase.Length; i++)
                purchase[i] = order[i];
            if (!match.TrySpendTokens(total, boss)) return false;
            if (boss) bossShopUsed = true;
            else playerShopUsed = true;
            var owned = InventoryFor(boss);
            int next = 0;
            for (int i = 0; i < Capacity; i++)
                if (!owned[i].HasValue && next < purchase.Length) owned[i] = purchase[next++];
            if (boss)
            {
                BossPurchaseCount++; LastBossPurchased = purchase[purchase.Length - 1];
                LastBossOrderCost = total; LastBossOrderCount = purchase.Length;
                AnnounceBoss("주문 " + purchase.Length + "개 / " + total + "토큰");
            }
            else match.SetActionMessage(purchase.Length + "개 주문 완료 / " + total + "토큰. 보관함의 아이템을 눌러 사용.");
            InventoryChanged?.Invoke();
            return true;
        }

        public bool CanInspectItems => MatchActive;
        public bool CanInspectWaiter => MatchActive;
        public bool CanOrderWaiter(bool boss = false)
            => MatchActive && (!boss || CanBossAct) && Capacity - InventoryCountFor(boss) >= WaiterItemCount &&
                match.CanAffordTokens(WaiterCost, boss);

        public bool TryOrderWaiter(bool boss = false)
        {
            if (!CanOrderWaiter(boss)) return Reject($"벨 주문: {WaiterCost}토큰과 빈 칸 {WaiterItemCount}개가 필요합니다.", boss);
            if (!match.TrySpendWaiterTokens(boss)) return false;
            var purchase = DrawRandomItems(WaiterItemCount);
            var owned = InventoryFor(boss);
            var deliveredSlots = new int[purchase.Length];
            int next = 0;
            for (int i = 0; i < owned.Length && next < purchase.Length; i++)
                if (!owned[i].HasValue)
                {
                    deliveredSlots[next] = i;
                    owned[i] = purchase[next++];
                }
            if (boss)
            {
                BossPurchaseCount++;
                LastBossPurchased = purchase[purchase.Length - 1];
                LastBossOrderCost = WaiterCost;
                LastBossOrderCount = WaiterItemCount;
                AnnounceBoss($"벨 주문: 랜덤 아이템 {WaiterItemCount}개");
            }
            else match.SetActionMessage($"벨 주문: 랜덤 아이템 {WaiterItemCount}개를 받았습니다.");
            InventoryChanged?.Invoke();
            WaiterOrdered?.Invoke(deliveredSlots, boss);
            return true;
        }

        private ColaItemId[] DrawRandomItems(int count)
        {
            // Shared item draws are independent of pressure rolls, AI decisions and the old test-menu RNG.
            itemRandom ??= new System.Random();
            return ColaItemCatalog.DrawRandom(itemRandom, count);
        }

        public bool CanUseSlot(int slot, bool boss = false)
        {
            if (IsUsingItem || !CanActFor(boss) || !ItemAt(slot, boss).HasValue || !ColaItemCatalog.Contains(ItemAt(slot, boss).Value)) return false;
            if (inventoryPresentation && inventoryPresentation.IsSlotArriving(slot, boss)) return false;
            var sheet = GetComponent<ColaOrderSheet>();
            if (!boss && sheet && sheet.BlocksGameplay) return false;
            var own = EffectsFor(boss);
            var target = EffectsFor(!boss);
            return ItemAt(slot, boss).Value switch
            {
                ColaItemId.IceCup => match.interaction.Model.Pressure > 0,
                ColaItemId.FizzCandy => match.interaction.Model.Pressure < match.interaction.Model.Limit,
                ColaItemId.CarbonationSyrup => !target.Syrup && !target.Spicy,
                ColaItemId.QueueTicket => InventoryCountFor(!boss) > 0,
                ColaItemId.SpicyOrder => !target.Spicy && !target.Syrup,
                ColaItemId.SafetyCap => own.Syrup || own.Spicy,
                ColaItemId.PizzaSlice => boss ? match.BossHealth < match.BossMaxHealth : match.PlayerHealth < match.playerMaxHealth,
                ColaItemId.HotSauce => !bottleHot,
                ColaItemId.DoubleStamp => !own.Stamp,
                ColaItemId.BottleOpener => match.interaction.Model.Limit > 1,
                ColaItemId.Straw => match.CanTransferTokens(ColaItemBalance.StrawTokens, boss),
                ColaItemId.SodaWater => match.interaction.Model.Pressure != match.interaction.Model.Limit / 2,
                ColaItemId.CoinCup => (boss ? match.BossTokens : match.PlayerTokens) < 9999,
                ColaItemId.SwapTongs => HasSwapPair(slot, boss),
                _ => true
            };
        }

        public bool TryUseSlot(int slot, bool boss = false)
        {
            if (!CanActFor(boss) || !ItemAt(slot, boss).HasValue) return false;
            if (!CanUseSlot(slot, boss)) return Reject("지금은 사용할 수 없습니다. 아이템은 유지됩니다.", boss);
            var owned = InventoryFor(boss);
            var id = owned[slot].Value;
            var own = EffectsFor(boss);
            var target = EffectsFor(!boss);
            bool pass = false;
            switch (id)
            {
                case ColaItemId.IceCup:
                    match.interaction.Model.AdjustPressure(-ColaItemBalance.IceReduction);
                    break;
                case ColaItemId.FizzCandy:
                    match.interaction.Model.AdjustPressure(ColaItemBalance.CandyIncrease);
                    break;
                case ColaItemId.CarbonationSyrup:
                    target.Syrup = true;
                    break;
                case ColaItemId.QueueTicket:
                    if (!TryCancelRandomItem(!boss)) return false;
                    break;
                case ColaItemId.Bill: pass = true; break;
                case ColaItemId.SpicyOrder:
                    target.Spicy = true;
                    break;
                case ColaItemId.SafetyCap:
                    own.Syrup = own.Spicy = false;
                    break;
                case ColaItemId.PizzaSlice:
                    if (!match.TryHeal(boss)) return false;
                    break;
                case ColaItemId.HotSauce:
                    bottleHot = true;
                    break;
                case ColaItemId.DoubleStamp:
                    own.Stamp = true;
                    break;
                case ColaItemId.BottleOpener:
                    match.interaction.Model.AdjustLimit(-ColaItemBalance.OpenerReduction);
                    break;
                case ColaItemId.Straw:
                    if (!match.TryTransferTokens(ColaItemBalance.StrawTokens, boss)) return false;
                    break;
                case ColaItemId.SodaWater:
                    var model = match.interaction.Model;
                    model.AdjustPressure(model.Limit / 2 - model.Pressure);
                    break;
                case ColaItemId.CoinCup:
                    if (!match.TryGrantItemTokens(ColaItemBalance.CoinCupTokens, boss)) return false;
                    break;
                case ColaItemId.SwapTongs:
                    if (!TrySwapRandomItem(slot, boss)) return false;
                    break;
                default: return false;
            }
            owned[slot] = null;
            if (boss) { BossUseCount++; LastBossUsed = id; AnnounceBoss("사용: " + ColaItemCatalog.Get(id).Name + " — " + ColaItemCatalog.Get(id).Effect); }
            else match.SetActionMessage(ColaItemCatalog.Get(id).Name + " 사용. " + ColaItemCatalog.Get(id).Effect);
            match.interaction.RefreshPrediction();
            if (pass)
            {
                if (boss) match.interaction.PassBossTurn();
                else match.interaction.PassPlayerTurn();
            }
            // Passing starts its normal coroutine before the presentation acquires its input lock.
            // Detach the visible used prop before refreshing the now-empty physical slot.
            ItemUsed?.Invoke(id, slot, boss);
            InventoryChanged?.Invoke();
            return true;
        }

        private bool Reject(string reason, bool boss)
        {
            if (!boss)
            {
                match.SetActionMessage(reason);
                ColaTabletopAudio.Cue(match.interaction, "Blocked", transform.position, .45f);
            }
            return false;
        }
        private bool TryCancelRandomItem(bool boss)
        {
            var owned = InventoryFor(boss);
            var occupied = new int[Capacity];
            int count = 0;
            for (int i = 0; i < owned.Length; i++) if (owned[i].HasValue) occupied[count++] = i;
            if (count == 0) return false;
            itemRandom ??= new System.Random();
            owned[occupied[itemRandom.Next(count)]] = null;
            return true;
        }

        private bool HasSwapPair(int usedSlot, bool boss)
        {
            var own = InventoryFor(boss);
            var other = InventoryFor(!boss);
            for (int i = 0; i < Capacity; i++)
                for (int j = 0; j < Capacity; j++)
                    if (i != usedSlot && own[i].HasValue && other[j].HasValue && own[i] != other[j]) return true;
            return false;
        }

        private bool TrySwapRandomItem(int usedSlot, bool boss)
        {
            var own = InventoryFor(boss);
            var other = InventoryFor(!boss);
            var pairs = new List<int>();
            for (int i = 0; i < Capacity; i++)
                for (int j = 0; j < Capacity; j++)
                    if (i != usedSlot && own[i].HasValue && other[j].HasValue && own[i] != other[j])
                        pairs.Add(i * Capacity + j);
            if (pairs.Count == 0) return false;
            itemRandom ??= new System.Random();
            int pair = pairs[itemRandom.Next(pairs.Count)];
            int a = pair / Capacity, b = pair % Capacity;
            (own[a], other[b]) = (other[b], own[a]);
            return true;
        }
        private void AnnounceBoss(string action)
        {
            BossNotice = "보스 " + action;
            BossNoticeUntil = Time.unscaledTime + 4;
            match.SetActionMessage(BossNotice);
        }

        public ItemActionModifiers PreviewAction(bool boss)
        {
            var own = EffectsFor(boss);
            return new ItemActionModifiers(own.Syrup ? ColaItemBalance.SyrupBonus : 0,
                1 + (bottleHot ? ColaItemBalance.HotDamageBonus : 0),
                own.Stamp ? ColaItemBalance.StampRewardBonus : 0, own.Spicy ? ColaItemBalance.SpicyMultiplier : 1);
        }

        public ItemActionModifiers CaptureAction(bool boss)
        {
            var modifiers = PreviewAction(boss);
            var own = EffectsFor(boss);
            own.Clear();
            return modifiers;
        }

        public void OnBottleReplaced()
        {
            bottleHot = false;
            playerShopUsed = bossShopUsed = false;
            DrawBottleMenu();
            var sheet = GetComponent<ColaOrderSheet>();
            if (sheet) { sheet.Close(); sheet.RefreshAvailability(); }
        }

        private void DrawBottleMenu()
        {
            // Menu draws never consume the pressure/outcome or boss-decision RNG.
            menuRandom ??= new System.Random();
            var pool = new ColaItemId[ColaItemCatalog.All.Count];
            for (int i = 0; i < pool.Length; i++) pool[i] = ColaItemCatalog.All[i].Id;
            bottleMenu.Clear();
            for (int i = 0; i < Mathf.Min(MenuSize, pool.Length); i++)
            {
                int selected = menuRandom.Next(i, pool.Length);
                (pool[i], pool[selected]) = (pool[selected], pool[i]);
                bottleMenu.Add(pool[i]);
            }
            bottleMenu.Sort((a, b) => ((int)a).CompareTo((int)b));
        }

        public void ResetEffects()
        {
            PresentationReset?.Invoke();
            bottleHot = false;
            playerEffects.Clear();
            bossEffects.Clear();
            BossNotice = "";
            BossNoticeUntil = 0;
        }

        public void ResetBossInventory()
        {
            Array.Clear(bossInventory, 0, bossInventory.Length);
            GiveStartingItems(bossInventory);
            InventoryChanged?.Invoke();
        }

        private void GiveStartingItems(ColaItemId?[] owned)
        {
            itemRandom ??= new System.Random();
            // At full health a pizza cannot help with the opening decision; it remains in bell orders.
            var starting = ColaItemCatalog.DrawRandom(itemRandom, 2, ColaItemId.PizzaSlice);
            for (int i = 0; i < starting.Length; i++) owned[i] = starting[i];
        }

        public void ResetRun(int? seed = null)
        {
            menuRandom = seed.HasValue ? new System.Random(seed.Value ^ 0x6C0A) : new System.Random();
            itemRandom = seed.HasValue ? new System.Random(seed.Value ^ 0x28D7) : new System.Random();
            bottleMenu.Clear();
            Array.Clear(inventory, 0, inventory.Length);
            Array.Clear(bossInventory, 0, bossInventory.Length);
            GiveStartingItems(inventory);
            GiveStartingItems(bossInventory);
            ResetEffects();
            BossPurchaseCount = BossUseCount = 0;
            LastBossOrderCost = LastBossOrderCount = 0;
            playerShopUsed = bossShopUsed = false;
            LastBossUsed = null;
            LastBossPurchased = null;
            InventoryChanged?.Invoke();
        }

        public string ActiveEffectsText => EffectsText(false);
        public string EffectsText(bool boss)
        {
                var own = EffectsFor(boss);
                var text = new StringBuilder();
                if (own.Stamp) text.Append("안전 보상 +").Append(ColaItemBalance.StampRewardBonus).Append(" · ");
                if (own.Syrup) text.Append("시럽 +").Append(ColaItemBalance.SyrupBonus).Append(" · ");
                if (own.Spicy) text.Append("압력 ×").Append(ColaItemBalance.SpicyMultiplier).Append(" · ");
                if (bottleHot) text.Append("이 콜라 피해 +").Append(ColaItemBalance.HotDamageBonus).Append(" · ");
                string effects = text.Length > 3 ? text.ToString(0, text.Length - 3) : "대기 효과 없음";
                return effects;
        }
    }
}
