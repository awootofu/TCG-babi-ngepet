using System;
using UnityEngine;

namespace TcgBattle.Data
{
    public enum Element { Dark, Fire }
    public enum CardKind { Battle, Support }
    public enum SupportEffect { MbokSumiBenchPassive, TumbalAnakHandPassive, KucingLiarSingleShot, PunarmaJumatLegiSingleShot }

    [Serializable]
    public class BattleCardDef
    {
        public string id;
        public string displayName;
        public Element element;
        public string artResource;          // path under Assets/Resources, no extension
        public int maxHp;
        public int attack;
        public string attackName;
        public string attackEffectText;     // effect note shown on-card in the art
        public int attackEnergyCost;
        public int retreatEnergyCost;
    }

    [Serializable]
    public class SupportCardDef
    {
        public string id;
        public string displayName;
        public string artResource;
        public SupportEffect effect;
        public int effectValue;             // damage / bonus amount for single-shot cards
    }

    [Serializable]
    public class DeckEntry                 // id + copy count for one deck (bot deck may differ)
    {
        public string cardId;
        public int copies;
    }

    /// <summary>
    /// Central card database. Stats were transcribed from the artist's baked card artwork -
    /// edit values here to rebalance. Deck composition (copies per card, per player) is below.
    /// </summary>
    [CreateAssetMenu(menuName = "TCG Battle/Card Database", fileName = "CardDatabase")]
    public class CardDatabase : ScriptableObject
    {
        public const int DeckSize = 20;

        [Header("Battle cards - stats transcribed from the card artwork")]
        public BattleCardDef[] battleCards =
        {
            new BattleCardDef
            {
                id = "BN", displayName = "Babi Ngepet", element = Element.Dark,
                artResource = "Cards/Babi Ngepet", maxHp = 100, attack = 40,
                attackName = "Mencuri Kekayaan", attackEffectText = "Mengambil 1 poin energi lawan.",
                attackEnergyCost = 1, retreatEnergyCost = 1,
            },
            new BattleCardDef
            {
                id = "PK", displayName = "Pak Karta", element = Element.Dark,
                artResource = "Cards/Pak Karta", maxHp = 150, attack = 20,
                attackName = "Pikiran Jahat", attackEffectText = "",
                attackEnergyCost = 1, retreatEnergyCost = 1,
            },
            new BattleCardDef
            {
                id = "SB", displayName = "Siluman Babi", element = Element.Dark,
                artResource = "Cards/Siluman Babi", maxHp = 200, attack = 100,
                attackName = "Hilang Kemanusiaan", attackEffectText = "",
                attackEnergyCost = 2, retreatEnergyCost = 2,
            },
            new BattleCardDef
            {
                id = "WD", displayName = "Warga Desa", element = Element.Fire,
                artResource = "Cards/Warga Evo 1", maxHp = 150, attack = 30,
                attackName = "Serang Balik", attackEffectText = "",
                attackEnergyCost = 1, retreatEnergyCost = 1,
            },
            new BattleCardDef
            {
                id = "PO", displayName = "Pasukan Obor", element = Element.Fire,
                artResource = "Cards/Warga Evo 2", maxHp = 200, attack = 45,
                attackName = "Obor Api", attackEffectText = "",
                attackEnergyCost = 1, retreatEnergyCost = 1,
            },
            new BattleCardDef
            {
                id = "AW", displayName = "Amukan Warga", element = Element.Fire,
                artResource = "Cards/Warga Evo 3", maxHp = 220, attack = 75,
                attackName = "Balas Dendam", attackEffectText = "",
                attackEnergyCost = 2, retreatEnergyCost = 2,
            },
        };

        [Header("Support cards - effects from the card artwork")]
        public SupportCardDef[] supportCards =
        {
            new SupportCardDef
            {
                id = "MS", displayName = "Mbok Sumi", artResource = "Cards/Mbok Sumi",
                effect = SupportEffect.MbokSumiBenchPassive, effectValue = 0,
            },
            new SupportCardDef
            {
                id = "TA", displayName = "Tumbal Anak", artResource = "Cards/Anak",
                effect = SupportEffect.TumbalAnakHandPassive, effectValue = 0,
            },
            new SupportCardDef
            {
                id = "KL", displayName = "Kucing Liar", artResource = "Cards/Kucing",
                effect = SupportEffect.KucingLiarSingleShot, effectValue = 40,
            },
            new SupportCardDef
            {
                id = "PJ", displayName = "Punarma Jumat Legi", artResource = "Cards/Punarma",
                effect = SupportEffect.PunarmaJumatLegiSingleShot, effectValue = 50,
            },
        };

        [Header("Player deck: 'Babi Ngepet' series, all Dark (+ support cards)")]
        public DeckEntry[] playerDeck =
        {
            new DeckEntry { cardId = "BN", copies = 3 },
            new DeckEntry { cardId = "PK", copies = 4 },
            new DeckEntry { cardId = "SB", copies = 3 },
            new DeckEntry { cardId = "MS", copies = 2 },
            new DeckEntry { cardId = "TA", copies = 2 },
            new DeckEntry { cardId = "KL", copies = 3 },
            new DeckEntry { cardId = "PJ", copies = 3 },
        };

        [Header("Bot deck: 'Warga' series, all Fire (+ support cards)")]
        public DeckEntry[] botDeck =
        {
            new DeckEntry { cardId = "WD", copies = 4 },
            new DeckEntry { cardId = "PO", copies = 4 },
            new DeckEntry { cardId = "AW", copies = 4 },
            new DeckEntry { cardId = "MS", copies = 2 },
            new DeckEntry { cardId = "TA", copies = 2 },
            new DeckEntry { cardId = "KL", copies = 2 },
            new DeckEntry { cardId = "PJ", copies = 2 },
        };

        [TextArea(3, 8)] public string rulesText =
            "Fire deals 2x damage to Dark cards. Dark deals 1x damage to Fire.\n" +
            "Each turn: draw one card and gain one energy.\n" +
            "Attacks and retreats cost energy. Knock out a card to score.\n" +
            "First to 3 points wins.";

        public BattleCardDef GetBattleCard(string id)
        {
            foreach (var def in battleCards)
                if (def.id == id)
                    return def;
            return null;
        }

        public SupportCardDef GetSupportCard(string id)
        {
            foreach (var def in supportCards)
                if (def.id == id)
                    return def;
            return null;
        }

        /// <summary>Total copies of every card in one deck list (should equal deckSize).</summary>
        public static int CountDeckCopies(DeckEntry[] entries)
        {
            int total = 0;
            if (entries != null)
                foreach (var e in entries)
                    total += e.copies;
            return total;
        }
    }
}
