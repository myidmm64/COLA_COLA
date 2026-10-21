using System;
using System.Collections.Generic;
using System.Text;
using ColaCola.Core;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Purchases, expandable physical inventories and one-use effects for the single-player duel.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaItemController : MonoBehaviour
    {
        public const int Capacity = 8;
        public const int MaxCapacity = 10;
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
        private readonly ColaItemId?[] inventory = new ColaItemId?[MaxCapacity];
        private readonly ColaItemId?[] bossInventory = new ColaItemId?[MaxCapacity];
        private readonly bool[] playerExchangeOrigin = new bool[MaxCapacity], bossExchangeOrigin = new bool[MaxCapacity];
        private bool[] ExchangeOriginFor(bool boss) => boss ? bossExchangeOrigin : playerExchangeOrigin;
        public int CapacityFor(bool boss = false) => match?.Progression?.CapacityFor(boss) ?? Capacity;
        public int WaiterCostFor(bool boss = false) => match?.Progression?.BellCostFor(boss) ?? WaiterCost;
        public int WaiterCountFor(bool boss = false) => match?.Progression?.BellCountFor(boss) ?? WaiterItemCount;
        private sealed class Effects
        {
            public bool Stamp, Chilled;
            public int SafetyStacks, SpicyStacks, IncomingPressure, RewardPenalty;
            public void Clear() { Stamp = Chilled = false; SafetyStacks = SpicyStacks = IncomingPressure = RewardPenalty = 0; }
        }
        private readonly Effects playerEffects = new(), bossEffects = new();
        private bool bottleHot, hotSauceDrawn;
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
        public bool SafetyReady => playerEffects.SafetyStacks > 0;
        public bool StampReady => playerEffects.Stamp;
        public bool SpicyReady => bossEffects.SpicyStacks > 0;
        // Old Editor diagnostics still compile; current effects have no tip/force/skip state.
        public bool DoubleReady => StampReady;
        public bool TipReady => false;
        public bool SyrupReady => false;
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
            match.State == ColaDuelController.DuelState.Playing && !ColaFrontEnd.BlocksGameplay && match.interaction && match.interaction.isActiveAndEnabled &&
            match.interaction.InputEnabled && !(match.Progression?.BlocksGameplay ?? false);
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

        public ColaItemId? ItemAt(int slot, bool boss = false) => slot >= 0 && slot < CapacityFor(boss) ? InventoryFor(boss)[slot] : null;

        public bool CanPurchase(ColaItemId id, bool boss = false)
        {
            return ColaItemCatalog.Contains(id) && CanActFor(boss) && OrderAvailable(boss) && InventoryCountFor(boss) < CapacityFor(boss);
        }

        public bool TryPurchase(ColaItemId id, bool boss = false)
            => TryPurchaseBatch(new[] { id }, boss);

        public bool CanPurchaseBatch(IReadOnlyList<ColaItemId> order, bool boss = false)
        {
            if (!CanActFor(boss) || !OrderAvailable(boss) || order == null || order.Count == 0 ||
                order.Count > CapacityFor(boss) - InventoryCountFor(boss)) return false;
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
            match.Progression?.MarkDebugAssisted();
            if (boss) bossShopUsed = true;
            else playerShopUsed = true;
            var owned = InventoryFor(boss);
            int next = 0;
            for (int i = 0; i < CapacityFor(boss); i++)
                if (!owned[i].HasValue && next < purchase.Length)
                { owned[i] = purchase[next++]; ExchangeOriginFor(boss)[i] = false; }
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

        public bool CanInspectItems => MatchActive && !match.interaction.IsPlayerShaking;
        public bool CanInspectWaiter => MatchActive;
        public bool CanOrderWaiter(bool boss = false)
            => MatchActive && (!boss || CanBossAct) && CapacityFor(boss) - InventoryCountFor(boss) >= WaiterCountFor(boss) &&
                match.CanAffordTokens(WaiterCostFor(boss), boss);

        public bool TryOrderWaiter(bool boss = false)
        {
            int count = WaiterCountFor(boss), cost = WaiterCostFor(boss);
            if (!CanOrderWaiter(boss)) return Reject($"벨 주문: {cost}토큰과 빈 칸 {count}개가 필요합니다.", boss);
            if (!match.TrySpendWaiterTokens(boss)) return false;
            var purchase = DrawRandomItems(count);
            var owned = InventoryFor(boss);
            var deliveredSlots = new int[purchase.Length];
            int next = 0;
            for (int i = 0; i < CapacityFor(boss) && next < purchase.Length; i++)
                if (!owned[i].HasValue)
                {
                    deliveredSlots[next] = i;
                    owned[i] = purchase[next++];
                    ExchangeOriginFor(boss)[i] = false;
                }
            if (boss)
            {
                BossPurchaseCount++;
                LastBossPurchased = purchase[purchase.Length - 1];
                LastBossOrderCost = cost;
                LastBossOrderCount = count;
                AnnounceBoss($"벨 주문: 랜덤 아이템 {count}개");
            }
            else match.SetActionMessage($"벨 주문: 랜덤 아이템 {count}개를 받았습니다.");
            match.Progression?.OnBell(boss, purchase.Length);
            InventoryChanged?.Invoke();
            WaiterOrdered?.Invoke(deliveredSlots, boss);
            return true;
        }

        private ColaItemId[] DrawRandomItems(int count)
        {
            // Shared item draws are independent of pressure rolls, AI decisions and the old test-menu RNG.
            itemRandom ??= new System.Random();
            return DrawNaturalItems(count);
        }

        private bool HoldsHotSauce() => Array.IndexOf(inventory, (ColaItemId?)ColaItemId.HotSauce) >= 0 ||
            Array.IndexOf(bossInventory, (ColaItemId?)ColaItemId.HotSauce) >= 0;
        public bool CanDrawHotSauce => !hotSauceDrawn && !bottleHot && !HoldsHotSauce();
        private ColaItemId[] DrawNaturalItems(int count, ColaItemId? excluded = null)
        {
            itemRandom ??= new System.Random();
            var drawn = ColaItemCatalog.DrawRandom(itemRandom, count, excluded, match.Level, CanDrawHotSauce);
            if (Array.IndexOf(drawn, ColaItemId.HotSauce) >= 0) hotSauceDrawn = true;
            return drawn;
        }

        public bool CanUseSlot(int slot, bool boss = false)
        {
            if (IsUsingItem || !CanActFor(boss) || !ItemAt(slot, boss).HasValue || !ColaItemCatalog.Contains(ItemAt(slot, boss).Value)) return false;
            if (inventoryPresentation && inventoryPresentation.IsSlotArriving(slot, boss)) return false;
            var sheet = GetComponent<ColaOrderSheet>();
            if (!boss && sheet && sheet.BlocksGameplay) return false;
            var own = EffectsFor(boss);
            return ItemAt(slot, boss).Value switch
            {
                ColaItemId.IceCup => match.interaction.Model.Pressure > 0,
                ColaItemId.FizzCandy => true,
                ColaItemId.QueueTicket => true,
                ColaItemId.SpicyOrder => true,
                ColaItemId.SafetyCap => true,
                ColaItemId.PizzaSlice => boss ? match.BossHealth < match.BossMaxHealth : match.PlayerHealth < match.playerMaxHealth,
                ColaItemId.HotSauce => !bottleHot,
                ColaItemId.DoubleStamp => !own.Stamp,
                ColaItemId.BottleOpener => match.interaction.Model.Limit > 1,
                ColaItemId.Straw => true,
                ColaItemId.SodaWater => match.interaction.Model.Pressure != match.interaction.Model.Limit / 2,
                ColaItemId.CoinCup => (boss ? match.BossTokens : match.PlayerTokens) < 9999,
                ColaItemId.SwapTongs => true,
                ColaItemId.SaltPacket => true,
                ColaItemId.CoolingPack => !own.Chilled,
                ColaItemId.TokenPunch => true,
                ColaItemId.FreshLid => match.interaction.Model.Limit < 999,
                ColaItemId.ExchangeCoupon => true,
                _ => true
            };
        }

        public bool TryUseSlot(int slot, bool boss = false)
        {
            if (!CanActFor(boss) || !ItemAt(slot, boss).HasValue) return false;
            if (!CanUseSlot(slot, boss)) return Reject("지금은 사용할 수 없습니다. 아이템은 유지됩니다.", boss);
            var owned = InventoryFor(boss);
            var id = owned[slot].Value;
            bool fromExchange = ExchangeOriginFor(boss)[slot];
            int pressureBefore = match.interaction.Model.Pressure, limitBefore = match.interaction.Model.Limit;
            var own = EffectsFor(boss);
            var target = EffectsFor(!boss);
            bool pass = false;
            ColaItemId? replacement = null;
            switch (id)
            {
                case ColaItemId.IceCup:
                    match.interaction.Model.AdjustPressure(-ColaItemBalance.IceReduction);
                    break;
                case ColaItemId.FizzCandy:
                    target.IncomingPressure = (int)Math.Min(int.MaxValue, (long)target.IncomingPressure + ColaItemBalance.CandyIncrease);
                    break;
                case ColaItemId.QueueTicket:
                    TryCancelRandomItem(!boss);
                    match.GrantPassiveTokens(ColaItemBalance.TicketTokens, boss);
                    break;
                case ColaItemId.Bill: pass = true; break;
                case ColaItemId.SpicyOrder:
                    target.SpicyStacks = (int)Math.Min(int.MaxValue, (long)target.SpicyStacks + 1);
                    break;
                case ColaItemId.SafetyCap:
                    own.SafetyStacks = (int)Math.Min(int.MaxValue, (long)own.SafetyStacks + 1);
                    break;
                case ColaItemId.PizzaSlice:
                    if (!match.TryHeal(boss)) return false;
                    break;
                case ColaItemId.HotSauce:
                    bottleHot = hotSauceDrawn = true;
                    break;
                case ColaItemId.DoubleStamp:
                    own.Stamp = true;
                    break;
                case ColaItemId.BottleOpener:
                    match.interaction.Model.AdjustLimit(-ColaItemBalance.OpenerReduction);
                    break;
                case ColaItemId.Straw:
                    int strawShortfall = ColaItemBalance.StrawTokens - Mathf.Min(ColaItemBalance.StrawTokens, boss ? match.PlayerTokens : match.BossTokens);
                    match.TryTransferTokens(ColaItemBalance.StrawTokens, boss);
                    // Transfer supplies the available part; fill the remainder to guarantee two.
                    match.GrantPassiveTokens(strawShortfall, boss);
                    break;
                case ColaItemId.SodaWater:
                    var model = match.interaction.Model;
                    model.AdjustPressure(model.Limit / 2 - model.Pressure);
                    break;
                case ColaItemId.CoinCup:
                    if (!match.TryGrantItemTokens(ColaItemBalance.CoinCupTokens, boss)) return false;
                    break;
                case ColaItemId.SwapTongs:
                    replacement = TakeOrDrawItem(boss);
                    break;
                case ColaItemId.SaltPacket:
                    target.IncomingPressure = (int)Math.Min(int.MaxValue, (long)target.IncomingPressure + ColaItemBalance.SaltIncrease);
                    break;
                case ColaItemId.CoolingPack:
                    own.Chilled = true;
                    break;
                case ColaItemId.TokenPunch:
                    target.RewardPenalty = (int)Math.Min(int.MaxValue, (long)target.RewardPenalty + ColaItemBalance.PunchTokens);
                    break;
                case ColaItemId.FreshLid:
                    match.interaction.Model.AdjustLimit(ColaItemBalance.LidIncrease);
                    break;
                case ColaItemId.ExchangeCoupon:
                    var rerolled = DrawNaturalItems(1, ColaItemId.ExchangeCoupon);
                    if (rerolled.Length == 0) return false;
                    replacement = rerolled[0];
                    break;
                default: return false;
            }
            owned[slot] = null;
            ExchangeOriginFor(boss)[slot] = false;
            match.Progression?.OnItemUsed(id, boss, fromExchange, pressureBefore, limitBefore);
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
            if (replacement.HasValue) replacements.Add(new Replacement { Boss = boss, Slot = slot, Item = replacement.Value, FromExchange = id == ColaItemId.ExchangeCoupon });
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
            var occupied = new int[MaxCapacity];
            int count = 0;
            for (int i = 0; i < owned.Length; i++) if (owned[i].HasValue) occupied[count++] = i;
            if (count == 0) return false;
            itemRandom ??= new System.Random();
            int target = occupied[itemRandom.Next(count)];
            owned[target] = null;
            ExchangeOriginFor(boss)[target] = false;
            return true;
        }

        private ColaItemId? TakeOrDrawItem(bool boss)
        {
            var other = InventoryFor(!boss);
            var slots = new List<int>();
            for (int i = 0; i < CapacityFor(!boss); i++) if (other[i].HasValue) slots.Add(i);
            if (slots.Count == 0) { var drawn = DrawNaturalItems(1); return drawn.Length > 0 ? drawn[0] : (ColaItemId?)null; }
            itemRandom ??= new System.Random();
            int slot = slots[itemRandom.Next(slots.Count)];
            var item = other[slot]; other[slot] = null; ExchangeOriginFor(!boss)[slot] = false;
            return item;
        }

        private sealed class Replacement { public bool Boss, FromExchange; public int Slot; public ColaItemId Item; }
        private readonly List<Replacement> replacements = new();
        internal void FlushReplacements()
        {
            if (ColaFrontEnd.BlocksGameplay || IsUsingItem) return;
            for (int i = replacements.Count - 1; i >= 0; i--)
            {
                var entry = replacements[i];
                if (IsReceivingItems(entry.Boss)) continue;
                var bag = InventoryFor(entry.Boss);
                int slot = !bag[entry.Slot].HasValue ? entry.Slot : Array.FindIndex(bag, x => !x.HasValue);
                if (slot < 0 || slot >= CapacityFor(entry.Boss)) continue;
                bag[slot] = entry.Item; ExchangeOriginFor(entry.Boss)[slot] = entry.FromExchange;
                replacements.RemoveAt(i); InventoryChanged?.Invoke();
            }
        }

        public bool CanReturnSlot(int slot, bool boss = false)
        {
            if (!ItemAt(slot, boss).HasValue) return false;
            if (boss) return CanBossAct && !IsUsingItem && !IsReceivingItems(true);
            // Returning a stored player item follows inspection availability, including the opponent's turn.
            return CanInspectItems && (!inventoryPresentation || !inventoryPresentation.IsSlotArriving(slot, false));
        }

        public bool TryReturnItem(int slot, bool boss = false)
        {
            if (!CanReturnSlot(slot, boss)) return false;
            InventoryFor(boss)[slot] = null; ExchangeOriginFor(boss)[slot] = false;
            match.GrantPassiveTokens(ColaItemBalance.ReturnTokens, boss);
            ColaTabletopAudio.Cue(match.interaction, "Tokens", transform.position, .32f);
            InventoryChanged?.Invoke(); return true;
        }

        private void AnnounceBoss(string action)
        {
            BossNotice = "보스 " + action;
            BossNoticeUntil = ColaGameClock.Time + 4;
            match.SetActionMessage(BossNotice);
        }

        public ItemActionModifiers PreviewAction(bool boss)
        {
            var own = EffectsFor(boss);
            return new ItemActionModifiers(own.IncomingPressure - (own.Chilled ? ColaItemBalance.CoolingReduction : 0),
                1 + (bottleHot ? ColaItemBalance.HotDamageBonus : 0),
                (own.Stamp ? ColaItemBalance.StampRewardBonus : 0) + (match.Progression?.SavingsBonusFor(boss) ?? 0), 1,
                own.SafetyStacks * ColaItemBalance.SafetyProbabilityReduction + (match.Progression?.ProbabilityReductionFor(boss) ?? 0),
                own.SpicyStacks * ColaItemBalance.SpicyProbabilityIncrease, own.RewardPenalty);
        }

        public bool HasCooling(bool boss) => EffectsFor(boss).Chilled;
        public int IncomingPressureFor(bool boss) => EffectsFor(boss).IncomingPressure;
        public int CoolingReductionFor(bool boss) => EffectsFor(boss).Chilled ? ColaItemBalance.CoolingReduction : 0;

        public string EffectDescription(ColaItemId id, bool boss = false)
        {
            return ColaItemCatalog.Get(id).Effect;
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
            match.Progression?.OnBottleStarted();
            bottleHot = false;
            hotSauceDrawn = HoldsHotSauce();
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
            replacements.Clear();
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
            Array.Clear(bossExchangeOrigin, 0, bossExchangeOrigin.Length);
            hotSauceDrawn = false;
            GiveStartingItems(bossInventory, ColaBossTactics.StartingItemsForStage(match.Level));
            InventoryChanged?.Invoke();
        }

        private void GiveStartingItems(ColaItemId?[] owned, int count = 2)
        {
            itemRandom ??= new System.Random();
            // At full health a pizza cannot help with the opening decision; it remains in bell orders.
            var starting = DrawNaturalItems(Mathf.Clamp(count, 0, owned.Length), ColaItemId.PizzaSlice);
            for (int i = 0; i < starting.Length; i++) owned[i] = starting[i];
        }

        public void ResetRun(int? seed = null)
        {
            menuRandom = seed.HasValue ? new System.Random(seed.Value ^ 0x6C0A) : new System.Random();
            itemRandom = seed.HasValue ? new System.Random(seed.Value ^ 0x28D7) : new System.Random();
            bottleMenu.Clear();
            Array.Clear(inventory, 0, inventory.Length);
            Array.Clear(bossInventory, 0, bossInventory.Length);
            Array.Clear(playerExchangeOrigin, 0, playerExchangeOrigin.Length);
            Array.Clear(bossExchangeOrigin, 0, bossExchangeOrigin.Length);
            hotSauceDrawn = false;
            GiveStartingItems(inventory);
            GiveStartingItems(bossInventory, ColaBossTactics.StartingItemsForStage(match.Level));
            ResetEffects();
            BossPurchaseCount = BossUseCount = 0;
            LastBossOrderCost = LastBossOrderCount = 0;
            playerShopUsed = bossShopUsed = false;
            LastBossUsed = null;
            LastBossPurchased = null;
            InventoryChanged?.Invoke();
        }

        public void RefreshCapacity() => InventoryChanged?.Invoke();

        internal bool TryGrantPassiveItem(ColaItemId id, bool boss = false)
        {
            if (!ColaItemCatalog.Contains(id) || (id == ColaItemId.HotSauce && !CanDrawHotSauce)) return false;
            var inventory = InventoryFor(boss);
            for (int i = 0; i < CapacityFor(boss); i++)
                if (!inventory[i].HasValue)
                {
                    inventory[i] = id; ExchangeOriginFor(boss)[i] = false;
                    if (id == ColaItemId.HotSauce) hotSauceDrawn = true;
                    InventoryChanged?.Invoke(); return true;
                }
            return false;
        }

        internal bool TryGrantPassiveRandomItem(bool boss)
        {
            var inventory = InventoryFor(boss);
            for (int i = 0; i < CapacityFor(boss); i++)
                if (!inventory[i].HasValue)
                {
                    var drawn = DrawNaturalItems(1);
                    if (drawn.Length == 0) return false;
                    inventory[i] = drawn[0]; ExchangeOriginFor(boss)[i] = false;
                    InventoryChanged?.Invoke(); return true;
                }
            return false;
        }

        internal void AddIncomingPressure(bool boss, int amount)
        {
            EffectsFor(boss).IncomingPressure += Mathf.Max(0, amount);
            match.interaction.RefreshPrediction();
        }

        public void ClearAllItems()
        {
            replacements.Clear();
            match.Progression?.ClearPendingItemGrants();
            CancelItemUsePresentation();
            PresentationReset?.Invoke();
            Array.Clear(inventory, 0, inventory.Length);
            Array.Clear(bossInventory, 0, bossInventory.Length);
            Array.Clear(playerExchangeOrigin, 0, playerExchangeOrigin.Length);
            Array.Clear(bossExchangeOrigin, 0, bossExchangeOrigin.Length);
            GetComponent<ColaOrderSheet>()?.ClearCart();
            InventoryChanged?.Invoke();
        }

        public string ActiveEffectsText => EffectsText(false);
        public string EffectsText(bool boss)
        {
                var own = EffectsFor(boss);
                var text = new StringBuilder();
                void AddEffect(string key, string fallback, object amount) => text.Append(string.Format(ColaLocalization.Get(key, fallback), amount)).Append(" · ");
                if (own.Stamp) AddEffect("ui.effect_safe", "안전 보상 +{0}", ColaItemBalance.StampRewardBonus);
                if (own.SafetyStacks > 0) AddEffect("ui.effect_burst", "다음 폭발 확률 {0}%p", (-own.SafetyStacks * ColaItemBalance.SafetyProbabilityReduction * 100).ToString("0"));
                if (own.SpicyStacks > 0) AddEffect("ui.effect_burst", "다음 폭발 확률 {0}%p", "+" + (own.SpicyStacks * ColaItemBalance.SpicyProbabilityIncrease * 100).ToString("0"));
                if (own.IncomingPressure > 0) AddEffect("ui.effect_pressure", "다음 압력 {0}", "+" + own.IncomingPressure);
                if (own.Chilled) AddEffect("ui.effect_pressure", "다음 압력 {0}", -ColaItemBalance.CoolingReduction);
                if (!boss && (match.Progression?.PassiveProbabilityReduction ?? 0) > 0)
                    AddEffect("ui.effect_burst", "다음 폭발 확률 {0}%p", (-ColaItemBalance.LowHealthProbabilityReduction * 100).ToString("0"));
                if (bottleHot) AddEffect("ui.effect_damage", "이 콜라 피해 +{0}", ColaItemBalance.HotDamageBonus);
                string effects = text.Length > 3 ? text.ToString(0, text.Length - 3) : ColaLocalization.Get("ui.no_effects", "대기 효과 없음");
                return effects;
        }
    }
}
