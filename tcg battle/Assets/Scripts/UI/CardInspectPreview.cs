using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TcgBattle.Gameplay;

namespace TcgBattle.UI
{
    /// <summary>
    /// Passive inspection overlay: a larger, readable rendering of the hand card the
    /// pointer is over. Purely informational - it shows a battle card's art, name,
    /// live HP, attack (energy cost + power + attack name), element chip, and the
    /// effect note from the card data when the data carries one. Support cards show
    /// name/art/PASIF only (the data defines no description text; nothing is invented).
    /// The overlay never intercepts input (blocksRaycasts off on the whole group) and
    /// is kept inside its parent's (SafeRoot) rect so it can never be clipped or hang
    /// over a notch. HandController drives Show/HideIfCard from CardView hover events.
    /// Built entirely in code - no prefab, no new art; sprites come from ArtLibrary.
    /// </summary>
    public class CardInspectPreview : MonoBehaviour
    {
        // palette (kept in sync with BattleUI/CardView)
        private static readonly Color ColBacking = Hex(0x1E1730);
        private static readonly Color ColBackingSupport = Hex(0x2A2148);
        private static readonly Color ColPanelInk = Hex(0x1C1530);
        private static readonly Color ColGoldSoft = new Color(0xD9 / 255f, 0xA8 / 255f, 0x43 / 255f, 0.30f);
        private static readonly Color ColIvory = Hex(0xF2EADA);
        private static readonly Color ColIvoryDim = Hex(0xC9BFDB);
        private static readonly Color ColInk = Hex(0x241B36);
        private static readonly Color ColDarkInk = new Color(0.14f, 0.11f, 0.21f, 0.92f);
        private static readonly Color ColGoldBright = Hex(0xF2C14E);
        private static readonly Color ColHpFull = Hex(0x6FA867);
        private static readonly Color ColHpMid = Hex(0xD9A843);
        private static readonly Color ColHpLow = Hex(0xC0504E);

        private RectTransform _rt;
        private CanvasGroup _group;
        private Image _backing;
        private Image _art;
        private Image _elementChip;
        private Image _elementIcon;
        private Image _hpBarBg;
        private Image _hpFill;
        private TMP_Text _hpText;
        private Image _costChip;
        private TMP_Text _costText;
        private TMP_Text _nameText;
        private TMP_Text _atkText;
        private TMP_Text _effectText;
        private Image _passifChip;
        private TMP_Text _passifText;

        private CardInstance _shownCard;
        private RectTransform _source;
        private Coroutine _fade;

        /// <summary>The card whose data is currently previewed, or null.</summary>
        public CardInstance CurrentCard => _shownCard;

        private void Awake()
        {
            if (_backing != null)
                return;
            EnsureBuilt();
        }

        public void EnsureBuilt()
        {
            if (_backing != null)
                return;
            _rt = (RectTransform)transform;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false; // the preview must never intercept input
            _group.interactable = false;
            _group.alpha = 0f;

            // soft drop shadow so the panel separates from the board artwork
            var shadow = MakeImage("Shadow", UiSpriteFactory.Rounded, new Color(0f, 0f, 0f, 0.38f));
            shadow.type = Image.Type.Sliced;
            Stretch(shadow.rectTransform, -12, -18, -12, -4);

            _backing = MakeImage("Panel", UiSpriteFactory.Rounded, ColBacking);
            _backing.type = Image.Type.Sliced;
            Stretch(_backing.rectTransform, 0, 0, 0, 0);

            var outline = MakeImage("Outline", UiSpriteFactory.RoundOutline, ColGoldSoft);
            outline.type = Image.Type.Sliced;
            Stretch(outline.rectTransform, 2, 2, 2, 2);

            _art = MakeImage("Art", null, Color.white);
            _art.preserveAspect = true;
            Stretch(_art.rectTransform, 12, 196, 12, 14);

            _nameText = MakeText("Name", _backing.rectTransform, 27, ColIvory, TextAlignmentOptions.Center, bold: true);
            Anchor(_nameText.rectTransform, 0f, 0f, 1f, 0f, 12f, 152f, -12f, 184f);

            _hpBarBg = MakeImage("HpBg", UiSpriteFactory.Rounded, new Color(0f, 0f, 0f, 0.55f));
            _hpBarBg.type = Image.Type.Sliced;
            Anchor(_hpBarBg.rectTransform, 0f, 0f, 1f, 0f, 12f, 114f, -12f, 144f);
            _hpFill = MakeImage("HpFill", UiSpriteFactory.Rounded, ColHpFull, _hpBarBg.transform);
            _hpFill.type = Image.Type.Sliced;
            Stretch(_hpFill.rectTransform, 3, 3, 3, 3);
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _hpFill.fillAmount = 1f;
            _hpText = MakeText("HpText", _hpBarBg.rectTransform, 19, Color.white, TextAlignmentOptions.Center, bold: true);
            Stretch(_hpText.rectTransform, 2, 0, 2, 0);

            _costChip = MakeImage("CostChip", UiSpriteFactory.Circle, ColGoldBright);
            Anchor(_costChip.rectTransform, 0f, 0f, 0f, 0f, 12f, 68f, 46f, 102f);
            var costRing = MakeImage("CostRing", UiSpriteFactory.Ring, new Color(0f, 0f, 0f, 0.45f), _costChip.transform);
            Stretch(costRing.rectTransform, -2, -2, -2, -2);
            _costText = MakeText("CostText", _costChip.rectTransform, 21, Hex(0x3A2506), TextAlignmentOptions.Center, bold: true);
            Stretch(_costText.rectTransform, 0, 0, 0, 0);

            _atkText = MakeText("AtkText", _backing.rectTransform, 21, ColIvory, TextAlignmentOptions.Left, bold: true);
            Anchor(_atkText.rectTransform, 0f, 0f, 1f, 0f, 54f, 70f, -12f, 100f);

            _effectText = MakeText("EffectText", _backing.rectTransform, 19, ColIvoryDim, TextAlignmentOptions.Center);
            Anchor(_effectText.rectTransform, 0f, 0f, 1f, 0f, 12f, 16f, -12f, 64f);

            _elementChip = MakeImage("ElementChip", UiSpriteFactory.Circle, ColIvory);
            Anchor(_elementChip.rectTransform, 0f, 1f, 0f, 1f, 10f, -40f, 40f, -10f);
            _elementIcon = MakeImage("ElementIcon", null, Color.white, _elementChip.transform);
            Stretch(_elementIcon.rectTransform, 3, 3, 3, 3);

            _passifChip = MakeImage("PassifChip", UiSpriteFactory.Rounded, ColDarkInk);
            _passifChip.type = Image.Type.Sliced;
            Anchor(_passifChip.rectTransform, 1f, 1f, 1f, 1f, -74f, -36f, -10f, -12f);
            _passifText = MakeText("PassifText", _passifChip.rectTransform, 15, ColIvory, TextAlignmentOptions.Center, bold: true);
            _passifText.text = "PASIF";
            Stretch(_passifText.rectTransform, 0, 0, 0, 0);
        }

        /// <summary>
        /// Show the preview for a card. Content is rebound without restarting the
        /// fade when another card follows (rapid sweeps restage seamlessly); the
        /// appearance animates the first time it appears.
        /// </summary>
        public void Show(CardInstance card, RectTransform cardRect)
        {
            if (card == null || cardRect == null)
            {
                Hide();
                return;
            }
            _source = cardRect;

            _shownCard = card;

            Bind(card);
            Reposition();

            // Take ownership of any fade in progress: a fresh show animates up from
            // the current alpha (usually 0), an interrupt of a fade-in/out continues
            // seamlessly, and an already-visible swap does nothing. This covers the
            // "exit then enter in the same frame" race between neighboring cards.
            if (_fade != null)
                StopCoroutine(_fade);
            float aFrom = _group.alpha;
            float sFrom = _rt.localScale.x;
            if (aFrom < 0.99f || !Mathf.Approximately(sFrom, 1f))
            {
                float dur = Mathf.Clamp(0.05f + 0.07f * (1f - aFrom), 0.05f, 0.12f);
                _fade = StartCoroutine(FadeRoutine(aFrom, 1f, sFrom, 1f, dur));
            }
        }

        /// <summary>Hide only if the given card is still the one being previewed.</summary>
        public void HideIfCard(CardInstance card)
        {
            if (card != null && _shownCard == card)
                Hide();
        }

        public void Hide()
        {
            if (_shownCard == null && _group.alpha <= 0.01f)
                return;
            _shownCard = null;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(_group.alpha, 0f, _rt.localScale.x, 0.94f, 0.10f));
        }

        /// <summary>Bind the panel contents to a card's data (only what the data has).</summary>
        private void Bind(CardInstance card)
        {
            bool isSupport = card.IsSupport;
            _backing.color = isSupport ? ColBackingSupport : ColBacking;

            _art.sprite = ArtLibrary.GetCardArt(card.ArtResource);

            _nameText.text = card.IsBattle || card.Support != null ? card.DisplayName : card.CardId;

            bool battle = card.IsBattle;
            _hpBarBg.gameObject.SetActive(battle);
            _costChip.gameObject.SetActive(battle);
            _atkText.gameObject.SetActive(battle);
            _elementChip.gameObject.SetActive(battle);
            _passifChip.gameObject.SetActive(isSupport);
            _effectText.gameObject.SetActive(false); // re-enabled below only with real note text

            if (!battle)
                return;

            // HP (live values)
            float pct = card.MaxHp > 0 ? Mathf.Clamp01((float)card.CurrentHp / card.MaxHp) : 0f;
            _hpFill.fillAmount = pct;
            _hpFill.color = pct > 0.5f
                ? Color.Lerp(ColHpMid, ColHpFull, (pct - 0.5f) / 0.5f)
                : Color.Lerp(ColHpLow, ColHpMid, pct / 0.5f);
            _hpText.text = $"{card.CurrentHp}/{card.MaxHp}";

            // attack: energy cost + power + attack name (always present in the data)
            _costText.text = card.AttackCost.ToString();
            var def = card.Battle;
            string atkName = def != null && !string.IsNullOrEmpty(def.attackName) ? def.attackName : "";
            _atkText.text = string.IsNullOrEmpty(atkName)
                ? $"ATK {card.AttackPower}"
                : $"ATK {card.AttackPower} \u00b7 {atkName}";

            // element
            _elementIcon.sprite = ArtLibrary.GetElement(card.Element);

            // effect note from the data, only when the data carries one
            string note = def != null ? def.attackEffectText : null;
            bool hasNote = !string.IsNullOrEmpty(note);
            _effectText.gameObject.SetActive(hasNote);
            if (hasNote)
                _effectText.text = note;
        }

        /// <summary>
        /// Landscape docking. The compact panel has no room above or beside the bot's
        /// top-row cards without covering the controls, so board previews dock in the
        /// empty upper-right well (leaving the secondary stamina diamond clear) and
        /// hand previews dock in the empty lower-left well. Neither dock overlaps the
        /// action bar, the hovered card, the player hand, or the score/stamina rails.
        /// The panel is still clamped inside SafeRoot for other aspect ratios.
        /// </summary>
        private void Reposition()
        {
            if (_source == null)
                return;
            var pr = (RectTransform)transform.parent;
            if (pr == null)
                return;

            float halfW = _rt.rect.width * 0.5f;
            float halfH = _rt.rect.height * 0.5f;
            float parentHalfW = pr.rect.width * 0.5f;
            float parentHalfH = pr.rect.height * 0.5f;
            float padW = Mathf.Max(0f, parentHalfW - halfW - 10f);
            float padH = Mathf.Max(0f, parentHalfH - halfH - 10f);

            Vector3 topWorld = _source.TransformPoint(new Vector3(0f, _source.rect.height * 0.5f, 0f));
            Vector2 top = pr.InverseTransformPoint(topWorld);
            bool lowerSource = top.y < 0f;

            if (lowerSource)
            {
                // Hand cards: restore the original presentation - centred directly
                // above the hovered card with a small gap, so the info reads as part
                // of that card instead of a detached side panel.
                Vector3 leftWorld = _source.TransformPoint(new Vector3(-_source.rect.width * 0.5f, 0f, 0f));
                Vector3 rightWorld = _source.TransformPoint(new Vector3(_source.rect.width * 0.5f, 0f, 0f));
                Vector2 left = pr.InverseTransformPoint(leftWorld);
                Vector2 right = pr.InverseTransformPoint(rightWorld);
                float xAbove = (left.x + right.x) * 0.5f;
                float yAbove = top.y + 14f + halfH;
                _rt.anchoredPosition = new Vector2(
                    Mathf.Clamp(xAbove, -padW, padW),
                    Mathf.Clamp(yAbove, -padH, padH));
                return;
            }

            // Bot board cards sit against the top edge; dock the panel in the empty
            // upper-right well, clear of the action bar and source card. The 160px
            // reserve keeps the bot stamina diamond/deck stack out from under it.
            float boardX = parentHalfW - 10f - 160f - halfW;
            float boardY = parentHalfH - 10f - halfH;
            _rt.anchoredPosition = new Vector2(
                Mathf.Clamp(boardX, -padW, padW),
                Mathf.Clamp(boardY, -padH, padH));
        }

        private void OnRectTransformDimensionsChange()
        {
            // canvas rescale while visible: keep the clamp fresh against the new rect
            if (_source != null && isActiveAndEnabled)
                Reposition();
        }

        private IEnumerator FadeRoutine(float aFrom, float aTo, float sFrom, float sTo, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                if (_group == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                _group.alpha = Mathf.Lerp(aFrom, aTo, k);
                float s = Mathf.Lerp(sFrom, sTo, k);
                _rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (_group != null)
            {
                _group.alpha = aTo;
                _rt.localScale = new Vector3(sTo, sTo, 1f);
            }
            _fade = null;
        }

        private Image MakeImage(string name, Sprite sprite, Color color, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent != null ? parent : transform, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TMP_Text MakeText(string name, Transform parent, float fontSize, Color color,
            TextAlignmentOptions align, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent != null ? parent : transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = TmpFontService.Get();
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            if (bold)
                tmp.fontStyle = FontStyles.Bold;
            return tmp;
        }

        private static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static void Anchor(RectTransform rt, float x0, float y0, float x1, float y1,
            float left, float bottom, float right, float top)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        private static Color Hex(int rgba)
        {
            return new Color32((byte)(rgba >> 16 & 0xFF), (byte)(rgba >> 8 & 0xFF), (byte)(rgba & 0xFF), 0xFF);
        }
    }
}
