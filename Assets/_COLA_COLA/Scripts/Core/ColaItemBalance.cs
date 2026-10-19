namespace ColaCola.Core
{
    /// <summary>One-effect consumables. Hot sauce lasts for one bottle; other pending effects last one shake.</summary>
    public static class ColaItemBalance
    {
        public const int IceReduction = 5;
        public const int CandyIncrease = 6;
        public const int SyrupBonus = 2;
        public const int SpicyMultiplier = 2;
        public const int ShieldReduction = 1;
        public const int HotDamageBonus = 1;
        public const int StampRewardBonus = 3;
        public const int StartingHealth = 6;
        public const int MaximumItemUsesPerTurn = 2;
        public const int MaximumBossHealthBonus = 2;

        public static int ShakeReward(int strength, bool burst, int safeBonus = 0)
            => burst ? 5 : strength + safeBonus;
    }
}
