namespace TcgBattle.Gameplay
{
    /// <summary>
    /// Pure battle rules: damage math, element multipliers, and win/loss checks.
    /// No Unity APIs - all methods are static and side-effect free.
    /// </summary>
    public static class BattleRules
    {
        public const int BenchLimit = 3;
        public const int HandLimit = 7;
        public const int StartingHandSize = 5;
        public const int DrawPerTurn = 1;
        public const int EnergyPerTurn = 1;
        public const int EnergyCap = 9;
        public const int PointsToWin = 3;

        /// <summary>Fire attacks deal 2x to Dark; Dark deals 1x to anything.</summary>
        public static int DamageMultiplier(Data.Element attacker, Data.Element defender)
        {
            if (attacker == Data.Element.Fire && defender == Data.Element.Dark)
                return 2;
            return 1;
        }

        public static bool IsSuperEffective(Data.Element attacker, Data.Element defender)
        {
            return DamageMultiplier(attacker, defender) > 1;
        }

        /// <summary>Base damage including element multiplier and active attack bonus.</summary>
        public static int ComputeDamage(CardInstance attacker, CardInstance defender, int attackBonus)
        {
            if (attacker == null || defender == null || !attacker.IsBattle || !defender.IsBattle)
                return 0;
            int raw = attacker.AttackPower + attackBonus;
            int multiplier = DamageMultiplier(attacker.Element, defender.Element);
            return raw * multiplier;
        }
    }
}
