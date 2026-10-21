using System.Collections.Generic;

namespace TcgBattle.Gameplay
{
    public enum Side { Player, Bot }
    public enum Phase { Setup, PlayerTurn, BotTurn, GameOver }

    /// <summary>One physical card in play: either a battle card or a support card.</summary>
    public class CardInstance
    {
        public string CardId;
        public Data.BattleCardDef Battle;      // null for support cards
        public Data.SupportCardDef Support;    // null for battle cards
        public int CurrentHp;                  // battle cards only; starts at MaxHp
        public bool FaceDown;                  // used during the coin-flip reveal animation

        public bool IsBattle => Battle != null;
        public bool IsSupport => Support != null;
        public bool IsKnockedOut => IsBattle && CurrentHp <= 0;
        public int MaxHp => IsBattle ? Battle.maxHp : 0;
        public int AttackPower => IsBattle ? Battle.attack : 0;
        public int AttackCost => IsBattle ? Battle.attackEnergyCost : 0;
        public int RetreatCost => IsBattle ? Battle.retreatEnergyCost : 0;
        public Data.Element Element => IsBattle ? Battle.element : Data.Element.Dark;
        public string DisplayName => IsBattle ? Battle.displayName : Support.displayName;
        public string ArtResource => IsBattle ? Battle.artResource : Support.artResource;

        public static CardInstance CreateBattle(Data.BattleCardDef def)
        {
            return new CardInstance { CardId = def.id, Battle = def, CurrentHp = def.maxHp };
        }

        public static CardInstance CreateSupport(Data.SupportCardDef def)
        {
            return new CardInstance { CardId = def.id, Support = def };
        }
    }

    /// <summary>Everything that belongs to one player during a battle.</summary>
    public class PlayerState
    {
        public Side Side;
        public string DisplayName;
        public List<CardInstance> Deck = new List<CardInstance>();
        public List<CardInstance> Hand = new List<CardInstance>();
        public List<CardInstance> Bench = new List<CardInstance>();   // max 3
        public CardInstance Active;
        public int Energy;
        public int Points;
        public bool HasAttackedThisTurn;
        public int NextAttackBonus;            // Punarma single-shot bonus
        public bool UsedFreeRetreatViaMbokSumi;// set when the Mbok Sumi free-retreat was consumed this turn

        public bool HasBenchBattleCard
        {
            get
            {
                foreach (var c in Bench)
                    if (BattleRules.CanEnterActiveDirectly(c))
                        return true;
                return false;
            }
        }

        public void Reset()
        {
            Deck.Clear();
            Hand.Clear();
            Bench.Clear();
            Active = null;
            Energy = 0;
            Points = 0;
            HasAttackedThisTurn = false;
            NextAttackBonus = 0;
            UsedFreeRetreatViaMbokSumi = false;
        }
    }
}
