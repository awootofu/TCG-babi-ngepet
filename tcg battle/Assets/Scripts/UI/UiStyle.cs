using UnityEngine;
using TcgBattle.Data;
using TcgBattle.Gameplay;

namespace TcgBattle.UI
{
    /// <summary>
    /// One small, shared visual language for the battle screen. Colors, card-frame
    /// states and the few typography tokens the card faces need live here so
    /// BattleUI, CardView, CardInspectPreview and the FX helpers cannot drift apart.
    /// This is presentation only: no gameplay value is read or changed.
    /// </summary>
    public static class UiStyle
    {
        // ---------------------------------------------------------------- palette

        // Board surfaces: a deep ink player field with a cooler bot terrace.
        public static readonly Color BoardDeep = Hex(0x241B36);      // existing player field
        public static readonly Color PlayerZoneGlow = Hex(0x30254A);
        public static readonly Color BotZone = Hex(0x221B33);
        public static readonly Color BotZoneGlow = Hex(0x33294A);
        public static readonly Color Panel = Hex(0x1C1530);
        public static readonly Color PanelRaised = Hex(0x2A2144);
        public static readonly Color PanelInset = Hex(0x161020);

        // Text and metal accents.
        public static readonly Color Ivory = Hex(0xF2EADA);
        public static readonly Color IvoryDim = Hex(0xC9BFDB);
        public static readonly Color IvoryMuted = Hex(0x9D93B3);
        public static readonly Color Gold = Hex(0xD9A843);
        public static readonly Color GoldBright = Hex(0xF2C14E);
        public static readonly Color GoldDeep = Hex(0x9C7428);
        public static readonly Color GoldSoft = new Color(0.85f, 0.66f, 0.26f, 0.30f);
        public static readonly Color Ink = Hex(0x241B36);
        public static readonly Color InkOnGold = Hex(0x3A2506);
        public static readonly Color InkSoft = Hex(0x4C3D78);
        public static readonly Color Maroon = Hex(0x5E3336);
        public static readonly Color Danger = Hex(0xC0504E);
        public static readonly Color DisabledSurface = Hex(0x5C5470);
        public static readonly Color DisabledText = Hex(0x9A92AC);
        public static readonly Color ButtonHover = Hex(0xC08A2E);

        // Element language. These tint UI accents only; the card art is untouched.
        public static readonly Color Fire = Hex(0xE4573F);       // warm red
        public static readonly Color Ember = Hex(0xF08A3C);      // impact / flame accent
        public static readonly Color FireDeep = Hex(0x7E2D2A);
        public static readonly Color Dark = Hex(0xA879E8);       // violet accent
        public static readonly Color DarkDeep = Hex(0x382A5C);

        // Card faces.
        public static readonly Color CardBacking = Hex(0x1E1730);
        public static readonly Color CardBackingSupport = Hex(0x2A2148);
        public static readonly Color CardShadow = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color FrameSupport = new Color(0.72f, 0.66f, 0.86f, 0.62f);
        public static readonly Color StageRing1 = new Color(0.95f, 0.86f, 0.62f, 0.30f);
        public static readonly Color StageRing2 = new Color(0.98f, 0.90f, 0.68f, 0.48f);
        public static readonly Color HoverFrame = new Color(0.93f, 0.78f, 0.42f, 0.90f);
        public static readonly Color SelectedFrame = new Color(1f, 0.86f, 0.45f, 1f);
        public static readonly Color EligibleFrame = new Color(0.85f, 0.66f, 0.26f, 0.72f);
        public static readonly Color TargetHarmful = new Color(0.89f, 0.34f, 0.28f, 0.82f);
        public static readonly Color TargetHelpful = new Color(0.85f, 0.66f, 0.26f, 0.72f);
        public static readonly Color SupportChip = Hex(0x241D38);
        public static readonly Color SupportChipRing = new Color(0.72f, 0.66f, 0.86f, 0.45f);
        public static readonly Color StripBacking = new Color(0.055f, 0.04f, 0.09f, 0.72f);
        public static readonly Color SlotEmpty = new Color(0.08f, 0.05f, 0.13f, 0.58f);
        public static readonly Color SlotEmptyRing = new Color(0.95f, 0.92f, 0.85f, 0.15f);
        public static readonly Color BoardFrame = new Color(0.85f, 0.66f, 0.26f, 0.10f);
        public static readonly Color BoardFrameDiamond = new Color(0.85f, 0.66f, 0.26f, 0.22f);
        public static readonly Color DividerGlow = new Color(0.85f, 0.66f, 0.26f, 0.09f);

        // HP language, shared by card faces, the inspection preview and damage FX.
        public static readonly Color HpFull = Hex(0x6FA867);
        public static readonly Color HpMid = Hex(0xD9A843);
        public static readonly Color HpLow = Hex(0xC0504E);
        public static readonly Color HpTrack = new Color(0f, 0f, 0f, 0.60f);
        public static readonly Color HpText = new Color(1f, 1f, 1f, 0.98f);
        public static readonly Color HpOutline = new Color(0.95f, 0.92f, 0.85f, 0.20f);

        // Score pips.
        public static readonly Color PipSocket = new Color(0.95f, 0.92f, 0.85f, 0.14f);
        public static readonly Color PipEmpty = new Color(0.95f, 0.92f, 0.85f, 0.24f);

        // Status rail and result screen.
        public static readonly Color StatusPill = Hex(0x211A33);
        public static readonly Color StatusText = Hex(0xF2EADA);
        public static readonly Color StatusRing = new Color(0.85f, 0.66f, 0.26f, 0.46f);
        public static readonly Color OverlayDim = new Color(0.04f, 0.02f, 0.08f, 0.76f);
        public static readonly Color ResultPanel = Hex(0x1C1530);

        // Typography tokens (reference sizes before UiRescaler applies the canvas scale).
        public const float CardHpActive = 15f;
        public const float CardHpBench = 13f;
        public const float CardCost = 16f;
        public const float CardChip = 11f;

        // ---------------------------------------------------------------- helpers

        public static int EvolutionStage(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return 0;
            // Reliable metadata, never the display name: PO evolves from WD (stage 1),
            // AW evolves from PO (stage 2). Basic cards and supports are stage 0.
            string source = BattleRules.EvolutionSource(cardId);
            if (source == BattleRules.WargaBasicId || source == BattleRules.PakKartaId)
                return 1;
            if (source == BattleRules.WargaEvolution1Id || source == BattleRules.BabiNgepetId)
                return 2;
            return 0;
        }

        public static Color StageRingColor(int stage)
        {
            if (stage >= 2)
                return StageRing2;
            if (stage == 1)
                return StageRing1;
            return new Color(0f, 0f, 0f, 0f);
        }

        public static string TypeLabel(CardInstance card)
        {
            if (card == null)
                return null;
            if (card.IsSupport)
                return SupportLabel(card);
            int stage = EvolutionStage(card.CardId);
            if (stage == 1)
                return "EVO I";
            if (stage == 2)
                return "EVO II";
            return null;
        }

        /// <summary>
        /// One-shot supports are effects, not passive bench/hand cards. The data enum is
        /// the source of truth; display names are never inspected.
        /// </summary>
        public static bool IsOneShotSupport(CardInstance card)
        {
            return card != null && card.IsSupport && card.Support != null &&
                (card.Support.effect == SupportEffect.KucingLiarSingleShot ||
                 card.Support.effect == SupportEffect.PunarmaJumatLegiSingleShot);
        }

        public static string SupportLabel(CardInstance card)
        {
            return IsOneShotSupport(card) ? "EFEK" : "PASIF";
        }

        public static Color SupportChipColor(CardInstance card)
        {
            return IsOneShotSupport(card) ? Panel : SupportChip;
        }

        public static Color SupportRingColor(CardInstance card)
        {
            return IsOneShotSupport(card) ? WithAlpha(Gold, 0.55f) : SupportChipRing;
        }

        public static Color SupportTextColor(CardInstance card)
        {
            return IsOneShotSupport(card) ? GoldBright : Ivory;
        }

        public static Color ElementAccent(Element element)
        {
            return element == Element.Fire ? Ember : Dark;
        }

        public static Color ElementText(Element element)
        {
            return element == Element.Fire
                ? new Color(0.96f, 0.70f, 0.45f, 1f)
                : new Color(0.78f, 0.66f, 0.96f, 1f);
        }

        public static string ElementLabel(Element element)
        {
            return element == Element.Fire ? "API" : "GELAP";
        }

        public static Color AccentFor(CardInstance card)
        {
            if (card == null)
                return Gold;
            if (card.IsSupport)
                return FrameSupport;
            return ElementAccent(card.Element);
        }

        public static Color HpColor(float pct)
        {
            pct = Mathf.Clamp01(pct);
            return pct > 0.5f
                ? Color.Lerp(HpMid, HpFull, (pct - 0.5f) / 0.5f)
                : Color.Lerp(HpLow, HpMid, pct / 0.5f);
        }

        /// <summary>HP label with a quieter "HP" prefix so it never reads as attack.</summary>
        public static string HpLabel(int current, int max, bool richPrefix)
        {
            return richPrefix
                ? $"<size=70%>HP</size> {current}/{max}"
                : $"HP {current}/{max}";
        }

        /// <summary>Subtle outline token for flat action buttons.</summary>
        public static Color ButtonOutline(Color background)
        {
            float lum = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
            return lum > 0.55f
                ? WithAlpha(GoldDeep, 0.50f)
                : WithAlpha(Ivory, 0.18f);
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        public static Color Hex(int rgba)
        {
            return new Color32(
                (byte)(rgba >> 16 & 0xFF),
                (byte)(rgba >> 8 & 0xFF),
                (byte)(rgba & 0xFF),
                0xFF);
        }
    }
}
