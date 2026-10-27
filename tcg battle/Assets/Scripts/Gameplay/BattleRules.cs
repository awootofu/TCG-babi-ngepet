namespace TcgBattle.Gameplay
{
    /// <summary>
    /// Pure battle rules: damage math, element multipliers, and win/loss checks.
    /// No Unity APIs - all methods are static and side-effect free.
    /// </summary>
    public static class BattleRules
    {
        public const int BenchLimit = 3;
        public const int HandLimit = 10;
        public const int StartingHandSize = 5;
        public const int DrawPerTurn = 1;
        public const int EnergyPerTurn = 1;
        public const int EnergyCap = 10;
        public const int PointsToWin = 3;

        // Warga Desa > Pasukan Obor > Amukan Warga. The two evolved battle cards
        // may only enter the active slot through this line, never as a normal play.
        public const string WargaBasicId = "WD";
        public const string WargaEvolution1Id = "PO";
        public const string WargaEvolution2Id = "AW";

        // Pak Karta's evolution line: Pak Karta > Babi Ngepet > Siluman Babi.
        // Every step consumes the matching evolution card from hand.
        public const string PakKartaId = "PK";
        public const string MbokSumiId = "MS";
        public const string BabiNgepetId = "BN";
        public const string SilumanBabiId = "SB";

        /// <summary>
        /// Evolution keeps the damage already taken, measured against the old card's
        /// printed maximum. The result is clamped to the evolved card's valid HP range.
        /// </summary>
        public static int EvolutionHpAfterDamage(int oldMaxHp, int oldCurrentHp, int newMaxHp)
        {
            int damageTaken = oldMaxHp - oldCurrentHp;
            int newCurrentHp = newMaxHp - damageTaken;
            if (newCurrentHp < 0)
                return 0;
            if (newCurrentHp > newMaxHp)
                return newMaxHp;
            return newCurrentHp;
        }

        public static string EvolutionSource(string cardId)
        {
            if (cardId == WargaEvolution1Id)
                return WargaBasicId;
            if (cardId == WargaEvolution2Id)
                return WargaEvolution1Id;
            if (cardId == BabiNgepetId)
                return PakKartaId;
            if (cardId == SilumanBabiId)
                return BabiNgepetId;
            return null;
        }

        public static string EvolutionTarget(string cardId)
        {
            if (cardId == WargaBasicId)
                return WargaEvolution1Id;
            if (cardId == WargaEvolution1Id)
                return WargaEvolution2Id;
            if (cardId == PakKartaId)
                return BabiNgepetId;
            if (cardId == BabiNgepetId)
                return SilumanBabiId;
            return null;
        }

        public static bool IsEvolutionOnly(CardInstance card)
        {
            return card != null && card.IsBattle && EvolutionSource(card.CardId) != null;
        }

        /// <summary>
        /// Cards that may be put into the active slot without an evolution. Basic
        /// cards qualify; PO/AW and BN/SB are evolution-only and must be played from
        /// hand onto their matching source card.
        /// </summary>
        public static bool CanEnterActiveDirectly(CardInstance card)
        {
            return card != null && card.IsBattle && !IsEvolutionOnly(card);
        }

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
