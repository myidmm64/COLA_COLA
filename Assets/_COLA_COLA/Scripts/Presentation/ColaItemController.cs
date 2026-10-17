using System;
using System.Text;
using ColaCola.Core;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Purchases, six physical inventory slots and one-use effects for the single-player duel.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaItemController : MonoBehaviour
    {
        public const int Capacity = 6;
        public ColaDuelController match;
        private readonly ColaItemId?[] inventory = new ColaItemId?[Capacity];
        private readonly ColaItemId?[] bossInventory = new ColaItemId?[Capacity];
        private sealed class Effects
        {
            public bool Safety, Double, Tip, Syrup, ForceThree, Skip;
            public void Clear() { Safety = Double = Tip = Syrup = ForceThree = Skip = false; }
        }
        private readonly Effects playerEffects = new(), bossEffects = new();
        private bool playerShopUsed, bossShopUsed;
        private bool playerOrderWindow, bossOrderWindow;
        public bool ShopUsed(bool boss = false) => boss ? bossShopUsed : playerShopUsed;
        // Each actor has until their first committed action on this bottle to place one order.
        public bool OrderAvailable(bool boss = false) => (boss ? bossOrderWindow : playerOrderWindow) && !ShopUsed(boss);
        public void EndOrderWindow(bool boss)
        {
            if (boss) bossOrderWindow = false;
            else playerOrderWindow = false;
        }
        public event Action InventoryChanged;
        public bool SafetyReady => playerEffects.Safety;
        public bool DoubleReady => playerEffects.Double;
        public bool TipReady => playerEffects.Tip;
        public bool SyrupReady => bossEffects.Syrup;
        public bool ForceThreeReady => bossEffects.ForceThree;
        public bool SkipBossReady => bossEffects.Skip;
        public bool HotSauceReady { get; private set; }
        public string BossNotice { get; private set; } = "";
        public float BossNoticeUntil { get; private set; }
        public int BossPurchaseCount { get; private set; }
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
        public int ForcedStrength(bool boss) => EffectsFor(boss).ForceThree ? 3 : 0;
        public bool HasSkip(bool boss) => EffectsFor(boss).Skip;
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
            display.Bind(items);
            var sheet = match.GetComponent<ColaOrderSheet>();
            if (!sheet) sheet = match.gameObject.AddComponent<ColaOrderSheet>();
            sheet.Bind(items);
            var ai = match.GetComponent<ColaBossItemAI>();
            if (!ai) ai = match.gameObject.AddComponent<ColaBossItemAI>();
            ai.Bind(items);
            return items;
        }

        public ColaItemId? ItemAt(int slot, bool boss = false) => slot >= 0 && slot < Capacity ? InventoryFor(boss)[slot] : null;

        public bool CanPurchase(ColaItemId id, bool boss = false)
        {
            return Enum.IsDefined(typeof(ColaItemId), id) && CanActFor(boss) && OrderAvailable(boss) && InventoryCountFor(boss) < Capacity &&
                (boss ? match.BossTokens : match.PlayerTokens) >= ColaItemCatalog.Get(id).Price;
        }

        public bool TryPurchase(ColaItemId id, bool boss = false)
        {
            if (!Enum.IsDefined(typeof(ColaItemId), id) || !CanActFor(boss)) return false;
            if (!OrderAvailable(boss)) return Reject("주문 시간이 끝났습니다. 새 콜라에서 첫 행동 전에 주문하세요.", boss);
            if (InventoryCountFor(boss) == Capacity) return Reject("아이템 공간이 가득 찼습니다.", boss);
            var definition = ColaItemCatalog.Get(id);
            if (!match.TrySpendTokens(definition.Price, boss)) return Reject("주문할 토큰이 부족합니다.", boss);
            if (boss) bossShopUsed = true;
            else playerShopUsed = true;
            EndOrderWindow(boss);
            var owned = InventoryFor(boss);
            for (int i = 0; i < Capacity; i++)
                if (!owned[i].HasValue) { owned[i] = id; break; }
            if (boss) { BossPurchaseCount++; LastBossPurchased = id; AnnounceBoss("주문: " + definition.Name); }
            else match.SetActionMessage(definition.Name + " 주문 완료. 테이블의 큐브를 눌러 사용.");
            InventoryChanged?.Invoke();
            return true;
        }

        public bool CanUseSlot(int slot, bool boss = false)
        {
            if (!CanActFor(boss) || !ItemAt(slot, boss).HasValue) return false;
            var sheet = GetComponent<ColaOrderSheet>();
            if (!boss && sheet && sheet.BlocksGameplay) return false;
            var own = EffectsFor(boss);
            var target = EffectsFor(!boss);
            return ItemAt(slot, boss).Value switch
            {
                ColaItemId.IceCup => match.interaction.Model.Pressure > 0,
                ColaItemId.CarbonationSyrup => !target.Syrup,
                ColaItemId.QueueTicket => !target.Skip,
                ColaItemId.SpicyOrder => !target.ForceThree,
                ColaItemId.SafetyCap => !own.Safety,
                ColaItemId.PizzaSlice => boss ? match.BossHealth < match.BossMaxHealth : match.PlayerHealth < match.playerMaxHealth,
                ColaItemId.HotSauce => !HotSauceReady,
                ColaItemId.DoubleStamp => !own.Double,
                ColaItemId.TipEnvelope => !own.Tip,
                _ => true
            };
        }

        public bool TryUseSlot(int slot, bool boss = false)
        {
            if (!CanActFor(boss) || !ItemAt(slot, boss).HasValue) return false;
            if (!CanUseSlot(slot, boss)) return Reject("현재 사용할 수 없는 효과입니다. 큐브는 유지됩니다.", boss);
            var owned = InventoryFor(boss);
            var id = owned[slot].Value;
            var own = EffectsFor(boss);
            var target = EffectsFor(!boss);
            bool pass = false;
            switch (id)
            {
                case ColaItemId.IceCup:
                    match.interaction.Model.AdjustPressure(-3);
                    break;
                case ColaItemId.FizzCandy:
                    match.interaction.Model.AdjustPressure(4);
                    pass = true;
                    break;
                case ColaItemId.CarbonationSyrup:
                    target.Syrup = true;
                    break;
                case ColaItemId.QueueTicket:
                    target.Skip = true;
                    break;
                case ColaItemId.Bill: pass = true; break;
                case ColaItemId.SpicyOrder:
                    target.ForceThree = true;
                    break;
                case ColaItemId.SafetyCap:
                    own.Safety = true;
                    break;
                case ColaItemId.PizzaSlice:
                    if (!match.TryHeal(boss)) return false;
                    break;
                case ColaItemId.HotSauce:
                    HotSauceReady = true;
                    break;
                case ColaItemId.DoubleStamp:
                    own.Double = true;
                    break;
                case ColaItemId.TipEnvelope:
                    own.Tip = true;
                    break;
                default: return false;
            }
            owned[slot] = null;
            InventoryChanged?.Invoke();
            if (boss) { BossUseCount++; LastBossUsed = id; AnnounceBoss("사용: " + ColaItemCatalog.Get(id).Name + " — " + ColaItemCatalog.Get(id).Effect); }
            else match.SetActionMessage(ColaItemCatalog.Get(id).Name + " 사용. " + ColaItemCatalog.Get(id).Effect);
            match.interaction.RefreshPrediction();
            if (pass)
            {
                if (boss) match.interaction.PassBossTurn();
                else match.interaction.PassPlayerTurn();
            }
            return true;
        }

        private bool Reject(string reason, bool boss) { if (!boss) match.SetActionMessage(reason); return false; }
        private void AnnounceBoss(string action)
        {
            BossNotice = "보스 " + action;
            BossNoticeUntil = Time.unscaledTime + 4;
            match.SetActionMessage(BossNotice);
        }

        public ItemActionModifiers PreviewAction(bool boss)
        {
            var own = EffectsFor(boss);
            return new ItemActionModifiers(own.Syrup ? 2 : 0, own.Safety ? .30d : 0,
                HotSauceReady ? 2 : 1, own.Double ? 2 : 1, own.Tip);
        }

        public ItemActionModifiers CaptureAction(bool boss)
        {
            var modifiers = PreviewAction(boss);
            var own = EffectsFor(boss);
            own.Safety = own.Double = own.Tip = own.Syrup = own.ForceThree = false;
            return modifiers;
        }

        public bool ConsumeBossSkip() => ConsumeSkip(true);
        public bool ConsumeSkip(bool boss)
        {
            var target = EffectsFor(boss);
            if (!target.Skip) return false;
            target.Skip = false;
            EndOrderWindow(boss);
            if (boss) match.SetActionMessage("대기 번호표: 상대 턴을 건너뜁니다. 내 턴.");
            else AnnounceBoss("대기 번호표: 플레이어 턴을 건너뜁니다.");
            return true;
        }

        public void OnBottleReplaced()
        {
            HotSauceReady = false;
            playerShopUsed = bossShopUsed = false;
            playerOrderWindow = bossOrderWindow = true;
            var sheet = GetComponent<ColaOrderSheet>();
            if (sheet) sheet.Close();
        }

        public void ResetEffects()
        {
            playerEffects.Clear();
            bossEffects.Clear();
            HotSauceReady = false;
            BossNotice = "";
            BossNoticeUntil = 0;
        }

        public void ResetBossInventory()
        {
            Array.Clear(bossInventory, 0, bossInventory.Length);
            GiveStartingItems(bossInventory);
            InventoryChanged?.Invoke();
        }

        private static void GiveStartingItems(ColaItemId?[] owned)
        {
            owned[0] = ColaItemId.IceCup;
            owned[1] = ColaItemId.SafetyCap;
        }

        public void ResetRun()
        {
            Array.Clear(inventory, 0, inventory.Length);
            Array.Clear(bossInventory, 0, bossInventory.Length);
            GiveStartingItems(inventory);
            GiveStartingItems(bossInventory);
            ResetEffects();
            BossPurchaseCount = BossUseCount = 0;
            playerShopUsed = bossShopUsed = false;
            playerOrderWindow = bossOrderWindow = true;
            LastBossUsed = null;
            LastBossPurchased = null;
            InventoryChanged?.Invoke();
        }

        public string ActiveEffectsText => EffectsText(false);
        public string EffectsText(bool boss)
        {
                var own = EffectsFor(boss);
                var text = new StringBuilder();
                if (own.Safety) text.Append("안전 마개 · ");
                if (own.Double) text.Append("더블 스탬프 · ");
                if (own.Tip) text.Append("팁 봉투 · ");
                if (own.Syrup) text.Append("시럽 +2 · ");
                if (own.ForceThree) text.Append("강도 3 고정 · ");
                if (own.Skip) text.Append("다음 턴 건너뜀 · ");
                if (HotSauceReady) text.Append("핫소스 · ");
                return text.Length > 3 ? text.ToString(0, text.Length - 3) : "대기 효과 없음";
        }
    }
}
