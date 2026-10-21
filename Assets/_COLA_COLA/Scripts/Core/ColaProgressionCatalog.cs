using System;
using System.Collections.Generic;

namespace ColaCola.Core
{
    public enum ColaAbilityId
    {
        RegularDiscount, LightHand, BoldChoice, BreakagePay, FirstOrderService, TidyCoupon,
        SaltyTaste, CoolingExpert, HotDeal, RecyclingCredit, ExtraStorage, DangerPay,
        RegularSupply, RoughHands, CrisisManagement, PrepaidContract, AccidentSubsidy
    }

    // IDs are persisted. Append new entries rather than reordering them.
    public enum ColaAchievementId
    {
        FirstGuest, DeliveryDone, ChefOffDuty, ManagerApproval, ClosingTime, AccidentFree,
        LastSip, DaringSip, PaperThin, BellRegular, SelfSufficient, HeadOn, GentleShakes,
        StrongShakes, FullCourse, MenuExchange, FirstOrder, MenuExplorer, SafeStreak,
        EmergencyCooling, SafetyCheck, EmptyWallet, SpicyVictory, DoubleTension,
        ExchangeRegular, AbilityCollector
    }

    public sealed class ColaProgressionDefinition<T> where T : Enum
    {
        public readonly T Id;
        private readonly string name, description;
        private string Key => (typeof(T) == typeof(ColaAbilityId) ? "ability." : "achievement.") + Id;
        public string Name => ColaLocalization.Get(Key + ".name", name);
        public string Description => ColaLocalization.Get(Key + ".description", description);
        public ColaProgressionDefinition(T id, string name, string description)
        { Id = id; this.name = name; this.description = description; }
    }

    public static class ColaProgressionCatalog
    {
        public static readonly IReadOnlyList<ColaProgressionDefinition<ColaAbilityId>> Abilities = Array.AsReadOnly(new ColaProgressionDefinition<ColaAbilityId>[]
        {
            new ColaProgressionDefinition<ColaAbilityId>(ColaAbilityId.RegularDiscount, "단골 할인", "웨이터벨 가격 -1토큰"),
            new(ColaAbilityId.LightHand, "가벼운 손", "강도 1로 생존하면 토큰 +1"),
            new(ColaAbilityId.BoldChoice, "과감한 선택", "강도 3으로 생존하면 토큰 +2"),
            new(ColaAbilityId.BreakagePay, "파손 보상", "내 차례에 폭발하면 토큰 +2"),
            new(ColaAbilityId.FirstOrderService, "첫 주문 서비스", "보스마다 벨 배달 1회 +1개. 획득 후 빈 칸이 충분한 다음 주문에 적용"),
            new(ColaAbilityId.TidyCoupon, "정리 쿠폰", "획득할 때와 새 보스 시작 시 교환권 1개. 가득 차면 빈 칸을 기다림"),
            // Keep persisted IDs when replacing the former item-dependent passives.
            new(ColaAbilityId.SaltyTaste, "알맞은 힘", "강도 2로 생존하면 토큰 +1"),
            new(ColaAbilityId.CoolingExpert, "침착한 손", "체력 2 이하일 때 폭발 확률 -5%p. 한계 도달 시 100%"),
            new(ColaAbilityId.HotDeal, "여유 자금", "12토큰 이상 가진 채 흔들어 생존하면 토큰 +1"),
            new(ColaAbilityId.RecyclingCredit, "재활용 적립", "획득 후 아이템을 3개 사용할 때마다 토큰 +1"),
            new(ColaAbilityId.ExtraStorage, "넉넉한 수납", "내 트레이 +2칸"),
            new(ColaAbilityId.DangerPay, "위험 수당", $"예상 폭발 확률 {ColaItemBalance.DangerPayThreshold * 100:0}% 이상에서 생존하면 토큰 +{ColaItemBalance.DangerPayBonus}"),
            new(ColaAbilityId.RegularSupply, "정기 납품", "새 콜라마다 무작위 아이템 1개. 가득 차면 빈 칸을 기다림"),
            new(ColaAbilityId.RoughHands, "거친 손맛", "강도 3으로 생존하면 상대의 다음 흔들기 압력 +2"),
            new(ColaAbilityId.CrisisManagement, "위기 대처", "체력 2 이하일 때 폭발 확률 -10%p. 한계 도달 시 100%"),
            new(ColaAbilityId.PrepaidContract, "선불 계약", "각 콜라의 첫 벨 주문 가격 -2토큰"),
            new(ColaAbilityId.AccidentSubsidy, "사고 수당", "내 차례에 폭발하면 토큰 +4")
        });

        public static readonly IReadOnlyList<ColaProgressionDefinition<ColaAchievementId>> Achievements = Array.AsReadOnly(new ColaProgressionDefinition<ColaAchievementId>[]
        {
            new ColaProgressionDefinition<ColaAchievementId>(ColaAchievementId.FirstGuest, "첫 손님", "1단계 보스 격파"),
            new(ColaAchievementId.DeliveryDone, "배달 완료", "2단계 보스 격파"),
            new(ColaAchievementId.ChefOffDuty, "주방장 퇴근", "3단계 보스 격파"),
            new(ColaAchievementId.ManagerApproval, "관리자 승인", "4단계 보스 격파"),
            new(ColaAchievementId.ClosingTime, "영업 종료", "5단계까지 도전 완료"),
            new(ColaAchievementId.AccidentFree, "무사고 영업", "내 폭발 없이 보스 1명 격파"),
            new(ColaAchievementId.LastSip, "마지막 한 모금", "체력 1로 보스 격파"),
            new(ColaAchievementId.DaringSip, "대담한 한 모금", "예상 폭발 확률 75% 이상에서 생존"),
            new(ColaAchievementId.PaperThin, "종이 한 장 차이", "흔든 뒤 압력이 한계보다 정확히 1 낮은 상태로 생존"),
            new(ColaAchievementId.BellRegular, "벨 단골", "한 도전에서 벨 5회 사용"),
            new(ColaAchievementId.SelfSufficient, "자급자족", "벨 없이 보스 1명 격파"),
            new(ColaAchievementId.HeadOn, "정면승부", "아이템을 사용하지 않고 보스 1명 격파"),
            new(ColaAchievementId.GentleShakes, "살살 흔들기", "강도 1만 사용해 보스 1명 격파"),
            new(ColaAchievementId.StrongShakes, "세게 흔들기", "강도 3만 사용해 보스 1명 격파"),
            new(ColaAchievementId.FullCourse, "풀코스", "누적 기록으로 서로 다른 아이템 19종 사용"),
            new(ColaAchievementId.MenuExchange, "메뉴 교체", "교환권으로 받은 아이템을 사용"),
            new(ColaAchievementId.FirstOrder, "첫 주문", "웨이터벨로 첫 주문"),
            new(ColaAchievementId.MenuExplorer, "메뉴 탐방", "누적 기록으로 서로 다른 아이템 5종 사용"),
            new(ColaAchievementId.SafeStreak, "연속 생존", "한 도전에서 내 흔들기 5회 연속 생존"),
            new(ColaAchievementId.EmergencyCooling, "위기 냉각", "압력이 한계의 75% 이상일 때 얼음컵 사용"),
            new(ColaAchievementId.SafetyCheck, "안전 점검", "안전 마개 사용"),
            new(ColaAchievementId.EmptyWallet, "잔고 정리", "계산 펀치로 상대 흔들기 보상 0 만들기"),
            new(ColaAchievementId.SpicyVictory, "매운 승부", "핫소스가 적용된 폭발로 보스 격파"),
            new(ColaAchievementId.DoubleTension, "두 배의 긴장", "매운맛 주문서가 적용된 흔들기에서 생존"),
            new(ColaAchievementId.ExchangeRegular, "교환 단골", "한 도전에서 교환권 3회 사용"),
            new(ColaAchievementId.AbilityCollector, "능력 수집", "한 도전에서 능력 4개 획득")
        });
    }
}
