namespace ColaCola.Core
{
    /// <summary>Public-state tactics only; these settings never read the outcome RNG.</summary>
    public static class ColaBossTactics
    {
        private static readonly int[] StartingItems = { 2, 2, 3, 3, 4 };
        private static readonly int[] StartingTokens = { 0, 2, 3, 4, 6 };
        public static int StartingItemsForStage(int level) => StartingItems[System.Math.Max(0, System.Math.Min(4, level - 1))];
        public static int StartingTokensForStage(int level) => StartingTokens[System.Math.Max(0, System.Math.Min(4, level - 1))];
        // Prefixes of one public, fixed priority list: count comparisons keep composition comparable.
        private static readonly int[] StartingAbilities = { 0, 1, 2, 4, 6 };
        private static readonly ColaAbilityId[] AbilityPriority = {
            ColaAbilityId.LightHand, ColaAbilityId.RegularDiscount, ColaAbilityId.RoughHands,
            ColaAbilityId.RegularSupply, ColaAbilityId.CrisisManagement, ColaAbilityId.PrepaidContract,
            ColaAbilityId.BoldChoice, ColaAbilityId.AccidentSubsidy, ColaAbilityId.CoolingExpert,
            ColaAbilityId.FirstOrderService, ColaAbilityId.BreakagePay, ColaAbilityId.DangerPay,
            ColaAbilityId.SaltyTaste, ColaAbilityId.RecyclingCredit, ColaAbilityId.HotDeal,
            ColaAbilityId.TidyCoupon, ColaAbilityId.ExtraStorage
        };
        public static int StartingAbilitiesForStage(int level) => StartingAbilities[System.Math.Max(0, System.Math.Min(4, level - 1))];
        public static ColaAbilityId AbilityAt(int index) => AbilityPriority[index];
        public const int AttackStage = 4;
        public const int DefenseStage = 5;
        public const double MinimumComboGain = .08;
        public const double MinimumAttackImprovement = .06;
        public const double DefenseRiskThreshold = .20;
        public const double MinimumDefenseReduction = .08;
        public const double StageFourTemperature = .05;
        public const double StageFiveTemperature = .04;
        public const double StageFourExploration = .02;
        public const double StageFiveExploration = .015;
    }
}
