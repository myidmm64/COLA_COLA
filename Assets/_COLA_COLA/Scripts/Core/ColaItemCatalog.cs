using System;
using System.Collections.Generic;

namespace ColaCola.Core
{
    public enum ColaItemId
    {
        IceCup, FizzCandy, CarbonationSyrup, QueueTicket, Bill, SpicyOrder,
        SafetyCap, PizzaSlice, HotSauce, DoubleStamp, TipEnvelope
    }

    public sealed class ColaItemDefinition
    {
        public ColaItemId Id { get; }
        public string Name { get; }
        public string Effect { get; }
        public int Price { get; }
        public ColaItemDefinition(ColaItemId id, string name, int price, string effect)
        { Id = id; Name = name; Price = price; Effect = effect; }
    }

    public static class ColaItemCatalog
    {
        private static readonly ColaItemDefinition[] Entries =
        {
            new(ColaItemId.IceCup, "얼음컵", 3, "압력 -3. 내 턴 유지."),
            new(ColaItemId.FizzCandy, "발포 캔디", 7, "압력 +4 후 상대 턴. 지금은 폭발 판정 없음."),
            new(ColaItemId.CarbonationSyrup, "탄산 시럽", 4, "상대의 다음 흔들기 압력 증가량 +2."),
            new(ColaItemId.QueueTicket, "대기 번호표", 3, "다음 상대 턴 1회 건너뛰기."),
            new(ColaItemId.Bill, "계산서", 5, "흔들지 않고 바로 상대 턴. 토큰 획득 없음."),
            new(ColaItemId.SpicyOrder, "매운맛 주문서", 3, "상대의 다음 흔들기 선택 강도 3 고정."),
            new(ColaItemId.SafetyCap, "안전 마개", 4, "내 다음 흔들기 폭발 확률 -30%p. 한계 도달 시 100%."),
            new(ColaItemId.PizzaSlice, "피자 한 조각", 8, "체력 +1. 최대 체력에서는 사용 불가."),
            new(ColaItemId.HotSauce, "핫소스", 3, "이번 콜라의 폭발 피해 +1. 새 콜라에서 해제."),
            new(ColaItemId.DoubleStamp, "더블 스탬프", 4, "내 다음 흔들기 토큰(팁 포함) 2배. 폭발 시 10개."),
            new(ColaItemId.TipEnvelope, "팁 봉투", 1, "내 다음 흔들기가 최종 확률 50% 이상에서 안전하면 +3토큰.")
        };
        public static IReadOnlyList<ColaItemDefinition> All { get; } = Array.AsReadOnly(Entries);
        public static ColaItemDefinition Get(ColaItemId id)
        {
            int index = (int)id;
            if (index < 0 || index >= Entries.Length) throw new ArgumentOutOfRangeException(nameof(id));
            return Entries[index];
        }
    }

    public readonly struct ItemActionModifiers
    {
        public readonly int PressureBonus, Damage, RewardMultiplier;
        public readonly double ProbabilityReduction;
        public readonly bool SafeTip;
        public static ItemActionModifiers None => new(0, 0, 1, 1, false);
        public ItemActionModifiers(int pressureBonus, double probabilityReduction, int damage, int rewardMultiplier, bool safeTip)
        {
            PressureBonus = pressureBonus; ProbabilityReduction = probabilityReduction;
            Damage = damage; RewardMultiplier = rewardMultiplier; SafeTip = safeTip;
        }
    }
}
