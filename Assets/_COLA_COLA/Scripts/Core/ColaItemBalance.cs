namespace ColaCola.Core
{
    /// <summary>One-effect consumables. Hot sauce lasts for one bottle; other pending effects last one shake.</summary>
    public static class ColaItemBalance
    {
        public const int Revision = 8;
        public const int LowSafeReward = 1;
        public const int MediumSafeReward = 3;
        public const int HighSafeReward = 4;
        public const int BurstReward = 5;
        public const double DangerPayThreshold = .50;
        public const int DangerPayBonus = 1;
        public const int MediumAbilityReward = 1;
        public const int HighAbilityReward = 2;
        public const int LowHealthThreshold = 2;
        public const double LowHealthProbabilityReduction = .05;
        public const int SavingsTokenThreshold = 12;
        public const int SavingsRewardBonus = 1;
        public const int SupplyItemCount = 1;
        public const int RoughPressureBonus = 2;
        public const double CrisisProbabilityReduction = .10;
        public const int PrepaidDiscount = 2;
        public const int AccidentTokenBonus = 4;
        public const int IceReduction = 5;
        public const int CandyIncrease = 4;
        // Reserved compatibility values for old diagnostics.
        public const int SyrupBonus = 0;
        public const int SpicyMultiplier = 1;
        public const double SafetyProbabilityReduction = .15;
        public const double SpicyProbabilityIncrease = .15;
        // Legacy diagnostics compatibility; SafetyCap no longer changes damage.
        public const int ShieldReduction = 0;
        public const int OpenerReduction = 4;
        public const int StrawTokens = 2;
        public const int CoinCupTokens = 3;
        public const int HotDamageBonus = 1;
        public const int StampRewardBonus = 3;
        public const int StartingHealth = 5;
        public const int SaltIncrease = 2;
        public const int CoolingReduction = 2;
        public const int PunchTokens = 3;
        public const int TicketTokens = 1;
        public const int ReturnTokens = 1;
        public const int LidIncrease = 3;

        public static int SafeRewardFor(int strength) => strength switch
        {
            1 => LowSafeReward, 2 => MediumSafeReward, 3 => HighSafeReward,
            _ => throw new System.ArgumentOutOfRangeException(nameof(strength))
        };
        public static int ShakeReward(int strength, bool burst, int safeBonus = 0)
            => burst ? BurstReward : SafeRewardFor(strength) + safeBonus;
    }
}
