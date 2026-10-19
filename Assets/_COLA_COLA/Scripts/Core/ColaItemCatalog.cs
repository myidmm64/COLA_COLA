using System;
using System.Collections.Generic;

namespace ColaCola.Core
{
    public enum ColaItemId
    {
        IceCup, FizzCandy, CarbonationSyrup, QueueTicket, Bill, SpicyOrder,
        SafetyCap, PizzaSlice, HotSauce, DoubleStamp,
        // Reserved ID for old Editor diagnostics. Not a catalog item or a grantable item.
        [Obsolete("Tip envelope was removed from the game.")] TipEnvelope = 10,
        BottleOpener = 11, Straw, SodaWater, CoinCup, SwapTongs
    }

    public sealed class ColaItemDefinition
    {
        public ColaItemId Id { get; }
        public string Name { get; }
        public string Effect { get; }
        public int Price { get; }
        public int DropWeight { get; }
        public ColaItemDefinition(ColaItemId id, string name, int price, int dropWeight, string effect)
        { Id = id; Name = name; Price = price; DropWeight = dropWeight; Effect = effect; }
    }

    public static class ColaItemCatalog
    {
        private static readonly ColaItemDefinition[] Entries =
        {
            new(ColaItemId.IceCup, "얼음컵", 3, 6, $"압력 -{ColaItemBalance.IceReduction}."),
            new(ColaItemId.FizzCandy, "발포 캔디", 7, 10, $"압력 +{ColaItemBalance.CandyIncrease}."),
            new(ColaItemId.CarbonationSyrup, "탄산 시럽", 4, 11, $"상대의 다음 흔들기 압력 +{ColaItemBalance.SyrupBonus}."),
            new(ColaItemId.QueueTicket, "취소 번호표", 3, 8, "상대 아이템 1개를 무작위로 제거."),
            new(ColaItemId.Bill, "계산서", 5, 5, "흔들지 않고 상대에게 차례를 넘김."),
            new(ColaItemId.SpicyOrder, "매운맛 주문서", 3, 8, $"상대의 다음 흔들기 압력 {ColaItemBalance.SpicyMultiplier}배."),
            new(ColaItemId.SafetyCap, "안전 마개", 4, 6, "내게 걸린 시럽과 매운맛을 제거."),
            new(ColaItemId.PizzaSlice, "피자 한 조각", 8, 3, "체력 +1."),
            new(ColaItemId.HotSauce, "핫소스", 3, 8, $"이 콜라의 양쪽 폭발 피해 +{ColaItemBalance.HotDamageBonus}."),
            new(ColaItemId.DoubleStamp, "더블 스탬프", 4, 7, $"내 다음 흔들기가 안전하면 +{ColaItemBalance.StampRewardBonus}토큰."),
            new(ColaItemId.BottleOpener, "병따개", 4, 8, $"이 콜라의 한계 압력 -{ColaItemBalance.OpenerReduction}."),
            new(ColaItemId.Straw, "빨대", 4, 7, $"상대 토큰을 최대 {ColaItemBalance.StrawTokens}개 훔침."),
            new(ColaItemId.SodaWater, "탄산수", 5, 5, "압력을 한계의 절반으로 맞춤."),
            new(ColaItemId.CoinCup, "동전컵", 3, 4, $"토큰 +{ColaItemBalance.CoinCupTokens}."),
            new(ColaItemId.SwapTongs, "교환 집게", 5, 4, "서로의 무작위 아이템 1개를 교환.")
        };
        public static IReadOnlyList<ColaItemDefinition> All { get; } = Array.AsReadOnly(Entries);
        // ID 10 remains retired; catalog positions must not be confused with serialized IDs.
        public static int IndexOf(ColaItemId id)
        {
            int value = (int)id;
            int index = value > 10 ? value - 1 : value;
            return value != 10 && index >= 0 && index < Entries.Length ? index : -1;
        }
        public static bool Contains(ColaItemId id) => IndexOf(id) >= 0;
        public static ColaItemId[] DrawRandom(Random random, int count, ColaItemId? excluded = null)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var pool = new List<ColaItemDefinition>(Entries);
            if (excluded.HasValue) pool.RemoveAll(item => item.Id == excluded.Value);
            var result = new ColaItemId[Math.Min(count, pool.Count)];
            for (int i = 0; i < result.Length; i++)
            {
                int total = 0;
                foreach (var item in pool) total += item.DropWeight;
                int ticket = random.Next(total);
                for (int j = 0; j < pool.Count; j++)
                {
                    ticket -= pool[j].DropWeight;
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
        public readonly int PressureBonus, Damage, RewardBonus, PressureMultiplier;
        // Kept neutral for old Editor diagnostic sources; not effects in the current game.
        public int RewardMultiplier => 1;
        public double ProbabilityReduction => 0;
        public bool SafeTip => false;
        public static ItemActionModifiers None => new(0, 1, 0, 1);
        public ItemActionModifiers(int pressureBonus, int damage, int rewardBonus, int pressureMultiplier)
        {
            PressureBonus = pressureBonus; Damage = damage;
            RewardBonus = rewardBonus; PressureMultiplier = pressureMultiplier;
        }
    }
}
