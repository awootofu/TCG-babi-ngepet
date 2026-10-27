using System;
using System.Collections.Generic;
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
        public string attackEffectText;     // effect of this attack, when the data carries one
        public string passiveText;          // battle-card passive/special ability (not the attack)
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
        public string description;          // concise Indonesian effect text for the preview
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
                passiveText = "",
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
                description = "Selama di bangku: mundur gratis sekali per giliran.",
            },
            new SupportCardDef
            {
                id = "TA", displayName = "Tumbal Anak", artResource = "Cards/Anak",
                effect = SupportEffect.TumbalAnakHandPassive, effectValue = 0,
                description = "Saat di tangan: menangkal satu serangan Api ke Babi Ngepet. Satu kartu Tumbal Anak dikorbankan.",
            },
            new SupportCardDef
            {
                id = "KL", displayName = "Kucing Liar", artResource = "Cards/Kucing",
                effect = SupportEffect.KucingLiarSingleShot, effectValue = 40,
                description = "Saat dimainkan: 40 damage ke Mbok Sumi di bangku lawan hingga keluar dari permainan. Hanya jika lawan punya Mbok Sumi di bangku.",
            },
            new SupportCardDef
            {
                id = "PJ", displayName = "Punarma Jumat Legi", artResource = "Cards/Punarma",
                effect = SupportEffect.PunarmaJumatLegiSingleShot, effectValue = 50,
                description = "Saat dimainkan: serangan berikutnya +50 damage hingga akhir giliran. Hanya jika Babi Ngepet aktif di sisi lawan.",
            },
        };

        [Header("Player deck: 'Babi Ngepet' series, all Dark (+ support cards)")]
        public DeckEntry[] playerDeck =
        {
            new DeckEntry { cardId = "PK", copies = 4 },
            new DeckEntry { cardId = "BN", copies = 4 },
            new DeckEntry { cardId = "SB", copies = 4 },
            new DeckEntry { cardId = "MS", copies = 2 },
            new DeckEntry { cardId = "TA", copies = 6 },
        };

        [Header("Bot deck: 'Warga' series, all Fire (+ support cards)")]
        public DeckEntry[] botDeck =
        {
            new DeckEntry { cardId = "WD", copies = 4 },
            new DeckEntry { cardId = "PO", copies = 4 },
            new DeckEntry { cardId = "AW", copies = 4 },
            new DeckEntry { cardId = "PJ", copies = 6 },
            new DeckEntry { cardId = "KL", copies = 2 },
        };

        // Canonical deck lists from the requirements sheet. BattleManager validates the
        // serialized lists above against these and falls back to these lists if an asset
        // was edited or left stale, so a match can never start with an illegal deck.
        public static readonly DeckEntry[] RequiredPlayerDeck =
        {
            new DeckEntry { cardId = "PK", copies = 4 },
            new DeckEntry { cardId = "BN", copies = 4 },
            new DeckEntry { cardId = "SB", copies = 4 },
            new DeckEntry { cardId = "MS", copies = 2 },
            new DeckEntry { cardId = "TA", copies = 6 },
        };

        public static readonly DeckEntry[] RequiredBotDeck =
        {
            new DeckEntry { cardId = "WD", copies = 4 },
            new DeckEntry { cardId = "PO", copies = 4 },
            new DeckEntry { cardId = "AW", copies = 4 },
            new DeckEntry { cardId = "PJ", copies = 6 },
            new DeckEntry { cardId = "KL", copies = 2 },
        };

        [TextArea(3, 8)] public string rulesText =
            "Fire deals 2x damage to Dark cards. Dark deals 1x damage to Fire.\n" +
            "Each turn: draw one card and gain one energy.\n" +
            "Maximum 10 energy; extra energy is lost. Maximum 10 cards in hand; extra draws are discarded.\n" +
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
                    if (e != null)
                        total += e.copies;
            return total;
        }

        /// <summary>Returns a fresh copy of the requirements' 20-card list for one side.</summary>
        public static DeckEntry[] CreateRequiredDeck(bool playerSide)
        {
            var source = playerSide ? RequiredPlayerDeck : RequiredBotDeck;
            var copy = new DeckEntry[source.Length];
            for (int i = 0; i < source.Length; i++)
                copy[i] = new DeckEntry { cardId = source[i].cardId, copies = source[i].copies };
            return copy;
        }

        /// <summary>
        /// Validates an exact 20-card list against the requirements: total size, exact
        /// per-card copies, no duplicate entries, and no cards from the opposing roster.
        /// Returns null when valid, otherwise a human-readable error.
        /// </summary>
        public static string ValidateDeck(DeckEntry[] entries, bool playerSide)
        {
            if (entries == null)
                return "Deck list is null.";
            if (CountDeckCopies(entries) != DeckSize)
                return $"Deck has {CountDeckCopies(entries)} cards; expected {DeckSize}.";

            var expected = playerSide ? RequiredPlayerDeck : RequiredBotDeck;
            var counts = new Dictionary<string, int>();
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.cardId))
                    return "Deck contains an entry with no card id.";
                if (counts.ContainsKey(entry.cardId))
                    return $"Deck contains duplicate entries for '{entry.cardId}'.";
                counts[entry.cardId] = entry.copies;
            }

            foreach (var required in expected)
            {
                if (!counts.TryGetValue(required.cardId, out int actual) || actual != required.copies)
                {
                    counts.TryGetValue(required.cardId, out actual);
                    return $"Deck has {actual} x '{required.cardId}'; expected {required.copies}.";
                }
                counts.Remove(required.cardId);
            }

            if (counts.Count > 0)
            {
                foreach (var unexpected in counts.Keys)
                    return $"Deck contains card from outside the required roster: '{unexpected}'.";
            }
            return null;
        }
    }
}
