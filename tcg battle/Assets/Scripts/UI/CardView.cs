using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using TcgBattle.Gameplay;

namespace TcgBattle.UI
{
    public enum CardDisplayMode { Active, Bench, Hand, Hidden }

    /// <summary>
    /// Reusable card visual. Shows the artist's full-card artwork (which already has the
    /// name, attack, flavor and frame baked in) plus lightweight live overlays:
    /// HP bar + text, element icon, attack-energy cost, KO/selected states.
    /// Handles its own click events. Built entirely in code - no prefab needed.
    /// </summary>
    public class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public CardInstance Card { get; private set; }
        public CardDisplayMode Mode { get; private set; } = CardDisplayMode.Active;
        public bool IsSelected { get; private set; }
        public bool IsEligible { get; private set; }
        public event Action<CardView> Clicked;

        /// <summary>Fires when the pointer enters/leaves this card's raycast area (bubbled from children).</summary>
        public event Action<CardView, bool> Hovered;

        private RectTransform _root;
        private Image _backing;
        private Image _frame;          // gold ring on actives, faint ivory on the bench
        private Image _selectedFrame;  // bright ring for selection feedback
        private Image _eligibleFrame;  // soft ring for "legal pick" cards (setup)
        private Image _targetFrame;    // action-target ring (attack / retreat affordance)
        private Image _art;
        private Image _dim;
        private Image _elementChip;
        private Image _elementIcon;
        private Image _hpBg;
        private Image _hpFill;
        private Image _costChip;
        private Image _costRing;
        private Image _supportChip;
        private TMP_Text _hpText;
        private TMP_Text _costText;
        private TMP_Text _supportText;
        private float _shownHpPct = 1f;
        private bool _hpTweening;
        private Coroutine _hpTween;
        private bool _hovered;
        private Color _frameDefault = ColFrameIvory;   // per-mode base frame color, set by Bind

        // current global layout scale (1 = the 1080x1920 reference composition)
        public static float GlobalScale = 1f;

        private List<ScaledItem> _scaled;   // captured lazily on first ApplyScale

        public RectTransform Root => _root;

        // palette (kept in sync with BattleUI)
        private static readonly Color ColBacking = Hex(0x1E1730);
        private static readonly Color ColBackingSupport = Hex(0x2A2148);
        private static readonly Color ColFrameGold = new Color(0xD9 / 255f, 0xA8 / 255f, 0x43 / 255f, 0.95f);
        private static readonly Color ColFrameIvory = new Color(0xF2 / 255f, 0xEA / 255f, 0xDA / 255f, 0.32f);
        private static readonly Color ColHpFull = Hex(0x6FA867);
        private static readonly Color ColHpMid = Hex(0xD9A843);
        private static readonly Color ColHpLow = Hex(0xC0504E);
        private static readonly Color ColIvory = Hex(0xF2EADA);
        private static readonly Color ColInk = Hex(0x241B36);
        private static readonly Color ColFrameHover = new Color(0.93f, 0.78f, 0.42f, 0.80f); // soft warm gold

        public static CardView Create(Transform parent, Vector2 size, string name = "Card")
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            var view = go.AddComponent<CardView>();
            view.EnsureBuilt();
            return view;
        }

        public void EnsureBuilt()
        {
            if (_backing != null)
                return;
            _root = (RectTransform)transform;

            // shadow first: it must sit behind the frame/target rings, not over them
            var shadow = MakeImage("Shadow", UiSpriteFactory.SoftShadow, new Color(0f, 0f, 0f, 0.5f));
            Stretch(shadow.rectTransform, -26, -26, -26, -26); // symmetric halo around the card

            _selectedFrame = MakeImage("SelectedFrame", UiSpriteFactory.RoundOutline,
                new Color(1f, 0.86f, 0.45f, 1f));
            _selectedFrame.type = Image.Type.Sliced;
            Stretch(_selectedFrame.rectTransform, -11, -11, -11, -11);
            _selectedFrame.gameObject.SetActive(false);

            _eligibleFrame = MakeImage("EligibleFrame", UiSpriteFactory.RoundOutline,
                new Color(0.95f, 0.80f, 0.45f, 0.75f));
            _eligibleFrame.type = Image.Type.Sliced;
            Stretch(_eligibleFrame.rectTransform, -7, -7, -7, -7);
            _eligibleFrame.gameObject.SetActive(false);

            _targetFrame = MakeImage("TargetFrame", UiSpriteFactory.RoundOutline,
                new Color(0.94f, 0.52f, 0.46f, 0.78f));
            _targetFrame.type = Image.Type.Sliced;
            Stretch(_targetFrame.rectTransform, -13, -13, -13, -13);
            _targetFrame.gameObject.SetActive(false);

            _frame = MakeImage("Frame", UiSpriteFactory.RoundOutline, ColFrameIvory);
            _frame.type = Image.Type.Sliced;
            Stretch(_frame.rectTransform, 1, 1, 1, 1);

            _backing = MakeImage("Backing", UiSpriteFactory.Rounded, ColBacking);
            _backing.type = Image.Type.Sliced;
            Stretch(_backing.rectTransform, 0, 0, 0, 0);
            _backing.raycastTarget = true; // the click target of the whole card

            _art = MakeImage("Art", null, Color.white);
            _art.preserveAspect = true;
            Stretch(_art.rectTransform, 6, 6, 6, 48);
            _art.raycastTarget = true;

            // ---- info strip (bottom): element icon + live HP ----
            var strip = new GameObject("InfoStrip", typeof(RectTransform));
            var stripRt = (RectTransform)strip.transform;
            strip.transform.SetParent(transform, false);
            stripRt.anchorMin = new Vector2(0f, 0f);
            stripRt.anchorMax = new Vector2(1f, 0f);
            stripRt.pivot = new Vector2(0.5f, 0f);
            stripRt.offsetMin = new Vector2(6f, 6f);
            stripRt.offsetMax = new Vector2(-6f, 46f);

            _elementChip = MakeImage("ElementChip", UiSpriteFactory.Circle, ColIvory);
            _elementChip.transform.SetParent(strip.transform, false);
            var chipRt = _elementChip.rectTransform;
            chipRt.sizeDelta = new Vector2(34, 34);
            chipRt.anchorMin = new Vector2(0, 0.5f);
            chipRt.anchorMax = new Vector2(0, 0.5f);
            chipRt.anchoredPosition = new Vector2(23, 0);

            _elementIcon = MakeImage("ElementIcon", null, Color.white);
            var elRt = _elementIcon.rectTransform;
            elRt.SetParent(strip.transform, false);
            elRt.sizeDelta = new Vector2(27, 27);
            elRt.anchorMin = new Vector2(0, 0.5f);
            elRt.anchorMax = new Vector2(0, 0.5f);
            elRt.anchoredPosition = new Vector2(23, 0);

            _hpBg = MakeImage("HpBg", UiSpriteFactory.Rounded, new Color(0f, 0f, 0f, 0.55f));
            _hpBg.type = Image.Type.Sliced;
            var bgRt = _hpBg.rectTransform;
            bgRt.SetParent(strip.transform, false);
            bgRt.anchorMin = new Vector2(0, 0.5f);
            bgRt.anchorMax = new Vector2(1, 0.5f);
            // bar is 30 units tall, vertically centred on the strip line
            bgRt.offsetMin = new Vector2(47, -15f);
            bgRt.offsetMax = new Vector2(-4, 15f);

            _hpFill = MakeImage("HpFill", UiSpriteFactory.Rounded, ColHpFull);
            _hpFill.type = Image.Type.Sliced;
            var fillRt = _hpFill.rectTransform;
            fillRt.SetParent(bgRt.transform, false);
            Stretch(fillRt, 3, 3, 3, 3);
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _hpFill.fillAmount = 1f;

            _hpText = MakeText("HpText", bgRt, 22, Color.white, TextAlignmentOptions.Center, bold: true);
            Stretch(_hpText.rectTransform, 2, 0, 2, 0);

            // ---- attack energy cost chip (top-left) ----
            _costChip = MakeImage("CostChip", UiSpriteFactory.Circle, ColHpMid);
            var costRt = _costChip.rectTransform;
            costRt.anchorMin = new Vector2(0, 1);
            costRt.anchorMax = new Vector2(0, 1);
            costRt.anchoredPosition = new Vector2(19, -19);
            costRt.sizeDelta = new Vector2(38, 38);
            _costRing = MakeImage("CostRing", UiSpriteFactory.Ring, new Color(0f, 0f, 0f, 0.45f));
            var ringRt = _costRing.rectTransform;
            ringRt.SetParent(costRt.transform, false);
            Stretch(ringRt, -2, -2, -2, -2);

            _costText = MakeText("CostText", costRt, 21, Hex(0x3A2506), TextAlignmentOptions.Center, bold: true);
            Stretch(_costText.rectTransform, 0, 0, 0, 0);

            // ---- support tag (top-right) ----
            _supportChip = MakeImage("SupportChip", UiSpriteFactory.Rounded, new Color(0.14f, 0.11f, 0.21f, 0.92f));
            _supportChip.type = Image.Type.Sliced;
            var supRt = _supportChip.rectTransform;
            supRt.anchorMin = new Vector2(1, 1);
            supRt.anchorMax = new Vector2(1, 1);
            supRt.anchoredPosition = new Vector2(-37, -15);
            supRt.sizeDelta = new Vector2(64, 26);
            _supportText = MakeText("SupportText", supRt, 15, ColIvory, TextAlignmentOptions.Center, bold: true);
            _supportText.text = "PASIF";
            Stretch(_supportText.rectTransform, 0, 0, 0, 0);

            _dim = MakeImage("Dim", null, new Color(0f, 0f, 0f, 0f));
            Stretch(_dim.rectTransform, 0, 0, 0, 0);
            _dim.raycastTarget = false;
        }

        public void Bind(CardInstance card, CardDisplayMode mode)
        {
            // A view being rebound to a different card must not keep tweening the old
            // card's HP (the KO/promotion path rebinds in the same frame as the damage).
            if (!ReferenceEquals(Card, card))
                StopHpTween();

            bool cardChanged = !ReferenceEquals(Card, card);
            Card = card;
            Mode = mode;
            gameObject.SetActive(card != null);
            if (card == null)
                return;

            // Board views are laid out by their slot and only ever leave that centre
            // for cosmetic lunges/shakes. A promoted/retreated card is a different
            // physical card, so start it from the slot centre instead of inheriting
            // a stale motion offset from the card it replaces.
            if (cardChanged && (mode == CardDisplayMode.Active || mode == CardDisplayMode.Bench) && _root != null)
            {
                _root.anchoredPosition = Vector2.zero;
                _root.localRotation = Quaternion.identity;
                _root.localScale = Vector3.one;
            }
            EnsureBuilt();

            bool isBattle = card.IsBattle;
            bool isBoard = mode == CardDisplayMode.Active || mode == CardDisplayMode.Bench;
            bool showStrip = isBoard && isBattle;

            _art.sprite = ArtLibrary.GetCardArt(card.ArtResource);
            _art.gameObject.SetActive(true);

            _elementIcon.sprite = ArtLibrary.GetElement(card.Element);
            _elementIcon.gameObject.SetActive(showStrip);
            _elementChip.gameObject.SetActive(showStrip);

            _costChip.gameObject.SetActive(isBattle && mode != CardDisplayMode.Hidden);
            if (isBattle)
                _costText.text = card.AttackCost.ToString();

            _supportChip.gameObject.SetActive(card.IsSupport);
            _backing.color = card.IsSupport ? ColBackingSupport : ColBacking;

            // mode frame: bright gold for the focal active, faint ivory on the bench/hand
            _frame.gameObject.SetActive(isBoard);
            _frameDefault = mode == CardDisplayMode.Active ? ColFrameGold : ColFrameIvory;
            _frame.color = _frameDefault;
            // Info strip: shown only for battle cards on the board.
            var strip = _hpBg.transform.parent.gameObject;
            strip.SetActive(showStrip);
            if (showStrip)
            {
                _hpText.fontSize = (mode == CardDisplayMode.Active ? 24f : 20f) * GlobalScale;
                SetHpDisplay(card.CurrentHp, card.MaxHp);
            }

            SetDimmed(false);
            SetHovered(false);
            SetSelected(false);
            SetEligible(false);
            SetTarget(false, false);
        }

        /// <summary>
        /// Scales every internal rect (and font) of this card to the canvas height.
        /// Captured lazily on first use; the HP text font is excluded because Bind sets
        /// it per display mode.
        /// </summary>
        public void ApplyScale(float k)
        {
            if (_root == null)
                return;
            if (_scaled == null)
            {
                _scaled = new List<ScaledItem>();
                UiRescaler.CaptureTree(_root, _scaled);
                // Layout systems (HandController fan, slot tweens, attack lunge) own the
                // root's position; the rescale pass may only resize it.
                if (_scaled.Count > 0)
                    _scaled[0].ApplyPosition = false;
                foreach (var it in _scaled)
                    if (it.RT == _hpText.rectTransform)
                        it.Text = null; // Bind owns this font size
            }
            foreach (var it in _scaled)
                it.Apply(k);
        }

        /// <summary>HP bar + text, tinted by remaining ratio (green - amber - red).</summary>
        public void SetHpDisplay(int current, int max)
        {
            if (_hpFill == null || max <= 0)
                return;
            // While a damage tween is running it owns both the fill and the number, so
            // state refreshes can't snap the bar ahead of the impact.
            if (_hpTweening)
                return;
            float pct = Mathf.Clamp01((float)current / max);
            _hpFill.fillAmount = pct;
            _shownHpPct = pct;
            _hpText.text = $"{current}/{max}";
            _hpFill.color = HpColor(pct);
        }

        /// <summary>
        /// Smooth HP bar tween used when damage lands. A delay holds the pre-damage bar
        /// until the attack's impact frame; the number counts down with the bar.
        /// </summary>
        public void TweenHpTo(int current, int max, float duration = 0.4f, float delay = 0f)
        {
            if (_hpFill == null || max <= 0 || gameObject == null || !gameObject.activeInHierarchy)
                return;
            if (_hpTween != null)
                StopCoroutine(_hpTween);
            _hpTweening = true; // synchronously, before the manager's same-frame refresh
            _hpTween = StartCoroutine(TweenHpRoutine(current, max, duration, delay));
        }

        private void StopHpTween()
        {
            if (_hpTween != null)
                StopCoroutine(_hpTween);
            _hpTween = null;
            _hpTweening = false;
        }

        private System.Collections.IEnumerator TweenHpRoutine(int current, int max, float duration, float delay)
        {
            float from = _shownHpPct;
            int fromHp = Mathf.RoundToInt(from * max);
            float to = Mathf.Clamp01((float)current / max);
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < duration)
            {
                if (_hpFill == null)
                    break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                float pct = Mathf.Lerp(from, to, k);
                _hpFill.fillAmount = pct;
                _hpFill.color = HpColor(pct);
                _hpText.text = $"{Mathf.RoundToInt(Mathf.Lerp(fromHp, current, k))}/{max}";
                yield return null;
            }
            if (_hpFill != null)
            {
                _hpFill.fillAmount = to;
                _hpFill.color = HpColor(to);
                _hpText.text = $"{current}/{max}";
                _shownHpPct = to;
            }
            _hpTweening = false;
            _hpTween = null;
        }

        private static Color HpColor(float pct)
        {
            return pct > 0.5f
                ? Color.Lerp(ColHpMid, ColHpFull, (pct - 0.5f) / 0.5f)
                : Color.Lerp(ColHpLow, ColHpMid, pct / 0.5f);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (_selectedFrame != null)
                _selectedFrame.gameObject.SetActive(selected);
        }

        /// <summary>Soft ring marking a card that is a legal pick right now (setup).</summary>
        public void SetEligible(bool eligible)
        {
            IsEligible = eligible;
            if (_eligibleFrame != null)
                _eligibleFrame.gameObject.SetActive(eligible);
        }

        /// <summary>
        /// Action-target affordance: harmful = a valid enemy to attack, helpful = a valid
        /// bench card to retreat to. Purely visual - clicks always keep their normal rules.
        /// </summary>
        public void SetTarget(bool targeted, bool harmful)
        {
            if (_targetFrame == null)
                return;
            _targetFrame.gameObject.SetActive(targeted);
            if (targeted)
                _targetFrame.color = harmful
                    ? new Color(0.94f, 0.52f, 0.46f, 0.78f)
                    : new Color(0.95f, 0.80f, 0.45f, 0.70f);
        }

        /// <summary>Whether the card is currently hover-highlighted.</summary>
        public bool IsHovered => _hovered;

        /// <summary>
        /// Hover highlight: warms the mode frame in place. Deliberately no new child
        /// objects and no motion here, so the rescale capture stays valid and the
        /// fan layout (HandController) remains the single owner of card movement.
        /// </summary>
        public void SetHovered(bool hovered)
        {
            _hovered = hovered;
            if (_frame != null)
                _frame.color = hovered ? ColFrameHover : _frameDefault;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Card == null || !gameObject.activeInHierarchy)
                return;
            SetHovered(true);
            Hovered?.Invoke(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Process even for a dying view: listeners may need to clear their state.
            SetHovered(false);
            Hovered?.Invoke(this, false);
        }

        public void SetDimmed(bool dimmed, float alpha = 0.55f)
        {
            if (_dim != null)
                _dim.color = new Color(0f, 0f, 0f, dimmed ? alpha : 0f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }

        private Image MakeImage(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
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
            rt.SetParent(parent, false);
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

        private static Color Hex(int rgba)
        {
            return new Color32((byte)(rgba >> 16 & 0xFF), (byte)(rgba >> 8 & 0xFF), (byte)(rgba & 0xFF), 0xFF);
        }
    }
}
