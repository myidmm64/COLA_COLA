using System;
using System.Collections.Generic;

namespace ColaCola.Core
{
    public enum ColaItemId
    {
        IceCup = 0, FizzCandy = 1,
        [Obsolete("Carbonation syrup was removed from the game.")] CarbonationSyrup = 2,
        QueueTicket = 3, Bill, SpicyOrder,
        SafetyCap, PizzaSlice, HotSauce, DoubleStamp,
        // Reserved ID for old Editor diagnostics. Not a catalog item or a grantable item.
        [Obsolete("Tip envelope was removed from the game.")] TipEnvelope = 10,
        BottleOpener = 11, Straw, SodaWater, CoinCup, SwapTongs,
        SaltPacket, CoolingPack, TokenPunch, FreshLid, ExchangeCoupon
    }

    public sealed class ColaItemDefinition
    {
        public ColaItemId Id { get; }
        private readonly string name;
        public string Name => ColaLocalization.Get("item." + Id + ".name", name);
        private readonly string effect;
        public string Effect => ColaLocalization.Get("item." + Id + ".effect", effect);
        public int Price { get; }
        // Legacy authoring-tool weight; live stage draws use ColaItemCatalog.DropWeightFor.
        public int DropWeight { get; }
        public ColaItemDefinition(ColaItemId id, string name, int price, int dropWeight, string effect)
        { Id = id; this.name = name; Price = price; DropWeight = dropWeight; this.effect = effect; }
    }

    public static class ColaItemCatalog
    {
        private static readonly ColaItemDefinition[] Entries =
        {
            new(ColaItemId.IceCup, "얼음컵", 3, 6, $"압력 -{ColaItemBalance.IceReduction}."),
            new(ColaItemId.FizzCandy, "발포 캔디", 7, 10, $"상대의 다음 흔들기 압력 +{ColaItemBalance.CandyIncrease}. 중복 가능."),
            new(ColaItemId.QueueTicket, "취소 번호표", 3, 8, "토큰 +1. 상대 아이템 1개 제거."),
            new(ColaItemId.Bill, "계산서", 5, 5, "흔들지 않고 상대에게 차례를 넘김."),
            new(ColaItemId.SpicyOrder, "매운맛 주문서", 3, 8, $"상대의 다음 흔들기 폭발 확률 +{ColaItemBalance.SpicyProbabilityIncrease * 100:0}%p. 중복 가능."),
            new(ColaItemId.SafetyCap, "안전 마개", 4, 6, $"내 다음 흔들기 폭발 확률 -{ColaItemBalance.SafetyProbabilityReduction * 100:0}%p. 중복 가능. 한계 도달 시 100%."),
            new(ColaItemId.PizzaSlice, "피자 한 조각", 8, 3, "체력 +1."),
            new(ColaItemId.HotSauce, "핫소스", 3, 8, $"이 콜라의 양쪽 폭발 피해 +{ColaItemBalance.HotDamageBonus}."),
            new(ColaItemId.DoubleStamp, "더블 스탬프", 4, 7, $"내 다음 흔들기가 안전하면 +{ColaItemBalance.StampRewardBonus}토큰."),
            new(ColaItemId.BottleOpener, "병따개", 4, 8, $"이 콜라의 한계 압력 -{ColaItemBalance.OpenerReduction}."),
            new(ColaItemId.Straw, "빨대", 4, 7, $"토큰 +{ColaItemBalance.StrawTokens}. 상대 토큰을 그만큼 빼앗음."),
            new(ColaItemId.SodaWater, "탄산수", 5, 5, "압력을 한계의 절반으로 맞춤."),
            new(ColaItemId.CoinCup, "동전컵", 3, 4, $"토큰 +{ColaItemBalance.CoinCupTokens}."),
            new(ColaItemId.SwapTongs, "교환 집게", 5, 4, "상대 아이템 1개 획득. 없으면 무작위 획득."),
            new(ColaItemId.SaltPacket, "소금 봉지", 3, 6, $"상대의 다음 흔들기 압력 +{ColaItemBalance.SaltIncrease}. 중복 가능."),
            new(ColaItemId.CoolingPack, "보냉팩", 4, 5, $"내 다음 흔들기 압력 -{ColaItemBalance.CoolingReduction}."),
            new(ColaItemId.TokenPunch, "계산 펀치", 4, 5, $"상대 다음 흔들기 토큰 -{ColaItemBalance.PunchTokens}."),
            new(ColaItemId.FreshLid, "새 뚜껑", 4, 3, $"이 콜라의 한계 압력 +{ColaItemBalance.LidIncrease}."),
            new(ColaItemId.ExchangeCoupon, "교환권", 4, 4, "무작위 다른 아이템으로 교체.")
        };
        // Rows sum to 100. Weights are the first draw's percentages before exclusions.
        // Subsequent items in a batch are sampled without replacement and renormalized.
        private static readonly int[][] StageWeights =
        {
            new[] {18,28,10,0,0,18,4,0,14,0,0,0,8,0,0,0,0,0,0},
            new[] {10,22,8,5,0,14,3,0,10,0,5,0,6,0,11,6,0,0,0},
            new[] {8,18,7,4,0,11,3,7,8,6,4,3,4,0,7,6,4,0,0},
            new[] {6,16,5,4,10,9,2,7,6,6,4,2,3,2,5,6,3,4,0},
            new[] {5,14,5,4,11,10,2,8,6,6,4,2,2,2,5,5,3,3,3},
        };
        public static int DropWeightFor(ColaItemId id, int level)
        {
            int index = IndexOf(id);
            return index < 0 ? 0 : StageWeights[Math.Max(0, Math.Min(4, level - 1))][index];
        }
        public static IReadOnlyList<ColaItemDefinition> All { get; } = Array.AsReadOnly(Entries);
        // IDs 2 and 10 remain retired; catalog positions must not be confused with serialized IDs.
        public static int IndexOf(ColaItemId id)
        {
            for (int i = 0; i < Entries.Length; i++) if (Entries[i].Id == id) return i;
            return -1;
        }
        // Preserve v1 achievement bit positions when catalog entries are retired.
        public static int UsageBitFor(ColaItemId id) => 1 << ((int)id > 10 ? (int)id - 1 : (int)id);
        public static bool Contains(ColaItemId id) => IndexOf(id) >= 0;
        public static ColaItemId[] DrawRandom(Random random, int count, ColaItemId? excluded = null, int level = 1, bool allowHotSauce = true)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var pool = new List<ColaItemDefinition>(Entries);
            pool.RemoveAll(item => item.Id == excluded || (!allowHotSauce && item.Id == ColaItemId.HotSauce) || DropWeightFor(item.Id, level) == 0);
            var result = new ColaItemId[Math.Min(count, pool.Count)];
            for (int i = 0; i < result.Length; i++)
            {
                int total = 0;
                foreach (var item in pool) total += DropWeightFor(item.Id, level);
                int ticket = random.Next(total);
                for (int j = 0; j < pool.Count; j++)
                {
                    ticket -= DropWeightFor(pool[j].Id, level);
                    if (ticket >= 0) continue;
                    result[i] = pool[j].Id;
                    pool.RemoveAt(j);
                    break;
                }
            }
            return result;
        }
        public static ColaItemDefinition Get(ColaItemId id)
        {
            int index = IndexOf(id);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(id));
            return Entries[index];
        }
    }

    public readonly struct ItemActionModifiers
    {
        public readonly int PressureBonus, Damage, RewardBonus, PressureMultiplier, RewardPenalty;
        // Kept neutral for old Editor diagnostic sources; not effects in the current game.
        public int RewardMultiplier => 1;
        public readonly double ProbabilityReduction, ProbabilityIncrease;
        public bool SafeTip => false;
        public static ItemActionModifiers None => new(0, 1, 0, 1);
        public ItemActionModifiers(int pressureBonus, int damage, int rewardBonus, int pressureMultiplier, double probabilityReduction = 0, double probabilityIncrease = 0, int rewardPenalty = 0)
        {
            PressureBonus = pressureBonus; Damage = damage;
            RewardBonus = rewardBonus; PressureMultiplier = pressureMultiplier;
            ProbabilityReduction = probabilityReduction; ProbabilityIncrease = probabilityIncrease; RewardPenalty = rewardPenalty;
        }
    }
}
